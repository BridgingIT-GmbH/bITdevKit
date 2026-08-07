---
status: implemented
---

# Design Specification: ChangeHistory Feature

> This design document defines the ChangeHistory feature for recording entity property changes, grouping properties changed together, and restoring previous values. The feature is based on the existing `src/Domain/Model/EntityChange` fluent change builder and follows the decision-oriented documentation style from the supplied ADR README: context, decision, rationale, consequences, alternatives, and implementation notes.

[TOC]

## 1. Context

bITdevKit already has several related capabilities:

- `EntityChange` in `src/Domain/Model/EntityChange` tracks old values while a fluent `.Change().Set(...).Apply()` transaction is executed.
- `EntityChangeContext` exposes previous values to custom domain event factories.
- `AuditState` and `RepositoryAuditStateBehavior` track who and when an entity was created, updated, deactivated, or deleted.
- Repository decorator behaviors add persistence concerns without changing application handlers.
- EF Core infrastructure owns database tables, mappings, and migrations.
- Current-user accessors, activity correlation, module context, result types, filtering, specifications, and serializers are already available across the kit.

The missing capability is durable, queryable property-level history:

- Which property changed?
- What was its old value?
- What is its new value?
- Which properties changed together in the same entity change transaction?
- Who changed it and under which request/correlation context?
- Can the entity be restored to the state before one property set changed?

## 2. Problem

`AuditState` answers "this entity was updated", but not "which values changed". Domain events can describe business events, but they are not a generic restoreable property history. Event sourcing provides full replayable history, but adopting event sourcing for every regular aggregate is too heavy.

ChangeHistory should fill the gap for regular entities and aggregates by recording property-level history in a separate database table while keeping the Domain and Application layers persistence-ignorant.

## 3. Goals

- Record old and new values for changed properties.
- Persist all properties changed by one `.Change().Apply()` call under one stable change set id.
- Store history in a separate EF-managed database table.
- Reuse existing bITdevKit features instead of introducing a parallel infrastructure stack.
- Capture direct property mutation on untracked entities when they are persisted through repository updates.
- Capture repository `UpdateSetAsync` bulk updates with before/after values for each affected entity.
- Support optional insert/create initial-value capture per entity.
- Support querying history by entity type, entity id, property name, change set id, user, and time range.
- Support restoring an entity to the values it had before a selected change set.
- Guarantee that restore can execute module domain logic through domain methods or typed domain restore handlers.
- Support restoring identifiable collections, owned value objects, and complex object graphs when identity and ownership can be inferred or declared.
- Record restore operations as new history rows, never mutate or delete previous history.

## 4. Non-Goals

- This is not a replacement for Event Sourcing. Event Sourcing remains the choice when event streams are the aggregate source of truth.
- The feature does not make direct property mutation the preferred domain style. Domain methods and `EntityChange` remain the clearest way to express business changes.
- The feature does not bypass domain invariants during restore. Restore must guarantee a domain-logic restore path through configured domain restore handlers or domain method mappings, with metadata/setter-based restore available only as an explicitly configured fallback.
- The feature does not promise provider-neutral bulk capture for stores that cannot read affected values before and after a set-based update. EF Core support is required for the first version.
- The feature should not expose domain entities directly through presentation models.

## 5. Decision Summary

Implement ChangeHistory as a cross-cutting feature with four parts:

1. Extend `EntityChange` to produce an in-memory `EntityChangeSet` when `.Apply()` succeeds.
2. Add configurable capture strategies for direct mutation: `EntityChangeOnly`, `RepositorySnapshot`, and `EfChangeTracker`.
3. Add an EF repository behavior that persists pending/detected change sets into a separate `__ChangeHistory` table in the same persistence flow as the entity update.
4. Add application-level query and restore services/commands that use repositories, specifications/filtering, result types, and explicit restore policies.

The preferred capture point is the existing `EntityChange` builder because it preserves the exact domain method boundary. Direct-mutation capture must be explicitly configurable. For this repository, the recommended default is `RepositorySnapshot` because entities are normally untracked; `EfChangeTracker` is available only for modules that deliberately work with tracked entities; `EntityChangeOnly` records only `EntityChangeBuilder` changes and ignores direct mutation.

## 6. bITdevKit Features To Reuse

| Feature | Reuse in ChangeHistory |
| --- | --- |
| `EntityChangeBuilder<TEntity>` | Primary capture point for property old/new values and change set grouping. |
| `EntityChangeContext` | Extend to expose old and new values plus a read-only change list to event factories. |
| `Result` / `Result<T>` | Return explicit success/failure for restore and query operations. |
| `AuditState` | Continue to track entity-level audit metadata; ChangeHistory complements it. |
| `ICurrentUserAccessor` | Capture changed-by user id, user name, and email. |
| Repository behaviors | Persist history through a `RepositoryChangeHistoryBehavior<TEntity, TContext>`. |
| Capture strategy options | Select whether direct mutation is captured through `EntityChangeOnly`, `RepositorySnapshot`, or `EfChangeTracker`. |
| Repository snapshot comparison | Detect direct property mutation on untracked entities by comparing the submitted entity with a persisted baseline loaded before update. |
| EF Core ChangeTracker | Optional opt-in direct-mutation detector for modules that keep tracked entities and want original/current value comparison. |
| Repository `UpdateSetAsync` | Capture set-based changes by snapshotting affected rows before and after the bulk update. |
| EF Core infrastructure | Own `__ChangeHistory` mapping, indexes, migration support, and DbContext contract. |
| Domain events / outbox | Optional downstream notifications after history has been stored; not the source of truth for the history table. |
| Common Serialization | Serialize old/new values with `SystemTextJsonSerializer` and default devkit JSON conventions. |
| Typed entity IDs | Store entity ids as stable strings while preserving entity id CLR type metadata. |
| Specifications / Filtering | Query history with specifications and filter models. |
| Requester / Commands / Queries | Provide restore and history search workflows through application request handlers. |
| Correlation / Activity baggage | Persist correlation id, flow id, module name, and activity parent id when available. |
| Mapster / DTO exposure | Map history rows to safe DTOs for presentation endpoints. |

## 7. Requirements

### Story 1: Capture Entity Change Sets

- Status: Implemented
- Ready: Yes
- Ready Reason: Existing `EntityChange` code already has the correct execution boundary; only change record shape needs to be extended.
- User Story: As a developer, I want every successful `EntityChange` transaction to produce a change set, so that property history reflects domain-approved changes.

Acceptance Criteria:

1. Given an entity changes two properties in one `.Change().Apply()` call, when the apply result is successful, then one change set is created with two property changes.
2. Given a property is assigned the same value it already has, when `.Apply()` runs, then no history row is created for that property.
3. Given the same property changes multiple times inside one change set, when history is captured, then the old value is the value before the first change and the new value is the final value.
4. Given `.Apply()` fails, when the entity is later not persisted, then no change history is stored.
5. Given a `When` guard cancels later operations, when earlier operations changed properties, then only the earlier changed properties are captured.

Data Requirements:

- `ChangeSetId`: `Guid`, created once per successful `.Apply()` call.
- `EntityType`: short CLR type name.
- `EntityClrType`: stable CLR type token for diagnostics.
- `EntityId`: string representation of `IEntity.Id`.
- `PropertyName`: property name or property path.
- `OldValue`: original property value before this change set.
- `NewValue`: final property value after this change set.
- `Sequence`: integer preserving property capture order within the change set.

Notes:

- `EntityChangeOrderedExecutionContext` currently stores only old values and overwrites repeat changes. It must preserve the first old value and update the final new value.
- `Register((entity, ctx) => ...)` currently receives `null` for new values. `EntityChangeContext` should expose new values.
- `Execute(...)` operations currently mark `"Execute"` as changed without property details. They should not create property history unless a future explicit tracking API is added.

### Story 2: Capture Direct Property Mutation

- Status: Implemented
- Ready: Yes
- Ready Reason: Direct-mutation capture source is explicit and configurable, with repository snapshots for untracked entities and ChangeTracker only for tracked modules.
- User Story: As a developer, I want to choose how direct mutation is captured when entities are saved, so that ChangeHistory matches the module's persistence style.

Acceptance Criteria:

1. Given `EntityChangeOnly` is configured, when an entity is directly mutated outside `.Change().Apply()`, then no direct-mutation history rows are stored.
2. Given `RepositorySnapshot` is configured, when an untracked entity property is changed directly and saved through `repository.UpdateAsync(entity)` or update-style `repository.UpsertAsync(entity)`, then the behavior loads the persisted baseline and stores old/new values for the changed property.
3. Given `EfChangeTracker` is configured, when a tracked entity property is changed directly and saved, then the behavior uses EF original/current values and stores old/new values for the changed property.
4. Given the configured strategy sees an unchanged property, when capture runs, then no row is stored for that property.
5. Given an entity also has pending `EntityChange` change sets, when the configured direct-mutation strategy runs, then duplicate rows are not written for properties already captured by `EntityChange`.
6. Given the configured capture strategy cannot resolve old values safely, when direct mutation capture is required by options, then the save fails before update with a `ChangeHistoryCaptureError`; otherwise it logs a warning and skips only that capture source.

Data Requirements:

- `CaptureStrategy`: `EntityChangeOnly`, `RepositorySnapshot`, or `EfChangeTracker`.
- `CaptureSource`: `EntityChange`, `RepositorySnapshot`, `EfChangeTracker`, `Create`, `UpdateSet`, or `Restore`.
- `CaptureStatus`: `Captured`, `Skipped`, or `Failed`.
- `CaptureMessage`: optional diagnostics for skipped/failed captures.

Notes:

- `RepositorySnapshot` should be the recommended default in bITdevKit modules that work with untracked entities.
- `RepositorySnapshot` loads the baseline using no-tracking queries by id before calling the inner repository update.
- `RepositorySnapshot` creates pending history rows before calling the inner repository update so the entity update and history rows are saved in the same EF transaction.
- `EfChangeTracker` is opt-in and should only be used by modules that intentionally keep entities tracked through the update.
- Baseline loading requires configured or inferred include paths for owned values, collections, and graph paths that should be compared or restored.
- Direct-mutation capture is about completeness, not encouraging direct mutation in domain code.

### Story 3: Capture Repository Bulk Updates

- Status: Implemented
- Ready: Yes
- Ready Reason: `UpdateSetAsync` already exposes the intended assignments; EF providers can snapshot affected rows before and after the set-based update.
- User Story: As an operator, I want bulk updates recorded with per-entity old and new values, so that administrative or background changes are auditable and restoreable.

Acceptance Criteria:

1. Given `UpdateSetAsync` updates multiple entities, when change history is enabled, then each affected entity gets its own change set.
2. Given one `UpdateSetAsync` call changes multiple properties on one entity, when rows are stored, then those property rows share the same `ChangeSetId` and `BulkOperationId`.
3. Given a bulk assignment sets a property to the same value, when history is stored, then no row is written for unchanged values.
4. Given a computed bulk assignment is used, when the provider cannot evaluate the new value before update, then it snapshots affected rows after update and compares them with the pre-update snapshot.
5. Given the affected row count exceeds the configured safety limit, when bulk history is required, then the operation fails before update with a validation error; when best-effort mode is configured, then it records a summary row and logs a warning.

Data Requirements:

- `BulkOperationId`: `Guid`, shared by all entity change sets created from one `UpdateSetAsync` call.
- `AffectedEntityCount`: stored in metadata for the bulk operation.
- `CaptureSource`: `UpdateSet`.

Notes:

- Bulk capture trades performance for auditability. Options must expose a max affected row limit and a mode: `Required`, `BestEffort`, or `Disabled`.
- The recommended default max affected row limit is `1000`. Larger operations must override the limit explicitly per entity or operation.
- The implementation should reuse specifications/filtering passed to `UpdateSetAsync` to snapshot the same affected entity set.
- `DeleteSetAsync` support is not required by this ChangeHistory spec unless delete/restore requirements are added later.

### Story 3a: Capture Entity Creation

- Status: Implemented
- Ready: Yes
- Ready Reason: Creation capture can reuse the same table and change set grouping without changing restore semantics.
- User Story: As a support user, I want to see the initial values an entity was created with, so that the full value history starts at creation.

Acceptance Criteria:

1. Given create capture is enabled for an entity, when the entity is inserted, then one change set is created with `Operation = Create`.
2. Given an included scalar property has an initial value, when create history is stored, then `OldValue` is null and `NewValue` contains the serialized initial value.
3. Given included owned values or identifiable collections exist at creation, when create history is stored, then rows are stored with the same path and identity rules used for updates.
4. Given create capture is not enabled for an entity, when the entity is inserted, then no create history rows are stored.
5. Given a create row is queried, when restore eligibility is evaluated, then it is not restoreable by default because restoring "before create" would require delete/deactivate semantics.

Data Requirements:

- `Operation`: `Create`.
- `CaptureSource`: `Create`.
- `ChangeSetId`: shared by all values recorded from the same insert.
- `OldValue`: null for initial value rows.
- `NewValue`: serialized initial property value.
- `IsRestoreable`: false by default for create rows unless a module explicitly defines delete/deactivate restore behavior.

Notes:

- Create capture is useful as an initial snapshot, not as a rollback mechanism.
- Create capture should be explicit opt-in per entity, aligned with direct mutation and bulk update capture.
- If a module wants "undo create", it should configure a separate domain operation such as deactivate/delete and document its invariants.

### Story 4: Persist History In A Separate Table

- Status: Implemented
- Ready: Yes
- Ready Reason: EF Core repository behavior and outbox table patterns provide a clear implementation model.
- User Story: As an operator, I want property changes saved in a separate table, so that entity history can be inspected without changing domain tables.

Acceptance Criteria:

1. Given a repository update persists an entity with pending change sets, when `RepositoryChangeHistoryBehavior` is registered, then rows are inserted into `__ChangeHistory`.
2. Given multiple properties changed together, when rows are persisted, then all rows share the same `ChangeSetId`.
3. Given no pending change set exists, when the entity is saved, then no history row is inserted.
4. Given the entity update fails, when EF Core does not commit the entity change, then history rows are not committed.
5. Given a module does not register the behavior, when an entity is saved, then entity persistence works without history.

Data Requirements:

Recommended MVP table: `__ChangeHistory`, one row per changed property.

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `uniqueidentifier` | Primary key. |
| `ChangeSetId` | `uniqueidentifier` | Groups properties changed together. Indexed. |
| `ChangeSetSequence` | `int` | Property order inside the change set. |
| `EntityType` | `nvarchar(512)` | Short entity type name. Indexed with `EntityId`. |
| `EntityClrType` | `nvarchar(2048)` | Runtime type token for diagnostics. |
| `EntityId` | `nvarchar(256)` | String form of `IEntity.Id`. Indexed. |
| `EntityIdType` | `nvarchar(512)` | Typed id CLR type or primitive id type. |
| `PropertyName` | `nvarchar(512)` | Property name/path. Indexed. |
| `PropertyPath` | `nvarchar(1024)` | Full path for owned values or graph members; null for simple properties. |
| `PathKind` | `nvarchar(64)` | `Scalar`, `Owned`, `Collection`, or `Graph`. |
| `CollectionAction` | `nvarchar(64)` | `Added`, `Removed`, `Replaced`, `Cleared`; null for non-collection rows. |
| `CollectionItemId` | `nvarchar(512)` | Stable item identity for collection/graph rows. |
| `ValueClrType` | `nvarchar(2048)` | CLR type of the changed value. |
| `OldValue` | `nvarchar(max)` | Serialized JSON; null allowed. |
| `NewValue` | `nvarchar(max)` | Serialized JSON; null allowed. |
| `OldValueHash` | `nvarchar(64)` | Optional diagnostics/integrity hash. |
| `NewValueHash` | `nvarchar(64)` | Optional diagnostics/integrity hash. |
| `Operation` | `nvarchar(64)` | `Create`, `Update`, `Restore`, `BulkUpdate`, `CollectionChanged`, `GraphChanged`, etc. |
| `CaptureStrategy` | `nvarchar(64)` | `EntityChangeOnly`, `RepositorySnapshot`, or `EfChangeTracker`. |
| `CaptureSource` | `nvarchar(64)` | `EntityChange`, `RepositorySnapshot`, `EfChangeTracker`, `Create`, `UpdateSet`, `Restore`. |
| `CaptureStatus` | `nvarchar(64)` | `Captured`, `Skipped`, `Failed`, or `Summary`. |
| `CaptureMessage` | `nvarchar(4000)` | Optional diagnostics for skipped/failed/summary rows. |
| `BulkOperationId` | `uniqueidentifier` | Shared by rows created from one bulk update; null otherwise. |
| `AffectedEntityCount` | `int` | Bulk operation metadata; null for normal changes. |
| `IsRestoreable` | `bit` | Whether this row can participate in restore. |
| `RestorePlanName` | `nvarchar(256)` | Restore plan used or required for this row. |
| `RestoreExecutionMode` | `nvarchar(64)` | `DomainLogic`, `RestorePlan`, or `ValidatedSetter`. |
| `DomainRestoreHandlerName` | `nvarchar(256)` | Domain restore handler or method mapping used; null when not applicable. |
| `ChangedByUserId` | `nvarchar(256)` | From `ICurrentUserAccessor`. |
| `ChangedByUserName` | `nvarchar(256)` | From `ICurrentUserAccessor`. |
| `ChangedByEmail` | `nvarchar(512)` | From `ICurrentUserAccessor`. |
| `ChangedDate` | `datetimeoffset` | UTC timestamp. Indexed. |
| `Reason` | `nvarchar(1024)` | Optional change reason. |
| `CorrelationId` | `nvarchar(256)` | From activity baggage when available. |
| `FlowId` | `nvarchar(256)` | From activity baggage when available. |
| `ModuleName` | `nvarchar(256)` | From module context when available. |
| `Properties` | `nvarchar(max)` | Optional JSON metadata bag. |

Notes:

- The table name should be configurable, defaulting to `__ChangeHistory`.
- One table with `ChangeSetId` is the default physical model. The logical model should still separate change set metadata from property rows in code so high-volume modules can later use a normalized storage implementation without changing application APIs.
- Once an entity/path is opted in, values are included by default. Sensitive properties must be configurable for redaction or exclusion.

### Story 5: Query Change History

- Status: Implemented
- Ready: Yes
- Ready Reason: Existing repositories, specifications, filtering, and paged results already cover the query shape.
- User Story: As a support user, I want to query history for an entity, so that I can understand how values changed over time.

Acceptance Criteria:

1. Given an entity type and id, when history is requested, then matching rows are returned ordered by `ChangedDate` descending and grouped by `ChangeSetId`.
2. Given a property filter, when history is requested, then only rows for that property are returned.
3. Given a date range, when history is requested, then only rows in that range are returned.
4. Given many rows exist, when history is requested, then paging is applied.
5. Given the caller lacks access, when history is requested through presentation endpoints, then the request is denied.

Data Requirements:

- `ChangeHistoryFindAllQuery` or equivalent query model.
- Filter fields: `EntityType`, `EntityId`, `PropertyName`, `ChangeSetId`, `BulkOperationId`, `ChangedByUserId`, `ChangedDateFrom`, `ChangedDateTo`, `Operation`, `CaptureSource`.
- DTOs must not expose internal CLR type tokens unless explicitly requested by developer diagnostics.

Notes:

- Prefer specifications and `FindOptions`/filter model integration over ad-hoc query strings.
- Presentation should return DTOs, not the EF persistence entity.

### Story 6: Restore A Previous Change Set

- Status: Implemented
- Ready: Yes
- Ready Reason: Restore scope is explicit: scalar properties, owned value objects, identifiable collections, and graph paths are supported through domain restore handlers, domain method mappings, restore plans, or explicitly allowed validated setter/metadata fallback and validated before persistence.
- User Story: As an authorized user, I want to restore an entity to the values it had before a selected change set, so that accidental changes can be reverted safely.

Acceptance Criteria:

1. Given a change set contains two property changes, when restore succeeds, then both properties are restored to their old values as one new entity change transaction.
2. Given the entity no longer exists, when restore is requested, then a `NotFoundError` result is returned.
3. Given the change set contains an owned value object, when restore succeeds, then the owned value object is restored as part of the same restore transaction.
4. Given the change set contains identifiable collection additions/removals, when restore succeeds, then the collection membership returns to the pre-change state.
5. Given the change set contains a complex graph path with inferable EF metadata or a registered restore plan, when restore succeeds, then the graph path is restored.
6. Given a restorable entity or path is configured with `RestoreExecutionMode = DomainLogic`, when restore is requested, then the restore command invokes the configured domain method or typed domain restore handler instead of directly setting the value.
7. Given `RestoreExecutionMode = DomainLogic` is configured without a domain method mapping or typed domain restore handler, when configuration is validated, then the feature fails validation before restore can be used.
8. Given configured domain restore logic returns a failure result, when restore is requested, then no entity mutation or history row is persisted and the failure is returned.
9. Given a property or graph path cannot be restored safely, when restore is requested, then the command fails with a validation error and no partial restore is persisted.
10. Given restore succeeds, when history is stored, then a new change set is written with `Operation = Restore` and metadata pointing to the restored `ChangeSetId`.
11. Given the current entity has changed since the selected change set, when restore is requested, then the handler validates the expected concurrency/version policy before applying old values.
12. Given the optional restore endpoint is enabled for an entity, when an authorized caller posts a restore request for that entity id and change set id, then the endpoint invokes `ChangeHistoryRestoreCommand`.

Data Requirements:

- `ChangeHistoryRestoreCommand`: `EntityType`, `EntityId`, `ChangeSetId`, optional `Reason`, optional expected concurrency version.
- `RestoredFromChangeSetId`: metadata entry on the new restore history rows.
- Restore policy configuration:
  - Allowed entity types.
  - Allowed properties.
  - Allowed owned value-object paths.
  - Allowed collection paths and item identity selectors.
  - Allowed complex graph paths and include paths.
  - Domain restore handlers or domain method mappings.
  - Per-property conversion/restoration method.
  - Optional read and restore authorization policy names.
  - Optional built-in presentation endpoint registration per entity.
- `RestoreExecutionMode`: `DomainLogic`, `RestorePlan`, or `ValidatedSetter`.
- `DomainRestoreHandlerName`: optional name/key of the domain restore handler used for diagnostics.

Notes:

- Restore must not bypass domain invariants by default.
- The framework must provide a first-class way to register domain restore logic per entity, property path, collection path, and graph path.
- Every restorable path must declare or inherit a restore execution mode; `DomainLogic` mode must require a domain method mapping or typed domain restore handler.
- `DomainLogic` mode may be implemented as a typed handler or a mapping to an existing domain method. Restore plan delegates may call domain methods, but they are not a substitute when `DomainLogic` is required.
- Domain restore logic must return `Result<TEntity>` or `Result` so validation failures propagate without persistence.
- Metadata/validated setter restore may be allowed only when explicitly enabled for simple public-settable properties.
- Collection restore requires stable item identity. If no identity can be resolved, the restore command must fail before mutation.
- Complex graph restore uses EF ownership/key metadata by default and requires a registered graph restore plan only when includes, ownership, identity, or delete behavior are ambiguous.
- Restore is a new change, not a rollback of the history table.

### Story 7: Configure Capture Scope And Sensitive Data

- Status: Implemented
- Ready: Yes
- Ready Reason: Scope and privacy concerns can be expressed in options and tested independently.
- User Story: As a developer, I want to configure which entities and properties are observed, so that ChangeHistory captures useful data without leaking sensitive values.

Acceptance Criteria:

1. Given an entity type is excluded, when it changes, then no history rows are stored.
2. Given a property is excluded, when it changes, then no row is stored for that property.
3. Given a property is marked sensitive, when it changes, then the row is stored with redacted values or hashes according to options.
4. Given old/new serialized values exceed the configured length policy, when history is persisted, then the values are truncated, rejected, or hash-only according to options.
5. Given a global capture strategy is configured, when an entity has no override, then the global strategy is used.
6. Given an entity-specific capture strategy is configured, when that entity changes, then the entity-specific strategy overrides the global strategy.
7. Given an entity is not explicitly opted in, when it changes, then no direct-mutation, bulk-update, or create history is stored for that entity.
8. Given an entity is opted in and no property policy is configured, when history is captured, then all supported properties are included by default.
9. Given native bulk-insert capture is not explicitly enabled, when `IEntityBulkInserter<TEntity>` inserts tracked entities, then no ChangeHistory rows are stored.
10. Given summary native bulk-insert capture and the explicit bulk decorator are configured, when a batch succeeds, then one non-restoreable row records the operation id and inserted count in the same transaction.
11. Given detailed native bulk-insert capture is configured, when a batch exceeds its positive safety limit or has no stable entity identifiers, then the operation fails without committing native rows or history rows.

Data Requirements:

- `DefaultCaptureStrategy`: global strategy used only for opted-in entities when an entity has no override.
- `EntityCaptureStrategy`: optional per-entity override.
- `TrackedEntities`: explicit entity registrations that enable ChangeHistory capture.
- `CaptureCreates`: optional per-entity setting for insert/create initial-value capture.
- `BulkInsertCaptureMode`: optional per-entity `Disabled`, `Summary`, or `Detailed` capture for explicitly decorated native bulk inserts.
- `BulkInsertMaxDetailedEntities`: positive per-entity safety limit for detailed native bulk-insert capture; defaults to `1000` after opt-in.
- `CaptureDirectMutations`: optional per-entity setting for direct mutation capture.
- `CaptureUpdateSet`: optional per-entity setting for bulk update capture.
- `CaptureChanges`: explicit convenience preset enabling creates, required direct-mutation capture, and best-effort set-based update capture.
- `SensitiveValueDefault`: common sensitive property names such as password, secret, token, credential, API key, and connection string are hash-only by default, with explicit `Exclude`, `Redact`, `HashOnly`, or include overrides per property/path.
- `ConcurrencyVersion`: excluded by convention for entities implementing `IConcurrency`; explicit property policy wins.
- `OversizedValuePolicy`: `Include`, `Truncate`, `HashOnly`, or `Reject` for serialized values above the configured maximum stored length.
- `RestoreConcurrencyPolicy`: `None`, `ExpectedVersion`, or `RequireExpectedVersion` for concurrency-enabled entities.
- `RestoreAuthorizer`: optional per-entity hook that must authorize restore before mutation.
- Strategy precedence: entity override, global option for opted-in entities, disabled for unregistered entities.

Configuration Requirements:

```csharp
services.AddEntityFrameworkRepository<Customer, CustomerDbContext>()
    .WithBehavior<RepositoryAuditStateBehavior<Customer>>()
    .WithBehavior<RepositoryChangeHistoryBehavior<Customer, CustomerDbContext>>();

services.AddChangeHistory(options => options
    .UseReadAuthorizationPolicy("ChangeHistory.Read")
    .UseRestoreAuthorizationPolicy("ChangeHistory.Restore")
    .UseDefaultUpdateSetMaxAffectedRows(1000)
    .UseOversizedValuePolicy(ChangeHistoryOversizedValuePolicy.HashOnly, maxStoredValueLength: 4000)
    .Track<Customer>()
        .CaptureCreates()
        .CaptureBulkInserts()
        .CaptureDirectMutations(
            strategy: ChangeHistoryCaptureStrategy.RepositorySnapshot,
            mode: ChangeHistoryCaptureMode.Required)
        .CaptureUpdateSet(ChangeHistoryCaptureMode.Required, maxAffectedRows: 1000)
        .UseCaptureStrategy(ChangeHistoryCaptureStrategy.RepositorySnapshot)
        .UseRestoreConcurrencyPolicy(ChangeHistoryRestoreConcurrencyPolicy.RequireExpectedVersion)
        .UseRestoreAuthorizer<CustomerRestoreAuthorizer>()
        .Exclude(c => c.PasswordHash)
        .Redact(c => c.Email)
        .HashOnly(c => c.RefreshToken)
        .AllowRestore(c => c.FirstName)
            .UseDomainMethod((customer, value) => customer.ChangeFirstName(value))
        .AllowRestore(c => c.LastName)
            .UseDomainMethod((customer, value) => customer.ChangeLastName(value))
        .CaptureOwned(c => c.BillingAddress, path => path.UseRestorePlan<CustomerAddressRestorePlan>())
        .CaptureCollection(c => c.Addresses, address => address.Id, path => path.UseRestorePlan<CustomerAddressRestorePlan>())
        .CaptureGraph("Orders", graph => graph
            .UseIdentity<Order, Guid>("Orders", order => order.Id)
            .UseIdentity<OrderItem, Guid>("Orders.Items", item => item.Id)
            .UseRestorePlan<CustomerOrderItemsRestorePlan>())));
```

Notes:

- API shape is illustrative. Implementation should follow existing builder conventions in the repository and EF packages.
- `ChangeHistoryCaptureStrategy.EntityChangeOnly` should be available for modules that want history only from `EntityChangeBuilder`.
- `ChangeHistoryCaptureStrategy.RepositorySnapshot` should be the default for modules using untracked entities.
- `ChangeHistoryCaptureStrategy.EfChangeTracker` should be explicit opt-in for tracked-entity modules.
- Direct mutation, bulk update, and create capture should require explicit opt-in per entity.
- The default value policy for opted-in entities should include supported non-sensitive properties until the module configures `Exclude`, `Redact`, or `HashOnly`.
- Sensitive names are protected by default, but modules remain responsible for explicitly excluding or redacting credentials, secrets, tokens, PII, PHI, and large binary payloads whose names are not covered by default patterns.
- Hash-only capture stores hashes for comparison/integrity and intentionally cannot support restore because raw values are not persisted.
- Restore configuration must allow domain-method mappings and typed domain restore handlers before any setter-based restore path.
- Restore endpoints and application services must require explicit authorization and should pass an expected concurrency version for concurrency-enabled entities.

## 8. Proposed Architecture

```text
Domain entity method
  -> this.Change()
       .Set(...)
       .Set(...)
       .Apply()
  -> EntityChangeBuilder creates EntityChangeSet in memory
  -> Application handler calls repository.UpdateAsync(entity)
  -> RepositoryChangeHistoryBehavior reads pending change sets
     and compares the submitted untracked entity with a persisted baseline
  -> EF Core writes entity update + __ChangeHistory rows
  -> Optional domain events/outbox continue as configured

Direct mutation
  -> entity.Property = newValue
  -> repository.UpdateAsync(entity) or update-style repository.UpsertAsync(entity)
  -> RepositoryChangeHistoryBehavior selects configured capture strategy
  -> EntityChangeOnly: no direct-mutation rows
  -> RepositorySnapshot: load persisted baseline and compare baseline/submitted values
  -> EfChangeTracker: compare EF original/current values
  -> EF Core writes entity update + __ChangeHistory rows

Bulk update
  -> repository.UpdateSetAsync(specification, set => ...)
  -> RepositoryChangeHistoryBehavior snapshots affected rows
  -> inner repository executes set-based update
  -> RepositoryChangeHistoryBehavior reloads affected rows
  -> EF Core writes __ChangeHistory rows with one BulkOperationId

Create
  -> repository.AddAsync(entity)
  -> RepositoryChangeHistoryBehavior checks per-entity create opt-in
  -> included initial values are stored with OldValue=null, NewValue=<initial>
  -> EF Core writes entity insert + __ChangeHistory rows

Native bulk insert
  -> IEntityBulkInserter<TEntity>.InsertAsync(entities)
  -> EntityBulkInserterChangeHistoryBehavior checks explicit per-entity bulk opt-in
  -> Summary: store one non-restoreable row with BulkOperationId and inserted count
  -> Detailed: enforce the batch limit and stable identifiers, then apply value policies
  -> EF Core writes native entity rows + __ChangeHistory rows in one transaction
```

### Domain Layer

Add domain-neutral change capture types under `src/Domain/Model/EntityChange` or `src/Domain/Model/ChangeHistory`:

- `EntityChangeSet`
- `EntityPropertyChange`
- `EntityChangeHistoryAccessor` or equivalent pending-change buffer
- Extended `EntityChangeContext`

Rules:

- Domain types hold raw old/new values and metadata necessary to describe the change.
- Domain types do not know EF Core, database tables, users, or serialization.
- Change sets are produced only after successful `.Apply()`.
- Domain capture remains optional for direct mutation because direct mutation is detected by infrastructure when persisted.

### Infrastructure Layer

Add EF Core persistence under `src/Infrastructure.EntityFramework`:

- `ChangeHistoryEntry`
- `IChangeHistoryContext` with `DbSet<ChangeHistoryEntry> ChangeHistory { get; set; }`
- `RepositoryChangeHistoryBehavior<TEntity, TContext>`
- `EntityBulkInserterChangeHistoryBehavior<TEntity, TContext>`
- `IEntityChangeDetector<TEntity>`
- `RepositorySnapshotChangeDetector<TEntity, TContext>`
- `EntityFrameworkChangeTrackerChangeDetector<TEntity, TContext>`
- `EntityFrameworkCreateChangeDetector<TEntity, TContext>`
- `EntityFrameworkUpdateSetChangeDetector<TEntity, TContext>`
- `IChangeHistoryRestorePlan<TEntity>`
- `ChangeHistoryOptions`
- EF configuration and indexes
- registration extensions, for example `WithChangeHistory(...)`

Rules:

- The behavior enriches rows with user, timestamp, correlation, module, serialization, redaction, and hashes.
- For EF repositories, add history rows before the save operation where possible so entity and history rows commit together.
- If the existing repository flow requires a second `SaveChangesAsync`, wrap entity and history writes in the same transaction or document the limitation before release.
- Direct mutation capture uses the configured strategy: `EntityChangeOnly`, `RepositorySnapshot`, or `EfChangeTracker`.
- `RepositorySnapshot` loads a persisted no-tracking baseline and compares it with the submitted untracked entity before update.
- `EfChangeTracker` uses EF original/current values and is valid only for tracked-entity modules.
- Create capture stores initial values only when enabled for the entity.
- Bulk update capture uses pre-update and post-update snapshots for each affected entity and stores all rows under a shared `BulkOperationId`.
- Native bulk-insert capture is independently opted in and explicitly decorated; repository behavior registration is never inferred. Summary mode stores one batch row, while detailed mode applies value policies and a positive safety limit.
- Restore plans define scalar, owned value-object, collection, and graph restore rules per entity type.

### Application Layer

Add optional reusable services/requests if the feature exposes restore/query workflows:

- `ChangeHistoryFindAllQuery`
- `ChangeHistoryFindOneChangeSetQuery`
- `ChangeHistoryFindAllRequest<TContext>`
- `ChangeHistoryFindAllChangeSetsRequest<TContext>`
- `ChangeHistoryFindOneChangeSetRequest<TContext>`
- `ChangeHistoryFindAllRequestHandler<TContext>`
- `ChangeHistoryFindAllChangeSetsRequestHandler<TContext>`
- `ChangeHistoryFindOneChangeSetRequestHandler<TContext>`
- `ChangeHistoryFindAllResult`
- `ChangeHistoryFindAllChangeSetsResult`
- `ChangeHistoryRestoreCommand<TEntity>`
- `ChangeHistoryRestoreRequest<TEntity, TContext>`
- `ChangeHistoryRestoreRequestHandler<TEntity, TContext>`
- `IChangeHistoryRestoreHandler<TEntity>`
- `IChangeHistoryGraphRestorePlan<TEntity>`

Rules:

- Query services return `ResultPaged<T>`; requester handlers return `Result<T>` with a response DTO to avoid nested result values.
- Query services support flat rows, grouped change-set rows, and one-change-set lookup.
- Restore handlers load the entity through repositories, resolve the configured domain restore handler or method mapping, apply old values through that domain logic, then persist normally.
- Validation failures use existing result errors such as `ValidationError` and `NotFoundError`.
- Restore handlers must validate collection identity and graph restore plans before mutating the entity.

Registration:

```csharp
services.AddRequester();
services.AddChangeHistoryRequesterHandlers<AppDbContext>();
services.AddChangeHistoryRequesterHandlers<Customer, AppDbContext>();
```

### Presentation Layer

Presentation integration is optional and uses minimal APIs plus DTOs. Built-in endpoints are registered per entity/context pair:

```csharp
services.AddChangeHistoryEndpoints<Customer, AppDbContext>(options => options
    .GroupPath("/_bdk/api/customers/history")
    .RequireReadPolicy("Customers.History.Read")
    .RequireRestorePolicy("Customers.History.Restore"));
```

Mapped routes:

- `GET /`
- `GET /change-sets`
- `GET /change-sets/{changeSetId}`
- `GET /{entityId}`
- `POST /{entityId}/change-sets/{changeSetId}/restore`

Rules:

- Built-in endpoints are optional and must be enabled by module configuration.
- Restore endpoints should require the target entity type and entity id to avoid restoring the wrong entity from a bare change set id.
- Endpoints support group-level authorization through `EndpointsOptionsBase` and route-specific policies through `RequireReadPolicy(...)` and `RequireRestorePolicy(...)`.
- DTOs hide serialized values by default through endpoint options and can explicitly include values when configured.
- Presentation uses requester-backed query and restore requests.

## 9. Capture Semantics

### Property Changes

- Track only real changes where `EqualityComparer<T>.Default` or a provided comparer determines old and new values differ.
- Preserve first old value and final new value for repeated property changes in one change set.
- Use the property expression to derive stable property names.
- Store one row per property per change set.
- When `EntityChange` capture and repository snapshot comparison see the same property in one repository save, prefer the `EntityChange` row and suppress the snapshot duplicate.

### Collections

Collection changes are supported and restoreable when item identity can be resolved.

- Persist collection operations as `Operation = CollectionChanged`.
- Store item action metadata: `Added`, `Removed`, `Replaced`, or `Cleared`.
- Store item identity using an explicit identity selector, typed entity id, or EF key metadata.
- Store old/new collection snapshots when configured, or compact item-level deltas when only membership changed.
- Restore collection membership by replaying the inverse action against the current entity after validating the expected current state.
- Fail restore before mutation if the collection item identity or ownership cannot be determined.

### Owned Value Objects And Complex Graphs

Owned value objects and graph paths with inferable EF metadata or restore plans are supported.

- Owned value-object changes are stored by property path, for example `Address.City`.
- Owned value-object restore replaces the owned object or applies persisted property values according to the restore plan.
- Complex graph capture uses inferred or configured include paths so the pre-change and post-change snapshots are comparable.
- Complex graph restore uses EF metadata when ownership and identity are unambiguous; otherwise it uses a graph restore plan that declares ownership, identity, allowed paths, and delete behavior.
- If a graph path is not configured and cannot be safely inferred from EF metadata, capture may store a non-restoreable summary row and restore must fail with a validation error.

### Execute Operations

Current `Execute(...)` operations can mutate state without property expressions. For MVP:

- Do not create restoreable property rows for `Execute(...)`.
- Optionally record a non-restoreable technical row only if configured.
- Recommend using `.Set(...)` for changes that must be explicitly grouped and restoreable.

### Direct Property Mutation

Direct property mutation outside `EntityChange` is handled according to the configured direct-mutation strategy.

`EntityChangeOnly`:

- Records only changes produced by `.Change().Apply()`.
- Does not inspect direct property mutation.
- Is useful when a module wants strict domain-method history only.

`RepositorySnapshot`:

- Load the persisted baseline by entity id before update using no-tracking queries.
- Compare the persisted baseline with the submitted untracked entity.
- Use one repository operation as the change set boundary when no `EntityChangeSet` exists.
- Mark rows with `CaptureSource = RepositorySnapshot`.
- Apply the same exclusion, redaction, serialization, and restore policies as `EntityChange` capture.
- Fail before update when capture is required and no baseline can be loaded for an update.

Repository snapshot algorithm:

1. Validate the submitted entity has a non-default id.
2. Load the persisted baseline by id with `NoTracking = true` and the configured include graph.
3. Compare configured scalar, owned value-object, collection, and graph paths.
4. Suppress rows already captured by a pending `EntityChangeSet`.
5. For `UpsertAsync`, treat existing persisted baseline as update history and missing baseline as insert behavior.
6. Add pending `ChangeHistoryEntry` rows to the same DbContext before the inner repository update.
7. Call the inner repository update so entity changes and history rows commit together.

`EfChangeTracker`:

- Reads EF original/current values from tracked entries.
- Marks rows with `CaptureSource = EfChangeTracker`.
- Must fail fast when configured for an untracked entity update and capture mode is `Required`.
- Should not be the default in bITdevKit modules that normally use untracked entities.

### Create Operations

Create capture records initial values for explicitly opted-in entities.

- Use one `ChangeSetId` for all included initial values created in one entity insert.
- Mark rows with `Operation = Create` and `CaptureSource = Create`.
- Store `OldValue = null` and `NewValue = <initial value>` for scalar and owned value rows.
- Store identifiable initial collection members with `CollectionAction = Added`.
- Do not make create rows restoreable by default; restoring "before create" requires explicit delete/deactivate domain behavior.

### Bulk Updates

Repository `UpdateSetAsync` is supported when the EF provider can identify and snapshot affected rows.

- Snapshot affected rows before the bulk update using the same specifications/options passed to `UpdateSetAsync`.
- Execute the set-based update through the inner repository.
- Reload affected rows after the update.
- Compare before/after snapshots and create one change set per affected entity.
- Assign one `BulkOperationId` to all rows from the same `UpdateSetAsync` call.
- Enforce a configurable max affected row limit to avoid accidental high-volume history explosions.

## 10. Restore Semantics

Restore means "apply the old values from one stored change set to the current entity as a new change."

Rules:

- Restore uses the whole change set by default.
- Restore validates all properties before applying any value.
- Restore is atomic from the caller perspective.
- Restore writes a new history change set.
- Restore never deletes or updates existing history rows.
- Restore must support domain logic as a first-class execution mode, so modules can restore by calling existing domain methods or typed domain restore handlers.
- When a domain restore handler or method mapping is configured for a path, restore must invoke it and must not bypass it with reflection or direct setters.
- Domain restore logic must return `Result` or `Result<TEntity>`; failure stops restore before persistence and before restore history rows are written.
- Validated setter restore is only a fallback for explicitly allowed paths without configured domain logic.
- Restore supports scalar properties, owned value objects, collection membership, and graph paths with inferable EF metadata or restore plans.
- Restore must fail before mutation when an identity, include path, ownership rule, or delete behavior is ambiguous.

Example restore flow:

```text
POST restore(changeSetId)
  -> load change set rows
  -> load current entity
  -> validate entity type/id and restore policy
  -> load configured include paths for collections/graphs
  -> deserialize old values
  -> resolve domain restore handler or method mapping when configured
  -> apply values through domain logic, restore plan, or explicit setter fallback
  -> repository.UpdateAsync(entity)
  -> new ChangeHistory rows with Operation=Restore
```

## 11. Database Design

Recommended indexes:

- `(EntityType, EntityId, ChangedDate DESC)`
- `(ChangeSetId, ChangeSetSequence)`
- `(BulkOperationId)`
- `(EntityType, EntityId, PropertyName, ChangedDate DESC)`
- `(ChangedByUserId, ChangedDate DESC)`
- `(CorrelationId)`
- `(ModuleName, ChangedDate DESC)`

Retention:

- No automatic purge in MVP.
- Add a retention option later only if product requirements allow deleting audit/history data.

Migration:

- Each module DbContext that wants history implements `IChangeHistoryContext`.
- Migrations are generated through the module's existing EF Core code-first workflow.

## 12. Security And Privacy

- History can contain sensitive data. Exclusion/redaction must be supported from the first implementation.
- Restore must be protected by explicit authorization.
- Presentation DTOs must never accidentally expose redacted raw values.
- The default for opted-in entities is to include supported values until a module configures otherwise.
- Module configuration should explicitly exclude or redact secrets such as password hashes, tokens, API keys, refresh tokens, and large binary data.
- Consider hash-only storage for sensitive values where change detection is needed but value recovery is not allowed.

## 13. Observability

- Use structured Serilog message templates:
  - `[ChangeHistory] stored change set (entityType={EntityType}, entityId={EntityId}, changeSetId={ChangeSetId}, propertyCount={PropertyCount})`
  - `[ChangeHistory] restore requested (entityType={EntityType}, entityId={EntityId}, changeSetId={ChangeSetId})`
  - `[ChangeHistory] restore failed (entityType={EntityType}, entityId={EntityId}, changeSetId={ChangeSetId}, reason={Reason})`
- Propagate `CorrelationId`, `FlowId`, `ModuleName`, and activity parent id like `RepositoryOutboxDomainEventBehavior`.

## 14. Testing Strategy

### Unit Tests

- `EntityChange` creates one change set per successful `.Apply()`.
- Multiple properties share one `ChangeSetId`.
- Repeated property changes preserve first old value and final new value.
- Same-value assignments produce no property history.
- Failed `Apply()` does not enqueue history.
- `EntityChangeOnly` records no direct-mutation rows outside `EntityChangeBuilder`.
- `RepositorySnapshot` creates change records by comparing the persisted baseline with the submitted untracked entity.
- `EfChangeTracker` creates change records from EF original/current values for tracked entities.
- Missing baselines fail before update when repository snapshot capture is required.
- Duplicate snapshot rows are suppressed when `EntityChange` already captured a property.
- Create capture writes `Operation = Create` rows with null old values and initial new values when enabled.
- Create capture writes no rows when the entity is not opted in.
- `UpdateSetAsync` creates one `BulkOperationId` and one change set per affected entity.
- Collection deltas include action, identity, old value, and new value.
- Owned value-object changes are captured by property path.
- Redaction/exclusion policies are applied before serialization.
- Restore policy rejects non-restorable properties.
- Restore configuration validation fails when `DomainLogic` mode has no domain method mapping or typed handler.
- Domain restore handlers are invoked when configured for a restore path.
- Domain restore handler failures stop restore without persisting entity changes or restore history rows.
- Restore plans validate collection identity and graph paths before mutation.

### Integration Tests

- EF repository behavior inserts `__ChangeHistory` rows according to the configured direct-mutation capture strategy.
- Entity update and history rows commit together.
- Query handler returns paged/grouped history.
- Direct mutation on an untracked entity through `repository.UpdateAsync` or update-style `repository.UpsertAsync` writes history without requiring `EntityChange`.
- Repository update loads a no-tracking baseline before update and writes history through snapshot comparison.
- Entity insert writes create rows only for entities with create capture enabled.
- `EntityChangeOnly` mode does not write history for direct mutation.
- `EfChangeTracker` mode writes history only when the entity is tracked.
- Bulk `UpdateSetAsync` writes per-entity/per-property history and respects max affected row limits.
- Restore command updates scalar properties and creates a new restore history set.
- Restore command updates owned value objects and creates a new restore history set.
- Restore command restores collection membership when item identity is available.
- Restore command restores complex graph paths when EF metadata or restore plans provide stable ownership and identity.
- Restore command invokes configured domain methods/handlers and propagates `Result` failures.
- No history is persisted when behavior is not registered.

### Architecture Tests

- Domain project has no EF Core dependency.
- Application handlers depend on abstractions and repositories, not concrete DbContext.
- Presentation exposes DTOs, not EF persistence entities.

## 15. Alternatives Considered

### Alternative 1: Use EF Core ChangeTracker As The Default

Rejected because this repository works with untracked entities. ChangeTracker original values are not a reliable default source of old values in this project. The accepted design keeps `EfChangeTracker` as an explicit capture strategy and recommends `RepositorySnapshot` as the default for bITdevKit modules.

### Alternative 2: Use Domain Events Only

Rejected as the source of truth because domain events may be customized, replaced, or routed through outbox asynchronously. ChangeHistory requires a consistent, queryable persistence model.

### Alternative 3: Require Event Sourcing

Rejected because Event Sourcing is a larger architectural choice. ChangeHistory should work for regular aggregates and entities without making event streams the source of truth.

### Alternative 4: Add History Columns To Each Entity Table

Rejected because history is multi-row, time-based data and would pollute each entity table. A separate table is simpler to query, index, secure, and retain.

## 16. Consequences

### Positive

- Developers get property-level history without adopting event sourcing.
- Support and audit workflows can inspect changes by entity, property, user, and time.
- Restore uses existing domain/repository flows and remains testable.
- Repository behavior keeps persistence concerns out of Domain and Application code.
- Existing audit/outbox/correlation features continue to work.

### Negative

- Additional rows are written for every captured property change.
- Serialized values can increase database size.
- Restore needs explicit policies to avoid bypassing domain invariants.
- Direct mutation capture cost depends on strategy: `RepositorySnapshot` adds a baseline read before update, while `EfChangeTracker` requires tracked entities.
- Bulk update capture adds pre-update and post-update reads, which can be expensive for large affected sets.
- Collection and graph restore require stable identity and explicit restore rules for ambiguous cases.

### Neutral

- Behavior ordering matters. Recommended order is audit state before change history, and outbox after domain events are registered.
- Collection and graph support are part of the scope, with stricter validation than scalar property restore.
- Modules must opt in by implementing the context contract and registering the behavior.

## 17. Implementation Plan

1. Extend `EntityChange` change records to store old and new values.
2. Add domain-neutral `EntityChangeSet` and pending-change accessor/buffer.
3. Add configurable direct-mutation capture strategies: `EntityChangeOnly`, `RepositorySnapshot`, and `EfChangeTracker`.
4. Add optional create capture for explicitly opted-in entities.
5. Add `UpdateSetAsync` snapshot capture with `BulkOperationId` support.
6. Add collection and owned value-object delta capture.
7. Add EF `ChangeHistoryEntry`, `IChangeHistoryContext`, options, and repository behavior.
8. Add integration tests for persistence and transactional behavior.
9. Add query service/handler and DTOs.
10. Add restore policy/plan abstractions, domain restore handler abstractions, and restore command.
11. Add scalar, owned value-object, collection, and graph restore tests.
12. Document setup, limitations, and examples in feature docs.

## 18. Resolved Decisions And Recommendations

- Restore endpoint: provide an optional built-in restore endpoint for a specified entity type, entity id, and change set id. Modules can disable it and expose only application services if they need custom API shape.
- Authorization policies: make read and restore policy names configurable. The feature should not hard-code product policy names.
- Sensitive values: include supported values by default for opted-in entities and properties. Modules configure `Exclude`, `Redact`, or `HashOnly` when values should not be stored as raw history.
- Create operations: support optional per-entity create capture. A create record uses one `ChangeSetId`, `Operation = Create`, `OldValue = null`, and `NewValue = <initial value>` for each included property/path. Create rows are not restoreable by default because restoring "before create" means delete/deactivate, which must be a separate explicit domain operation.
- Capture opt-in: direct mutation, bulk update, and create capture require explicit opt-in per entity. This avoids surprising persistence overhead and avoids accidentally storing sensitive values for entities that were never selected.
- `UpdateSetAsync` limit: default `maxAffectedRows` should be `1000`. This is large enough for normal administrative batches and small enough to prevent accidental high-volume history writes. Larger jobs must override the limit explicitly.
- Collection identity inference: built-in inference should be limited to deterministic identity sources: entity ids, EF primary/alternate keys, and explicitly configured identity selectors. Do not infer identity from arbitrary property names by default; convention-based inference can be an explicit advanced option later.
- Table model: keep one physical `__ChangeHistory` table as the default model. Keep the code model logically separated into change set metadata and property rows so high-volume modules can later use a normalized two-table implementation without changing application APIs.
