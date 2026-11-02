---
created: 2026-11-02
status: implemented
---

# Design Specification: Entities Dashboard

> This specification defines a dashboard page for browsing, searching and editing `IEntity` instances exposed through Repositories and ActiveEntity. It behaves like a rough table editor for development and operations.

[TOC]

## Overview

The Entities page lets developers and operators inspect and maintain persisted entities without writing ad-hoc queries or tooling. It supports every `IEntity` type reachable through a registered `IGenericRepository<T>` or an ActiveEntity registration. Search uses the existing `FilterModel`. The page is read-only by default and enables write operations only for principals that satisfy a configured authorization policy.

## Goals

- One page covering both repository-backed and ActiveEntity-backed entities.
- Automatic discovery of entity types, with opt-out per entity.
- Search, sort and incremental loading through `FilterModel`, with no raw JSON editor in the normal filter form.
- Full CRUD (create, update, delete) with concurrency protection.
- Scalar and JSON (value object) property editing.
- Opt-in property masking per entity.
- Same look, registration and disabling conventions as the other dashboard pages (Profiling, Auditing, Change History).

## Non-Goals

- Import, data migration, or schema editing. Export is in scope (see Copy and export).
- Cross-entity joins or aggregation queries.
- Editing child collections (owned collections, navigation graphs) beyond the JSON representation of a property.
- Replacing application-level commands; the page is a maintenance tool.
- Documentation examples that use repository example applications (use fictional models only).

## Page Registration

| Item | Value |
| --- | --- |
| Page key | `entities` |
| Title | Entities |
| Group / GroupOrder | `bdk` / `0` |
| Order | next to Change History (about 66) |
| Route | `{dashboard}/entities` (via `DashboardEndpointsOptions` / `DashboardPath`) |
| Icon | `table` (Bootstrap fallback) plus the existing canonical feature SVG `entities.svg`, per the icon README |

### Enablement

1. The page is available when at least one entity source is registered: an `IGenericRepository<T>` (T : `IEntity`) or an ActiveEntity registration.
2. When neither is registered the page is not listed and its card is omitted.
3. Explicit disabling follows the existing mechanism: `DashboardEndpointsOptions.DisabledPageKeys.Add("entities")`. Disabled pages are not mapped and return 404.
4. The overview card shows the count of discovered entity types, or `Unavailable` when none exist.

## Entity Discovery

- Sources: closed `IGenericRepository<T>` registrations and ActiveEntity types that implement `IEntity`.
- ActiveEntity types are discovered through an `ActiveEntityRegistry` that the ActiveEntity configuration fills automatically. Developers do not register anything extra.
  - Lifetime: the registry is a plain instance obtained through `ActiveEntityRegistry.GetOrAdd(IServiceCollection)`, which reuses the instance already added to the `IServiceCollection` or adds a new one with `services.AddSingleton(registry)` as an instance registration. Several `AddActiveEntity(...)` calls therefore share one registry, and ordering against `AddEntitiesDashboard()` does not matter, because the dashboard resolves it from the built container.
  - The `ActiveEntityConfigurator<TEntity, TId>` constructor records a descriptor (entity type, id type, soft-delete flag) into the registry at configuration time, not inside the deferred registration lambda, so it is complete when the container is built.
  - Both configuration paths construct that configurator: `ActiveEntityConfigurationBuilder.For<TEntity, TId>()` (and `For<TEntity>()`) and `AddActiveEntity<TEntity, TId>()` (`ActiveEntityBuilder`). Recording does not depend on the optional `Build()` call. Recording the same entity type twice is idempotent.
  - Domain holds only the registry and descriptor types, with no dashboard dependency.
- An entity type registered through both sources appears once, labelled with each available source. If both exist, the selector lets the user choose; the default is the repository.
- Discovery is automatic. Types are excluded with setup-time opt-out:

```csharp
services.AddEntitiesDashboard(options => options
    .Exclude<AuditLogEntry>()
    .Exclude(typeof(OutboxMessage)));
```

- DevKit infrastructure entity types (change history entries, outbox messages, storage documents, and so on) are excluded by default through an internal deny list, in case they are registered as an entity source. Users can re-include them with `Include<T>()`.
- Discovery is done per request scope and cached by type metadata only. Entity data is never cached.
- Entity types are listed by short name, with the full namespace in a tooltip (using the shared `Shortener`).
- Multiple DbContexts and tenants: the dashboard uses whatever repository or ActiveEntity registration the DI container resolves and adds no context or tenant switching of its own. Each entity type appears once per source, using the registration the container resolves for `IGenericRepository<T>` (the last one when several exist). The DbContext behind a repository is not discoverable through the interface and is not shown. Tenant scoping remains the job of the existing repository behaviors.

## Access Model

| Mode | Requirement | Capabilities |
| --- | --- | --- |
| View | Normal dashboard access | List, search, sort, load more, view details, saved filters, column picker, copy, export, deep links, related entity navigation, Change History link |
| Edit | Global `EditPolicy` (or `EditRoles`) is satisfied | Everything in View, plus create, clone, update (with diff), delete and bulk delete |

- A single global setting, not per entity:

```csharp
options.EditPolicy = "EntitiesEditor";   // authorization policy name
options.EditRoles = ["Admin"];           // alternative; either may be set
```

- If neither is configured, the page is permanently read-only. Edit is deny-by-default.
- The server evaluates the policy on every write action. Hiding UI controls is never the only protection.
- A principal without edit access sees no edit toggle and no create/delete controls. Write endpoints return 403.
- The current mode is shown by the pressed state of the Edit toggle (`aria-pressed`), not by a separate badge.
- Edit mode is a session toggle. It starts in View, even for editors, so that edits are deliberate.
- No antiforgery tokens. The dashboard is a backend admin surface, so the write endpoints (create, update, delete, bulk delete) follow the existing dashboard POST convention: `.DisableAntiforgery()` metadata and JSON bodies. Protection comes from the host's dashboard authorization plus the server-side edit check on every write.

## Search and Listing

### Filtering

- The filter toolbar builds a `FilterModel` from simple controls:
  - Property selector (scalar properties of the entity).
  - Operator selector, based on the property type (`contains`, `equals`, `startsWith`, `>`, `<`, `between`, `isNull`, and so on, limited to operators supported by `FilterModel`).
  - Value input typed to the property (text, number, date, boolean, enum).
  - Multiple conditions combined with AND (OR via an optional match mode toggle).
- A quick-search box applies `contains` across the entity's string properties.
- Apply and Reset use text labels. Filter state is kept in query parameters so selections are shareable.
- Invalid filters (unknown property, wrong type) show an inline error and never an unhandled exception.

### Table

- Columns come from the entity's public scalar properties and value-object properties. Columns are ordered with `Id` first.
- Value-object and complex properties show as a compact JSON preview, truncated with the full value in a tooltip.
- Filter properties: top-level scalar properties plus one level of value-object scalar properties as dotted paths (for example `Address.City`). Collections, deeper nesting and masked properties are not offered.
- Sorting works on top-level scalar columns through `FindOptions` ordering (single column, ascending or descending).
- Default order: when the entity implements `IAuditable`, rows show the latest audit action first through the orderings `AuditState.UpdatedDate` descending, `AuditState.CreatedDate` descending, `Id` ascending. Dotted order paths are supported by `OrderOptionBuilder`. The computed `AuditState.LastActionDate` is not used because it cannot be translated to a query. `AuditState` sets `UpdatedDate` on create, update, deactivate and delete, so this matches the newest audit date. Otherwise the default is `Id` ascending. `Id` is always the last ordering so that "Load more" stays stable.
- Audit state is shown as read-only columns (created, updated, deleted) and is never editable.
- Loading uses a "Load more" button, as in the Auditing and Logging dashboards, and not numbered paging. The intended workflow is to narrow the list with filters, not to page through whole tables.
- The first request returns one batch (default 50, `options.BatchSize`, never above `MaxBatchSize`, default 200). "Load more" fetches the next batch from the next offset through a rows fragment and appends it. The button is hidden when a batch comes back short.
- The loaded row count is shown (`50 loaded`). A total count is not required and is not shown.
- Changing filters, sort or entity type resets the list to the first batch.
- Auto-refresh is off for this page. Refresh is manual, and edits update the affected row in place.
- A sticky table head and an independent scroll region follow the Change History style.
- Row selection opens a detail panel with every property (full values, never truncated) and, in edit mode, edit and delete actions.
- Empty, error and unavailable states have distinct text.

### Data access

- Repository-backed reads and writes go through the result extensions (`GenericReadOnlyRepositoryResultExtensions`: `FindOneResultAsync`, `FindAllResultAsync(FilterModel, ...)`; `GenericRepositoryResultExtensions`: `InsertResultAsync`, `UpdateResultAsync`, `DeleteResultAsync`). ActiveEntity-backed access uses its own `FilterModel` and `Result<>` overloads.
- Batches use `FilterModel.Page`/`PageSize` with `PageSize` fixed to the batch size for the lifetime of a list, so batch *n* maps to `Skip = (n - 1) * BatchSize`, `Take = BatchSize` through `FindOptionsBuilder`. `FindAllResultPagedAsync` is not used because it runs an extra count query that the page does not need.
- Export uses the same query with `Take = MaxExportRows + 1` to detect truncation. Both sources therefore yield a `Result`, and the dashboard maps it to one outcome shape: success flag, field-level errors and a summary message.
- Expected failures (validation, rule violations, concurrency conflicts, not found) show as inline messages in the modal. Unexpected exceptions show a generic error with a correlation id; the full detail goes to the log.
- Reads use `FilterModel` and `FindOptions`. The page does not use `DbContext` directly.
- Requests use the request `CancellationToken`.

## Editing (Edit Mode)

### Create

- Opens a modal form with an input per editable property. Fields are generated from the property type.
- The `Id` is editable on create only if it is not store-generated (Guid, int identity). Default behavior: if the `Id` is left empty and the type is `Guid`, one is generated.
- Masked properties are write-only inputs: empty, with an optional value. Hidden properties are not in the form and keep their CLR default. If validation requires a hidden property, the create fails with the validation message. The documentation recommends not hiding required properties of creatable types.
- On submit, the entity is built from the form and saved through `InsertResultAsync` (or the ActiveEntity overload).

### Update

- The modal is pre-filled from the current entity. Only changed properties are applied.
- If the entity implements `IConcurrency`, the `ConcurrencyVersion` (a `Guid`) shown in the form is submitted with the update. The server loads the current entity and compares the versions before applying changes. A mismatch returns a conflict message with a reload action, and nothing is written. Races between this check and the save are still caught by the store's own concurrency token, and that failure is reported as the same conflict.
- Entities without `IConcurrency` use last-write-wins. The editor footer says so.
- Read-only properties are displayed disabled: `Id`, `ConcurrencyVersion`, and audit state properties (`IAuditable`).

### Delete

- Needs a confirmation (shared `confirmAction`) naming the entity type and Id.
- Uses `DeleteResultAsync` by Id (or the ActiveEntity overload), so soft-delete behaves as the repository or ActiveEntity is configured. The dashboard never offers a hard delete.
- Soft-deleted rows are not specially fetched. The repository soft-delete decorator (`RepositorySoftDeleteBehavior`, filter `Deleted != true`) wraps the inner repository, so the dashboard cannot opt out of it without bypassing the registered pipeline, which it must not do. The dashboard therefore shows whatever the registered repository or ActiveEntity returns, so deleted rows are normally omitted.
- When a returned row is soft-deleted (`ISoftDeletable.Deleted` or `AuditState.IsDeleted()`), for example because the stack is configured not to exclude them, it carries a "deleted" badge and is read-only (no edit or delete actions).

### Identifiers

- Typed ids (for example `CustomerId` based on `EntityId`) are handled through their underlying primitive value. The table, filters and detail URLs show and accept the primitive as text.
- The primitive is converted back through the typed id's own parse/convert logic. An invalid id returns a validation error.
- On create, an empty id generates a `Guid` where the primitive type is `Guid`. Otherwise an id is required unless the store generates it.

### Building the entity to save

- **Update:** the entity is loaded, then only changed properties are applied, then `UpdateAsync` is called. Properties with a public setter are set directly. Properties with a non-public setter are set through reflection only if the entity type opts in (`options.Entity<T>(e => e.AllowNonPublicSetters())`). Properties with no usable setter display read-only.
- **Create:** uses a public parameterless constructor. With the opt-in, a non-public parameterless constructor is also used (`Activator` with non-public access). If neither works, the type is view and delete only, and the UI says why.
- Reflection writes bypass domain invariants in setters and factory methods. This is documented, and the opt-in is per entity so the risk is explicit.
- Repository or ActiveEntity behaviors (validation, auditing, domain events) still run on the save call.

### Property editors

| Property kind | Editor |
| --- | --- |
| string, number | text / number input with parse validation |
| Guid | text input with `Guid.TryParse` validation, "New Guid" button on create |
| bool | checkbox |
| DateTime / DateTimeOffset | UTC date-time input |
| enum / `EnumerationValueObject` | select (enumerations list their static `All`, storing the key) |
| Nullable | input plus a "null" toggle |
| Value object / complex | JSON textarea (validated, pretty-printed) |
| Collections of scalars | JSON textarea |
| Unsupported (navigation graphs, byte arrays) | shown read-only, with a short note |

### JSON serialization

- System.Text.Json with indented output, camelCase names and string-valued enums, used for both display and parsing.
- On save the JSON is parsed and deserialized into the property type. Malformed or mismatched JSON is rejected with a clear error before reaching the repository.
- Types with a custom converter round-trip through that converter.
- Value objects that cannot be deserialized (no usable constructor or setters) show their JSON read-only.

### Validation and domain behavior

- Writes go through the normal repository or ActiveEntity pipeline, so behaviors (validation, auditing, domain events) run as for any other write.
- Validation failures are returned as field-level messages when possible, otherwise as a summary.
- Domain rules that reject the change surface their message to the user. The dashboard does not bypass them.
- JSON that does not deserialize into the property type returns a clear error and nothing is saved.
- Every successful write is logged with `[EntitiesDashboard]` message templates (entity type, id, operation, user). Property values are never logged.
- No separate audit store. `IAuditable` state, domain events and existing audit behaviors run as for any other write, so the writes also show up in the Auditing dashboard when it is enabled.

## User Features

Features beyond basic list and CRUD. Everything here respects masking and the access model.

### Saved and recent filters

- Each entity keeps the last 10 applied filters ("recent") and named saved filters. Both are stored in browser `localStorage`, keyed by page and entity key, and are never sent to the server.
- A saved filter stores the `FilterModel` conditions, match mode and sort. Applying one resets the list to the first batch. Saved filters can be renamed and deleted.
- Stored filters that reference a property that no longer exists or is now masked are shown disabled with a reason.

### Column picker

- A column menu toggles column visibility and order per entity. The choice is stored in `localStorage` and a "Reset columns" action restores the defaults.
- `Id` cannot be hidden. Masked and hidden properties are not listed.

### Copy and export

- Row actions: "Copy as JSON" copies the full row (masking applied) using the same serializer as the JSON editor.
- Export: "Export JSON" and "Export CSV" download the rows matching the current filter and sort, with only the visible columns for CSV. Export is a server endpoint and runs the same query path, so masking and exclusions apply.
- Export is capped by `options.MaxExportRows` (default 5000). When the cap is hit, the file is truncated and the UI says so before download. Export is a read operation (no `EditPolicy`). The action is logged (`[EntitiesDashboard]`, entity, row count, no values).
- CSV: UTF-8, invariant culture, value objects as JSON in a single cell, formula-leading characters (`=`, `+`, `-`, `@`) are prefixed to prevent spreadsheet injection.

### Clone

- Edit mode only. "Clone" in the row actions opens the create modal prefilled from the selected row.
- `Id`, `ConcurrencyVersion` and audit state are cleared, so the normal create rules apply (generated or user-entered `Id`). Masked and hidden properties start empty, because their values are never sent to the browser.

### Deep links

- The URL carries entity key, filter, sort and the selected `Id`. Column visibility is a per-browser preference and is not part of the URL. Opening such a URL restores the state and opens the detail panel for that row.
- If the row no longer exists (or is no longer returned) the page shows a "not found" message and the list.
- Deep links never carry edit state. Edit mode is decided by the principal on each load.

### Diff on save

- On update, saving first shows a before/after summary of the changed properties (value objects as indented JSON diff lines) and requires a second confirmation. Create does not show a diff.
- Masked and hidden properties are never listed. A masked property that was replaced shows "changed" without values.
- If nothing changed, the save is skipped with a "no changes" message.

### Related entity navigation

- A top-level property is detected as a reference to a discovered, non-excluded entity type when either:
  - its type is that entity's typed id type (for example a `CustomerId` property type referencing `Customer`), or
  - its name is `<EntityName>Id` (for example `CustomerId` referencing `Customer`) and its type, or the typed id's primitive, equals the target's id primitive type.
- Detected references render as links. Ambiguous matches (several targets with the same short name) are not linked unless configured explicitly.
- Developers can override or add references per entity with the builder: `.Reference(o => o.CustomerId).To<Customer>()`, and can suppress detection with `.Reference(o => o.CustomerId).None()`.
- Following a link switches to the target entity with the filter `Id equals <value>` and opens its detail panel. A "back" breadcrumb returns to the previous entity, filter and row (kept in the URL history).
- If the target type is not discovered, excluded or has no matching row, the value is shown as plain text, or a "not found" message after the click.

### Bulk delete

- Edit mode only. Rows have a checkbox, with a header checkbox that selects loaded rows. Selection is kept across "Load more" and cleared on filter, sort or entity change.
- At most `options.MaxBulkSelection` (default 100) rows can be selected. The header checkbox selects the first loaded rows up to the cap and says so when it stops. The action bar shows the selected count and "Delete selected".
- The server enforces the same cap and rejects larger requests with 400 without deleting anything.
- A single confirmation names the entity type and count. Rows are deleted one by one through `DeleteResultAsync` (or the ActiveEntity overload), so soft-delete, domain events and rules apply per row. There is no wrapping transaction.
- The result is a summary: deleted count and failed rows with `Id` and message. Failed rows stay selected. Deleted rows are removed from the list.
- Soft-deleted rows cannot be selected.

### Change History link

- When the Change History dashboard page is enabled (EF Core change history feature present and `change-history` not disabled), the detail panel shows a "Change history" link for entity types that Change History tracks.
- The link opens the Change History page filtered to the entity type and `Id`. If the type is not tracked or the page is disabled, the link is not rendered.
- The link needs no new coupling: the Entities page asks an optional `IEntitiesDashboardChangeHistoryLinkProvider` (implemented by the Change History dashboard when registered) for a URL, and renders nothing when none is registered.

### Editing UI

- Create, update and clone happen in a modal dialog. The modal pauses any refresh, supports Esc and a text "Cancel", shows inline field errors, and keeps focus inside while open.

## Property Masking

- Masking is opt-in per entity at setup, through a fluent per-entity builder (`options.Entity<T>()` returns an `EntitiesDashboardEntityBuilder<T>`):

```csharp
services.AddEntitiesDashboard(options => options
    .Entity<Customer>(e => e
        .Mask(c => c.Email)                          // shown as ••••••
        .Mask(c => c.Address.Phone, "***")           // custom mask text
        .Hide(c => c.TaxNumber)                      // column omitted
        .Hide("InternalNotes")));                    // by public property name or dotted path
```

- Builder surface: `Mask(expression, maskText = "••••••")`, `Hide(expression)`, `Hide(name)`, and `Exclude()` (same as `options.Exclude<T>()`). Calls are additive, last call per property wins. Expressions and names allow a public top-level property or one level of value-object property (`Address.Phone`). Anything else, including non-public or shadow members, fails at startup with a clear error. Non-public members are never shown anyway.
- `Id` cannot be masked or hidden, and trying fails at startup.
- Property names are validated against the entity type at startup, so a typo fails fast and not silently unmasked.
- Configuration is stored as plain descriptors (property path, mode, mask text) in the options and is read by `EntityPropertyDescriptor` creation.

| Mode | Behavior |
| --- | --- |
| Mask | Value shown as the mask text (default `••••••`). Not revealed in list, detail or edit form. |
| Hide | Column omitted entirely. |

- Masked properties are masked on the server before data is sent to the browser. Hiding is not done on the client.
- Masked properties cannot be used in filters or sorting (this prevents value leakage through queries) and are not included in quick-search.
- On update, a masked property is only changed if the user explicitly chooses "replace value" and enters a new one. An untouched masked field keeps the stored value.
- Masked or hidden value-object members (for example `Address.Phone`): the JSON editor for the value object omits the member. On save, the server merges the submitted JSON with the stored member value before deserializing, so the mask text or a missing member never overwrites real data. A masked member gets its own "replace value" input next to the JSON editor. On create, the member starts at its default unless replaced.
- Masking applies equally in View and Edit mode.
- Without setup, no property is masked. Documentation points out that sensitive properties need explicit configuration.

## Configuration Summary

```csharp
services.AddEntitiesDashboard(options =>
{
    options.EditPolicy = "EntitiesEditor";
    options.MaxBatchSize = 200;
    options.MaxExportRows = 5000;
    options.MaxBulkSelection = 100;
    options.Exclude<AuditLogEntry>();
    options.Entity<Customer>(e => e
        .Mask(c => c.Email)
        .Reference(c => c.RegionId).To<Region>());
});
```

| Option | Default | Description |
| --- | --- | --- |
| `EditPolicy` | null | Authorization policy required for write operations |
| `EditRoles` | empty | Alternative role gate for write operations |
| `BatchSize` | 50 | Rows per "Load more" batch |
| `MaxBatchSize` | 200 | Upper bound for a batch |
| `MaxExportRows` | 5000 | Row cap for JSON and CSV export |
| `MaxBulkSelection` | 100 | Maximum rows in one bulk delete |
| `Exclude<T>()` | – | Opt out one entity type |
| `Include<T>()` | – | Re-include a default-excluded type |
| `Entity<T>(...)` | – | Per entity settings (masking, reference overrides) |

The extension is registered by the presentation web package, and the page is only active if the dashboard itself is enabled.

## Architecture

- **Presentation layer** (new folder `Presentation.Web/Entities/Dashboard`, namespace `BridgingIT.DevKit.Presentation.Web.Entities.Dashboard`):
  - `DashboardPageProvider` (page and card), `DashboardEndpoints` (page, rows fragment, detail, export, create/update/delete/bulk delete actions), `EntitiesDashboardOptions`, Razor slices (`Index`, `Content`, `Data`, `Editor`, `Script`).
- **Abstractions**:
  - `IEntitiesDashboardSource` describes one entity type: name, id type, properties, and operations (find batch, find by id, insert, update, delete) with a repository-backed (result extensions) and an ActiveEntity-backed implementation. Closed generics are invoked through a cached, compiled invoker (like `ChangeHistoryDashboardInvoker`).
  - `EntitiesDashboardDiscovery` builds the source list from the service provider and options.
  - `EntityPropertyDescriptor` carries name, CLR type, editor kind, read-only and masking flags.
- Hosted in the existing `Presentation.Web` package, next to Logging and Profiling, and registered through `AddEntitiesDashboard()` there. It depends only on Domain abstractions (`IGenericRepository`, the ActiveEntity registry) and has no EF Core dependency, so both sources work for any provider.
- Endpoints inherit the dashboard group, authorization, CORS and rate-limit settings from `DashboardEndpointsOptions`.
- Layout rules: `RazorSlice`, shared `_DashboardLayout`, `window.bdkDashboard.createRefresher` (paused while a modal is open), `DashboardPage.RememberedViews` for view tabs (list / detail).

## Security Considerations

- Edit is deny-by-default and enforced server-side on each write.
- Write endpoints require edit access on the server and need no antiforgery token, like the other dashboard POST endpoints (see Access Model). Read endpoints are side-effect free.
- Bulk delete and export caps are enforced on the server, not only in the UI.
- Masked data never leaves the server.
- Property names and operators from the client are validated against the discovered descriptor, never reflected by arbitrary name.
- Batch size, JSON payload size and filter condition count are bounded.
- The page can expose sensitive data in View mode. Documentation recommends the standard dashboard authorization and masking setup.
- Server values are HTML encoded by Razor and client text uses `textContent`.

## Acceptance Criteria

1. When a repository or an ActiveEntity registration exists, then the Entities page appears in the `bdk` group. When none exist, then it does not.
2. When `DisabledPageKeys` contains `entities`, then the page and its endpoints are not available.
3. When the user picks an entity type, then a table with its scalar and value-object properties shows the first batch, with "Load more" when more rows exist.
4. When filters are applied, then the table shows only matching rows and the URL reflects the filter.
5. When a filter references an unknown property or invalid value, then an inline error is shown.
6. Given the principal does not satisfy `EditPolicy`, then no write controls are shown and write requests return 403.
7. Given the principal satisfies `EditPolicy` and edit mode is on, then create, update and delete work, and domain events and validation run.
8. When an update uses a stale `ConcurrencyVersion`, then a conflict is reported and the data is unchanged.
9. When a JSON value is invalid for a value-object property, then the save is rejected with an error message.
10. Given a property is configured as masked, then its value is not present in the HTML or response payload, and it is not filterable or sortable.
11. When a type is excluded through `Exclude<T>()`, then it is not in the selector.
12. Entity types that are available via both sources show once.
13. When "Load more" is clicked, then the next batch is appended and the button disappears once a batch comes back short.
14. Given an entity needs non-public setters or constructors and has not opted in, then it is view and delete only.
15. Given an entity implements `IAuditable`, then rows default to the latest audit action first.
16. When a write fails with an expected error, then the modal shows inline messages; an unexpected error shows a correlation id.
17. When a filter is applied, then it appears in the entity's recent list. When it is saved with a name, then it can be re-applied after a reload, and filters referencing unavailable properties are disabled.
18. When columns are hidden or reordered, then the choice persists per entity and "Reset columns" restores the defaults. `Id` stays visible.
19. When "Export JSON" or "Export CSV" is used, then the file contains the filtered rows up to `MaxExportRows`, with masked and hidden properties absent and CSV formula characters escaped.
20. When "Clone" is used in edit mode, then the create modal is prefilled without `Id`, concurrency version and audit state, and masked values start empty.
21. When a URL with entity, filter, sort and selected `Id` is opened, then the same state and detail panel are restored.
22. When an update changes properties, then a before/after summary is confirmed before saving, with no masked values shown, and an unchanged form is not saved.
23. When a property is a reference to a discovered entity, then it renders as a link that opens the target row, and "back" returns to the previous view.
24. When rows are selected and "Delete selected" is confirmed, then each row is deleted individually and the result lists failures. Selection above `MaxBulkSelection` is blocked and soft-deleted rows cannot be selected.
25. Given the Change History page is enabled and tracks the entity type, then the detail panel links to it filtered by type and `Id`. Otherwise no link is rendered.
26. When a write request is sent without an antiforgery token, then it succeeds for an editor and returns 403 for a non-editor; no token handling or `IAntiforgery` registration is required.
27. Given a value-object member is masked, when the value object is updated through the JSON editor without replacing the member, then the stored member value is unchanged.

## Testing

- **Unit**: discovery (sources, opt-out, deny list, duplicates), `ActiveEntityRegistry` filling (both builder paths, several `AddActiveEntity` calls), property descriptor and editor mapping, masking builder validation (invalid paths, `Id`), masked value-object merge, `FilterModel` building and validation, default audit ordering, reference detection and overrides, CSV escaping, option defaults.
- **Presentation (TestServer)**: page enabled/disabled, card states, group path, view versus edit authorization (403, no token needed), CRUD and clone round trip with an in-memory ActiveEntity provider and a fake repository, concurrency conflict, masked payload check (HTML, fragments, copy, export), export cap and truncation flag, bulk delete (partial failures, server-side cap), Change History link present or absent.
- **Browser** (when requested): filter, reset, load more, saved and recent filters, column picker persistence, deep link restore, related entity navigation and back, diff confirmation, modal editing, delete and bulk delete confirmation, refresh pause, light/dark/narrow layouts.

## Documentation

- New feature guide `docs/features-presentation-dashboard-entities.md` covering setup, enablement and disabling, access model, masking and reference builders, the reflection opt-in and its risks, export, and the security recommendations. It uses fictional models only.
- `docs/features-presentation-dashboard.md` lists the new page and links to the guide.
- Public APIs (`AddEntitiesDashboard`, options, builders, `ActiveEntityRegistry`) get XML documentation with usage examples.

## Decisions on Former Open Items

1. **Enumeration and Guid editors.**
   - `EnumerationValueObject<T, TKey>` and `EnumerationValueObject<T>` properties use a select built from the type's static `All` collection, showing the name and storing the key (`Create(key)` or `FromKey(key)` on save). Nullable ones add an empty option.
   - `Guid` properties and ids use a text input validated with `Guid.TryParse`, plus a "New Guid" button in the create modal.
   - Enumerations used as ids or filter values follow the same rule (select in filter, key as value).
2. **Icon.** The page uses the Bootstrap icon `table`, which no other dashboard page uses. This replaces the earlier `database` fallback, because `database-fill` already belongs to the Blobs page. The existing canonical feature SVG `docs/assets/features/entities.svg` (catalog id `entities`) is embedded and mapped to the page key per the icon README; the Bootstrap icon is the fallback.
3. **Latest audit action ordering.** Approximated: `UpdatedDate` descending with `CreatedDate` as the fallback, then `Id`. `AuditState` sets `UpdatedDate` on create, deactivate and delete, so this matches the newest of the four dates.
4. **Soft-deleted rows.** Not specially fetched, see the Delete section. Deleted rows that the registered stack returns are shown read-only with a badge.
5. **ActiveEntity registry lifetime and masking builder.** Resolved in Entity Discovery and Property Masking.
