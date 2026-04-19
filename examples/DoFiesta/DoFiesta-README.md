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

# Development

### Entity Framework

- Setup: `dotnet tool install --global dotnet-ef` or `dotnet tool update dotnet-ef -g`
- Migrations:
  `dotnet ef migrations add [NAME] --context CoreDbContext --output-dir .\EntityFramework\Migrations --project .\examples\DoFiesta.Infrastructure\DoFiesta.Infrastructure.csproj --startup-project .\examples\DoFiesta.Presentation.Web.Server\DoFiesta.Presentation.Web.Server.csproj`
- Database Update: done when starting the service (MigrationsHostedService)
