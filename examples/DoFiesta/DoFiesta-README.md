### Layers & Dependencies

```

                                                                    - Services, Jobs, Validators
                                                                    - Commands/Query + Handlers
                                                .----------------.  - Messages/Queues + Handlers
   - WebApi/Mvc/                            .-->| Application    |  - Adapter Interfaces, Exceptions
     SPA/Console program host              /    `----------------`  - View Models + Mappings
                                          /        |        ^
  .--------------.                       /         |        |
  .              |     .--------------. /          V        |  - Events, Aggregates, Services
  | Presentation |     |              |/        .--------.  |  - Entities, ValueObjects
  | .Web|Tool    |---->| Presentation |-------->| Domain |  |  - Repository interfaces
  |  Service|*   |     |              |\        `--------`  |  - Specifications, Rules
  |              |     `--------------` \          ^        |
  `--------------`                       \         |        |
                       - Composition Root \        |        |
                       - Controllers       \    .----------------.  - Interface Implementierungen (Adapters/Repositories)
                       - Razor Pages        `-->| Infrastructure |  - DbContext
                       - Hosted Services        `----------------`  - Data Entities + Mappings

```

# Presentation Layer

#### Api Client

The Api Client is generated with
the [Unchase extension](https://marketplace.visualstudio.com/items?itemName=Unchase.unchaseopenapiconnectedservice).
This is a Visual Studio extension to generate C# (TypeScript) HttpClient (or C# Controllers) code
for OpenAPI (formerly Swagger API) web service with NSwag.
The underlaying generator used is NSwag.

DoFiesta.Presentation.Web.Client > Connected
Services > [DoFiesta](.\DoFiesta.Presentation.Web.Client\Connected%20Services\DoFiesta)

Right click and choose 'Update Unchase' to regenerate
the [ApiClient](.\DoFiesta.Presentation.Web.Client\Connected%20Services\DoFiesta\ApiClient.cs)

# Application Layer

# Domain Layer

# Infrastructure

-------------------

# Domain

# Application

# Presentation

## Todo attachments

- Todo-item attachments are stored through the regular todo API, not through the admin-only `_system` file-storage endpoints.
- The server uses the named `attachments` file-storage provider and stores each todo item's files below a folder named after the todo item id.
- The Blazor todo page exposes the feature through an attachment dialog on each row, supporting list, upload, download, and delete for text and binary files.

## ChangeHistory

- DoFiesta enables ChangeHistory for `TodoItem` and `Subscription` in the Core module.
- History rows are stored in the shared `__ChangeHistory` table configured on `CoreDbContext`.
- `TodoItem` captures creates, direct repository updates, bulk updates, and identifiable `Steps` collection changes.
- `Subscription` captures creates, direct repository updates, and bulk updates.
- User ids are hash-only, todo assignees are redacted, oversized values are truncated, and concurrency tokens are excluded.
- Restore is enabled for selected scalar fields, todo status through domain logic, enum-like fields through validated setters, and configured collection history when stored values are restoreable.
- Read and restore requests use DoFiesta entity permissions. Reads require entity/list read permissions, and restores require write permissions before and after the target entity is loaded.
- Restore endpoints require authentication.
- Todo history endpoints are available below `api/core/todoitems/history`.
- Subscription history endpoints are available below `api/core/subscriptions/history`.

## Todo DataPorter bulk imports

- The todo DataPorter import endpoint parses and validates uploaded rows first, then an `AfterImportCompletedAsync` interceptor writes successful `TodoItem` rows through the Domain-owned `IEntityBulkInserter<TodoItem>` capability.
- `CoreModule` configures `CoreDbContext` through `AddSqlServerDbContext<CoreDbContext>()`, which automatically registers the SQL Server native strategy, and registers `IEntityBulkInserter<TodoItem>` independently with `AddEntityFrameworkBulkInserter<TodoItem, CoreDbContext>()`.
- The example intentionally keeps normal CRUD on `IGenericRepository<TodoItem>` and uses the bulk inserter only for the explicit import path.
- `TodoItemBulkImportPersistenceInterceptor` intentionally calls `entity.Steps.Clear()` because the native capability writes the aggregate root table only. Importing steps requires a separate persistence workflow or normal repository insertion.
- The explicit bulk chain applies cancellation, tracing, logging, metrics, outbox, audit, concurrency, created domain events, and event metrics in its configured order. Repository decorators are independent; inputs remain detached and store-generated values are not copied back.

# Development

### Entity Framework

- Setup: `dotnet tool install --global dotnet-ef` or `dotnet tool update dotnet-ef -g`
- Migrations:
  `dotnet ef migrations add [NAME] --context CoreDbContext --output-dir .\EntityFramework\Migrations --project .\examples\DoFiesta.Infrastructure\DoFiesta.Infrastructure.csproj --startup-project .\examples\DoFiesta.Presentation.Web.Server\DoFiesta.Presentation.Web.Server.csproj`
- Database Update: done when starting the service (MigrationsHostedService)


## Application caching

`GET /api/core/enumerations` reads the shared reference catalog through `EnumerationCatalogQuery` and `EnumerationCatalog`. The application caches the completed JSON object using `JsonSerializerOptions.Web` for camelCase property names. Authentication runs on every HTTP request, including a hit. Todo data and per-item permission decisions are evaluated through their existing application paths.

The `reference-catalog` collection uses the `core` Entity Framework provider, SQL Server, a one-hour absolute TTL and shared-store source coordination. The entry has the stable key `enumerations`. Concurrent nodes using the same database share retained content and source ownership. Expiration or explicit invalidation reloads the current catalog. Shared authority failure returns an explicit failure before starting a new source; it does not change the configured coordination scope.

`CoreCacheSeederTask` fills the `memory-demo` collection at startup with 48 fictional string values. The named `demo-memory` InMemory provider retains eight entries at each payload size: 1 KiB, 8 KiB, 32 KiB, 128 KiB, 512 KiB and 1 MiB, totalling about 13.3 MiB before JSON and metadata overhead. Compression is disabled so the entries exercise resident memory capacity. The collection and provider each have a 32 MiB ceiling and a 128-entry limit, with a 2 MiB per-value limit and one-hour absolute expiry. Each application node owns its own copy. Restarting the application repopulates the collection; running the task again replaces the same keys.

Use `memory-demo` in the caching dashboard to inspect entry sizes, long key suffixes and the `category`, `payloadBytes`, `active`, `tags` and `sequence` dimensions. For example, select `category = report`, `payloadBytes = 1048576`, or set membership `tags contains demo` in Entries. Six entries have long keys. Operations can preview and invalidate a key selection, or clear the collection. This seed task writes only to memory.

The `task-lookups` collection contains 72 task-title suggestions across Development, Testing, DevOps, Security, Performance and Documentation. Each suggestion is a separate typed JSON entry with `category`, `technology` and `language` dimensions. `TaskLookupSeederTask` warms the collection at startup. `TaskLookupCatalog` uses cache-aside reads from the static catalog, so expiration, invalidation or clearing reloads suggestions on demand. The `lookups-memory` InMemory provider has an 8 MiB resident and storage ceiling and a 256-entry limit. The collection has an 8 MiB resident ceiling, 4 MiB storage ceiling, 64 KiB payload limit and one-hour absolute expiry.

`GET /api/core/task-lookups` returns the suggestions through `TaskLookupsQuery` and requires authentication on every request. In Create Task, the Title input loads suggestions once when the dialog opens and filters them locally. Selecting a suggestion fills the title; free-text titles are accepted too. The input commits changes on selection or blur, keeping typing updates inside the input component. If suggestions cannot be loaded, a warning appears and task creation stays available.

`CoreDbContext` implements `IApplicationCachingContext`, so its regular module model includes the annotated `__Caching_*` tables. `AddSqlServerDbContext(...).WithDbContextFactory()` supplies independent contexts from the module's SQL Server configuration, including worktree catalog isolation. Request tracking, transactions and business-save interceptors belong to their request scopes. Local development creates the model through the sample's database creator. An existing deployed database needs a consumer-owned migration for its `CoreDbContext` before the named readiness gate is signalled.

Administrators can inspect collections, entry keys, diagnostics and operations through **Caching** at `/_bdk/dashboard/caching`. Key and dimension display is enabled within the administrator dashboard. The generic cache HTTP API is not enabled by this composition. Application cache configuration uses `Caching:Application` by convention. The other `Caching` settings configure Common Caching.

```csharp
// Consume the same declared collection from application code.
var collection = cache.GetCollection<string>(EnumerationCatalog.CollectionName);
var retained = await collection.GetAsync(catalog.CacheKey, cancellationToken);
// The retained value is complete JSON object text for an application/json response.
```

The integration fixture starts an owned SQL Server Testcontainers instance with a unique catalog. It explicitly targets both context registrations at that server and catalog and verifies their identity before schema creation or seeding. It deploys the real module model and uses independent production contexts for business operations and caching. It seeds 205 todo rows and their per-item permissions, then advances the SQL sequence beyond those rows. The host is disposed before its owned container and catalog are removed. Docker is required. Run `dotnet test examples/DoFiesta/DoFiesta.IntegrationTests/DoFiesta.IntegrationTests.csproj` for endpoint, authorization and retained-cache coverage.