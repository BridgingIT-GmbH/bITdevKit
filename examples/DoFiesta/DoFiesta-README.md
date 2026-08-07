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
