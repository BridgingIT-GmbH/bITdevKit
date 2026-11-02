<img class="bdk-doc-icon" src="assets/features/entities.svg" width="32" height="32" alt="" align="right">

# Entities Dashboard

> Browse, search and maintain persisted entities from the dashboard, like a rough table editor for repositories and ActiveEntity types.

[TOC]

## Overview

The Entities page lists every `IEntity` type reachable through a registered `IGenericRepository<T>` or an ActiveEntity configuration. Pick an entity type, narrow the list with filters and inspect or edit single rows. Searching uses `FilterModel`; reads and writes run through the normal repository or ActiveEntity pipeline, so behaviors such as validation, auditing and domain events apply as for any other write.

The page is read-only by default. Write operations need an explicitly configured authorization policy or role.

## Challenges

Checking or fixing a single record during development or operations usually means an ad-hoc SQL query or a throw-away tool. Both bypass the domain model, skip validation and auditing and cannot be shared with the team. Sensitive columns then end up in screenshots, chat messages and query history.

## Solution

The Entities page reuses the registrations the application already has. It discovers the entity types automatically, builds filters from the entity's properties and saves changes through the same pipeline that production code uses. Property masking keeps sensitive values on the server. Edit access is deny-by-default and enforced on every write request.

## Key Features

- **Automatic discovery.** Repository and ActiveEntity types appear without extra registration; types registered through both sources show once.
- **Search.** A filter toolbar builds a `FilterModel` (property, operator, value, AND/OR) plus a quick search over string properties. Filters, sort and the selected row live in the URL.
- **Load more.** Rows load in batches (default 50). The footer shows a "Load more" button, which is disabled once the list is complete. The "Rows per load" selector changes the batch size (10 to `MaxBatchSize`) and is remembered per browser. Narrow the list instead of paging through whole tables.
- **View and edit modes.** Everyone with dashboard access can view. Editors toggle edit mode (the pencil icon button at the top right, highlighted while active) to reveal the New and Delete selected icon buttons next to it and to create, clone, update, delete and bulk delete rows in a modal dialog. The view dialog has "Copy JSON" at the bottom left, next to "Close".
- **Safe updates.** Only changed properties are applied and `IConcurrency` entities are protected against stale updates. The edit dialog has a plain OK and Cancel.
- **Property masking.** Mask or hide sensitive properties per entity. Masked values never leave the server.
- **Related entities.** Properties that reference another discovered entity render as links. Ids and reference links open the record in a modal dialog (edit dialog in edit mode, view dialog otherwise), so the list stays as it is. Following references inside the view dialog adds a "Back" button.
- **Productivity.** Saved and recent filters, a column picker, copy as JSON, JSON and CSV export and deep links.
- **Change History link.** The detail panel links to the Change History page for tracked entities.

## Enablement and disabling

The page is part of the dashboard (see [Presentation Dashboard](features-presentation-dashboard.md)) and needs no extra registration. It is listed when at least one entity source exists: an `IGenericRepository<T>` for an `IEntity` type or an ActiveEntity configuration. Without a source the page and its overview card are not shown.

Disable it like any other dashboard page by its key `entities`:

```csharp
builder.Services.AddDashboard(options => options
    .DisablePages("entities"));
```

A disabled page is not mapped and its endpoints return 404.

## Configuration

Call `AddEntitiesDashboard` only when you need to change the defaults.

```csharp
builder.Services.AddEntitiesDashboard(options =>
{
    options.EditPolicy = "EntitiesEditors";
    options.BatchSize = 50;
    options.MaxBatchSize = 200;
    options.MaxExportRows = 5000;
    options.MaxBulkSelection = 100;
    options.Exclude<Region>();
    options.Entity<Customer>(e => e
        .Mask(c => c.Email)
        .Hide(c => c.TaxNumber)
        .Reference(c => c.PreferredRegionId).To<Region>());
});
```

| Option | Default | Description |
| --- | --- | --- |
| `EditPolicy` | null | Authorization policy required for write operations |
| `EditRoles` | empty | Alternative role gate for write operations |
| `BatchSize` | 50 | Default rows per "Load more" batch (also selectable in the footer) |
| `MaxBatchSize` | 200 | Upper bound for a batch and the largest selectable size |
| `MaxExportRows` | 5000 | Row cap for JSON and CSV export |
| `MaxBulkSelection` | 100 | Maximum rows in one bulk delete |
| `Exclude<T>()` | – | Opt one entity type out of the page |
| `Include<T>()` | – | Re-include a type that DevKit excludes by default |
| `Entity<T>(...)` | – | Per entity settings: masking, hiding, references, non-public setters |

DevKit infrastructure entities (for example change history entries or outbox messages) are excluded by default through a deny list. Use `Include<T>()` to bring one back. The configuration is validated at startup, so a mistyped property name fails fast.

## Access model

| Mode | Requirement | Capabilities |
| --- | --- | --- |
| View | Normal dashboard access | List, search, details, copy, export, deep links, related entities, Change History link |
| Edit | `EditPolicy` (or `EditRoles`) is satisfied | View plus create, clone, update, delete and bulk delete |

- If neither `EditPolicy` nor `EditRoles` is set, the page stays read-only.
- Edit mode is a toggle that starts in View, even for editors.
- The server checks the edit permission on every write request and answers 403 when it is missing. Hiding the controls is never the only protection.
- The write endpoints follow the other dashboard POST endpoints: JSON bodies and no antiforgery token. This is a backend admin surface, so protect it with the dashboard authorization of the host. See [Authentication and authorization](features-presentation-dashboard.md#authentication-and-authorization).

## Searching

The filter toolbar offers the top-level scalar properties of the selected entity plus one level of value-object properties as dotted paths (for example `Address.City`). The operators depend on the property type. Several conditions combine with AND, or with OR through the match toggle. Quick search applies `contains` across the top-level string properties (not value-object members, which stores with value converters cannot translate).

Rows are ordered by the latest audit action first when the entity implements `IAuditable`, otherwise by `Id`. The list always ends the ordering with `Id`, so "Load more" stays stable. Click a column header to sort by a top-level scalar column.

Unknown properties, unsupported operators and invalid values show an inline error. Masked and hidden properties cannot be filtered or sorted.

## Editing

### Create, update and delete

- **Create** opens a modal with an input per editable property. An empty `Guid` id is generated.
- **Update** is pre-filled from the current entity and applies only the changed properties. OK saves right away and Cancel discards the edits. Nothing is written when nothing changed.
- **Delete** asks for a confirmation and uses the repository or ActiveEntity delete, so soft-delete behaves as configured. The page never performs a hard delete of its own.
- **Clone** opens the create modal prefilled from a row, without id, concurrency version and audit state.
- **Bulk delete** removes the selected rows one by one (up to `MaxBulkSelection`) and reports the deleted count and the failed rows. There is no wrapping transaction.

### Concurrency

For entities that implement `IConcurrency`, the version shown in the editor is submitted with the update. A stale version is reported as a conflict with a reload action and nothing is written. Entities without `IConcurrency` use last-write-wins, and the editor says so.

### Property editors

| Property kind | Editor |
| --- | --- |
| string, number | Text or number input with parse validation |
| Guid | Text input; "New Guid" on create |
| bool | Checkbox |
| DateTime, DateTimeOffset | UTC date-time input |
| enum, enumeration value object | Select |
| Nullable | Input plus a null toggle |
| Value object, complex, scalar collections | JSON textarea, validated before save |
| Navigation graphs, byte arrays | Read-only |

Malformed JSON is rejected with an error before it reaches the repository. Domain rule violations and validation failures appear as inline messages in the modal. A database rejection (for example a value that is too long for its column) shows the first line of the database message, attached to the offending property when the column is named. Unexpected errors show a generic message. Failures with a correlation id show it as a link to the matching log entries; stack traces are never shown.

### Non-public setters

By default the page sets properties with public setters and builds new entities through a public parameterless constructor. Properties without a public setter are not shown in the editor (they stay visible in the list and view dialog), and entities without a public constructor are view and delete only. You can opt in per entity:

```csharp
options.Entity<LockedProduct>(e => e.AllowNonPublicSetters());
```

> Reflection writes bypass domain invariants in setters and factory methods. Use the opt-in only for types where that is acceptable.

Repository or ActiveEntity behaviors still run on the save call.

## Property masking

Without configuration nothing is masked. Mark sensitive properties explicitly:

```csharp
builder.Services.AddEntitiesDashboard(options => options
    .Entity<Customer>(e => e
        .Mask(c => c.Email)                  // shown as ••••••
        .Mask(c => c.Address.Phone, "***")   // custom mask text
        .Hide(c => c.TaxNumber)              // column omitted
        .Hide("InternalNotes")));            // by name or dotted path
```

| Mode | Behavior |
| --- | --- |
| Mask | The value is replaced by the mask text in the list, details and editor |
| Hide | The property is omitted entirely |

- Masking happens on the server before data is sent to the browser. It also applies to JSON and CSV export and to "Copy as JSON".
- Masked properties are excluded from filters, sorting and quick search.
- On update a masked property only changes when you choose "replace value" and enter a new one. For masked members of a value object the server merges the submitted JSON with the stored member, so the mask text never overwrites real data.
- `Id` cannot be masked or hidden.
- Hidden properties keep their default value on create. If validation requires them, creating fails; do not hide required properties of entities that must be creatable.

## Related entities

A top-level property is shown as a link when it references another discovered entity: either its type is the target's typed id (for example `CustomerId`), or it is named `<Entity>Id` and matches the target's id type. Following a link opens the referenced record in a modal dialog (an edit dialog in edit mode, a view dialog otherwise) and leaves the list untouched. Links inside the view dialog stack, and "Back" returns to the previous record. Override or suppress the detection per entity:

```csharp
options.Entity<Order>(e => e
    .Reference(o => o.BillingCustomerId).To<Customer>()
    .Reference(o => o.ExternalNumber).None());
```

## Copy and export

"Copy as JSON" copies the row as shown on the page. "Export JSON" and "Export CSV" download the rows matching the current filter and sort, up to `MaxExportRows`. The UI says so when the file is truncated. CSV uses UTF-8 with the invariant culture, writes value objects as JSON in one cell and prefixes cells that start with `=`, `+`, `-` or `@` to prevent spreadsheet formula injection. Export is a read operation and needs no edit permission. Each export is logged with the entity and row count, never the values.

## Saved filters and columns

The page keeps the last 10 applied filters per entity and lets you save named filters. Column visibility and order are also stored per entity. The page also remembers the last selected entity, source, filter and sort, and restores them when you open the dashboard without query parameters. All of this is stored in the browser's local storage and never sent to the server. Saved filters that reference a property that is no longer available are shown disabled.

## Change History link

When [Change History](features-domain-change-history.md) tracks the entity type and the `change-history` dashboard page is enabled, the detail panel shows a "Change history" link that opens that page for the entity and id. The link is not shown when the type is not tracked or the page is disabled.

## Security recommendations

- Protect the dashboard with the standard dashboard authorization. The page can expose sensitive data in View mode.
- Mask or hide sensitive properties such as personal data, secrets and tokens.
- Grant `EditPolicy` or `EditRoles` only to trusted operators.
- Keep `MaxExportRows` and `MaxBulkSelection` as low as your workflow allows.
- Every write is logged with `[EntitiesDashboard]` (entity type, id, operation, user). Property values are never logged, and the writes also appear in the Auditing dashboard when it is enabled.

## Limitations

- Entities with several repositories for the same type use the registration that the container resolves; tenant or DbContext switching is not part of the page.
- There is no total row count. The list shows the loaded count, and "Load more" stays enabled while batches come back full.
- Child collections and navigation graphs can only be edited as the JSON of a property, if at all.
- Import and schema changes are out of scope.
