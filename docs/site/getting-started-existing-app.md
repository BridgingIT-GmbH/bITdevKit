---
title: Build an app with DevKit
hide:
  - footer
---

<!-- markdownlint-disable-file MD033 -->

# Build an app with DevKit

Start with a minimal ASP.NET Core Web API and add DevKit one capability at a time. The guide builds a Customers module with its own configuration, domain model, repository, and SQL Server database. No entity, database context, or persistence setup is required at the start.

Use the same approach in an existing application. Keep its working features and add only the parts you need. Each step introduces something new rather than converting an existing data model.

<div class="host-stack">
  <div class="host-stack__caption"><span>Build up your application</span><span>Each row links to its step</span></div>
  <ol>
    <li class="is-base"><a href="#step-1"><span class="host-stack__n">01</span><img class="bdk-doc-icon" src="assets/features/host.svg" width="18" height="18" alt=""><span class="host-stack__name">Minimal API</span><code class="host-stack__api">WebApplication.CreateBuilder(args)</code><span class="host-stack__tag">start</span></a></li>
    <li><a href="#step-2"><span class="host-stack__n">02</span><img class="bdk-doc-icon" src="assets/features/mcp.svg" width="18" height="18" alt=""><span class="host-stack__name">bdk MCP</span><code class="host-stack__api">dotnet tool run bdk mcp</code><span class="host-stack__tag">agent tools</span></a></li>
    <li><a href="#step-3"><span class="host-stack__n">03</span><img class="bdk-doc-icon" src="assets/features/web-host.svg" width="18" height="18" alt=""><span class="host-stack__name">DevKit host</span><code class="host-stack__api">DevKitWebApplication.CreateBuilder(args)</code><span class="host-stack__tag">1 package</span></a></li>
    <li><a href="#step-4"><span class="host-stack__n">04</span><img class="bdk-doc-icon" src="assets/features/modules.svg" width="18" height="18" alt=""><span class="host-stack__name">Module</span><code class="host-stack__api">Configure&lt;T&gt;(), AddModules(), MapModules()</code><span class="host-stack__tag">appsettings</span></a></li>
    <li><a href="#step-5"><span class="host-stack__n">05</span><img class="bdk-doc-icon" src="assets/features/domain.svg" width="18" height="18" alt=""><span class="host-stack__name">Customer</span><code class="host-stack__api">AggregateRoot&lt;Guid&gt;, Customer.Create(...)</code><span class="host-stack__tag">1 file</span></a></li>
    <li><a href="#step-6"><span class="host-stack__n">06</span><img class="bdk-doc-icon" src="assets/features/provider-entityframework.svg" width="18" height="18" alt=""><span class="host-stack__name">SQL Server</span><code class="host-stack__api">AddSqlServerDbContext(...), InitialCustomers</code><span class="host-stack__tag">1 package</span></a></li>
    <li><a href="#step-7"><span class="host-stack__n">07</span><img class="bdk-doc-icon" src="assets/features/repositories.svg" width="18" height="18" alt=""><span class="host-stack__name">Repository</span><code class="host-stack__api">AddEntityFrameworkRepository&lt;Customer, CustomersDbContext&gt;()</code><span class="host-stack__tag">registration</span></a></li>
    <li><a href="#step-8"><span class="host-stack__n">08</span><img class="bdk-doc-icon" src="assets/features/requester.svg" width="18" height="18" alt=""><span class="host-stack__name">Handlers</span><code class="host-stack__api">AddRequester(), validation, commands and queries</code><span class="host-stack__tag">2 files</span></a></li>
    <li><a href="#step-9"><span class="host-stack__n">09</span><img class="bdk-doc-icon" src="assets/features/endpoints.svg" width="18" height="18" alt=""><span class="host-stack__name">Endpoints</span><code class="host-stack__api">AddEndpoints&lt;CustomerEndpoints&gt;(), MapEndpoints()</code><span class="host-stack__tag">1 file</span></a></li>
    <li><a href="#step-10"><span class="host-stack__n">10</span><img class="bdk-doc-icon" src="assets/features/domain-events.svg" width="18" height="18" alt=""><span class="host-stack__name">Domain events</span><code class="host-stack__api">AddNotifier(), RepositoryDomainEventPublisherBehavior</code><span class="host-stack__tag">optional</span></a></li>
  </ol>
</div>

Each step ends with a copyable **AI prompt**. Connect bdk MCP in step 2 before using the [planning and apply prompts](#hand-to-an-agent).

---

## Start with a minimal Web API { #step-1 .tutorial-step data-step="01" data-toc-label="01 Web API" }

Use an existing ASP.NET Core application, or create a plain .NET 10 web project. This command uses the standard .NET template:

```powershell
dotnet new web -n Customers.Web -f net10.0
cd Customers.Web
```

The starting app needs only a host and one route:

```csharp title="Program.cs"
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Customers API is running.");

app.Run();
```

For an existing app, keep its `Program.cs` and use one of its existing routes for the first checks. No customer endpoints or database are needed yet. The examples below use `Customers.Web` as the project name and namespace.

!!! verify "Check it"

    Run in Development with PowerShell 7.3 or later:

    ```powershell
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    dotnet run --no-launch-profile --urls http://localhost:5199
    ```

    In a second terminal, run `Invoke-RestMethod http://localhost:5199/`. Expect `Customers API is running.` Stop the app before changing files.

<!-- agent-prompt 1 source="docs/features-presentation.md" -->

---

## Install bdk and connect MCP { #step-2 .tutorial-step data-step="02" data-toc-label="02 MCP" }

Install the local `bdk` CLI so your agent can search DevKit guides, get feature guidance, and check API signatures before changing the app. These tools work without a running DevKit host.

Solutions created from the [DevKit template](getting-started-template.md) already have bdk MCP installed and configured.

Run from your app's repository root in PowerShell 7.3 or later:

```powershell
if (-not (Test-Path .config/dotnet-tools.json) -and -not (Test-Path dotnet-tools.json)) {
    dotnet new tool-manifest
}
dotnet tool install BridgingIT.DevKit.Cli
```

If the manifest already lists the CLI, run `dotnet tool restore` instead of installing it again.

Configure your MCP client to run `dotnet` with arguments `tool run bdk mcp` from this repository root. For clients that read `.mcp.json`, merge this server into the existing configuration:

```json title=".mcp.json"
{
  "mcpServers": {
    "bdk": {
      "command": "dotnet",
      "args": ["tool", "run", "bdk", "mcp"]
    }
  }
}
```

Reload the client's MCP connection. [MCP client setup](reference/features-cli-mcp-clients.md) covers other configuration formats.

!!! verify "Check the tools"

    Ask the agent to call `bdk_mcp_status`, then `bdk_docs_search` with query `host builder` and `bdk_guidance_list`. Expect the correct workspace, documentation results, and guidance topics. No running host is needed.

The step prompts use `bdk_docs_get` to read feature guides, `bdk_guidance_get` for implementation checklists, and `bdk_api_search` with `bdk_api_get` for exact signatures.

<!-- agent-prompt 2 source="docs/features-cli-mcp-clients.md" -->

---

## Add the DevKit application builder { #step-3 .tutorial-step data-step="03" data-toc-label="03 Host" }

Install the host package, then replace `WebApplication.CreateBuilder` with `DevKitWebApplication.CreateBuilder`:

```powershell
dotnet add package BridgingIT.DevKit.Presentation.Web
```

```csharp title="Program.cs"
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;

var builder = DevKitWebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Customers API is running.");

app.Run();
```

Keep all DevKit packages on the same version. With Central Package Management, manage those versions in `Directory.Packages.props`.

!!! verify "Check it"

    Build and run the app. The root route still responds. In Development, `dotnet tool run bdk hosts list` also finds the host.

The builder makes DevKit host integrations available. Enable [logging](reference/features-presentation-logging.md), [request correlation](reference/features-presentation-correlationid.md), or [dashboards](developer-dashboards.md) when you need them.

<!-- agent-prompt 3 source="docs/features-presentation.md" -->

---

## Add a module and its appsettings configuration { #step-4 .tutorial-step data-step="04" data-toc-label="04 Module" }

A module owns its feature registrations and configuration. Add a Customers module before introducing any data model.

??? example "CustomersModule.cs"

    ```csharp title="Modules/Customers/CustomersModule.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Common;

    public class CustomersModule : WebModuleBase
    {
        private CustomersModuleConfiguration moduleConfiguration = null!;

        public override IServiceCollection Register(
            IServiceCollection services,
            IConfiguration? configuration = null,
            IWebHostEnvironment? environment = null)
        {
            this.moduleConfiguration = this.Configure<CustomersModuleConfiguration,
                CustomersModuleConfiguration.Validator>(services, configuration);

            return services;
        }

        public override IApplicationBuilder Use(
            IApplicationBuilder app,
            IConfiguration? configuration = null,
            IWebHostEnvironment? environment = null) => app;

        public override IEndpointRouteBuilder Map(
            IEndpointRouteBuilder app,
            IConfiguration? configuration = null,
            IWebHostEnvironment? environment = null)
        {
            app.MapGet("/api/customers/status", () => Results.Ok(new
            {
                module = this.Name,
                message = this.moduleConfiguration.Greeting,
            })).WithTags("Customers");

            return app;
        }
    }
    ```

??? example "Typed module configuration"

    ```csharp title="Modules/Customers/CustomersModuleConfiguration.cs"
    namespace Customers.Web.Modules.Customers;

    using FluentValidation;

    public class CustomersModuleConfiguration
    {
        public string Greeting { get; set; } = string.Empty;

        public class Validator : AbstractValidator<CustomersModuleConfiguration>
        {
            public Validator()
            {
                this.RuleFor(c => c.Greeting).NotEmpty();
            }
        }
    }
    ```

Add this section to `appsettings.json`. Keep the app's other settings:

```json title="appsettings.json"
{
  "Modules": {
    "customers": {
      "Enabled": true,
      "Greeting": "Customers module is ready."
    }
  }
}
```

`CustomersModule` has the module name `customers`. `Configure<T, TValidator>` binds and validates `Modules:customers`. The module registry reads `Enabled` from the same section.

Register the module before `builder.Build()` and run its lifecycle after the app is built:

```csharp title="Program.cs"
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Customers.Web.Modules.Customers;

var builder = DevKitWebApplication.CreateBuilder(args);

builder.Services.AddModules(builder.Configuration, builder.Environment, modules => modules
    .WithModule<CustomersModule>());

var app = builder.Build();

app.UseRequestModuleContext();
app.UseModules();
app.MapModules();

app.MapGet("/", () => "Customers API is running.");

app.Run();
```

!!! verify "Check it"

    Run `Invoke-RestMethod http://localhost:5199/api/customers/status`. Expect module `customers` and the configured greeting. Change `Greeting`, restart, and check that the response changes. An empty greeting fails configuration validation at startup.

[Modules](reference/features-modules.md) covers naming, configuration, and module lifecycle behavior.

<!-- agent-prompt 4 topics="modules" -->

---

## Add the Customer entity { #step-5 .tutorial-step data-step="05" data-toc-label="05 Entity" }

Introduce the first domain type. The aggregate has an ID and a factory for creating customers. Persistence comes in the next step.

```csharp title="Customer.cs"
namespace Customers.Web;

using BridgingIT.DevKit.Domain.Model;

public class Customer : AggregateRoot<Guid>
{
    private Customer()
    {
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public static Customer Create(string firstName, string lastName, string email)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
        };

        return customer;
    }
}
```

!!! verify "Check it"

    Run `dotnet build`. The app and module status route still work without a database.

[Domain building blocks](reference/features-domain.md) explains entities and aggregate roots.

<!-- agent-prompt 5 topics="domain" -->

---

## Add SQL Server persistence { #step-6 .tutorial-step data-step="06" data-toc-label="06 SQL Server" }

Use a local SQL Server instance and a database dedicated to this guide. Install the provider and EF Core design tools:

```powershell
dotnet add package BridgingIT.DevKit.Infrastructure.EntityFramework.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.*
dotnet tool install dotnet-ef --version 10.*
```

If `dotnet-ef` is already in the local tool manifest, restore it instead. Keep its major version and the design package aligned with the installed EF Core version.

```csharp title="CustomersDbContext.cs"
namespace Customers.Web;

using Microsoft.EntityFrameworkCore;

public class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers { get; set; } = null!;
}
```

Extend `CustomersModuleConfiguration` with the connection string dictionary and its validator:

??? example "Updated module configuration"

    ```csharp title="Modules/Customers/CustomersModuleConfiguration.cs"
    namespace Customers.Web.Modules.Customers;

    using FluentValidation;

    public class CustomersModuleConfiguration
    {
        public string Greeting { get; set; } = string.Empty;

        public Dictionary<string, string> ConnectionStrings { get; set; } = new();

        public class Validator : AbstractValidator<CustomersModuleConfiguration>
        {
            public Validator()
            {
                this.RuleFor(c => c.Greeting).NotEmpty();
                this.RuleFor(c => c.ConnectionStrings)
                    .Must(strings => strings is not null
                        && strings.TryGetValue("Default", out var value)
                        && !string.IsNullOrWhiteSpace(value))
                    .WithMessage("Connection string 'Default' is required.");
            }
        }
    }
    ```

Store the connection string under the module, alongside its other settings:

```json title="appsettings.json"
{
  "Modules": {
    "customers": {
      "Enabled": true,
      "Greeting": "Customers module is ready.",
      "ConnectionStrings": {
        "Default": "Server=localhost;Database=CustomersDev;Integrated Security=true;TrustServerCertificate=true"
      }
    }
  }
}
```

The example uses Windows integrated authentication. Replace the server and authentication settings for your SQL Server installation. For a SQL login, supply the full string through `Modules__customers__ConnectionStrings__Default` instead of putting a password in source control.

In `CustomersModule.Register`, after `Configure<...>`, add:

```csharp
services.AddSqlServerDbContext<CustomersDbContext>(options => options
        .UseConnectionString(this.moduleConfiguration.ConnectionStrings["Default"]))
    .WithDatabaseMigratorService(options => options
        .Enabled(environment?.IsDevelopment() == true)
        .HaltOnFailure());
```

Add `using BridgingIT.DevKit.Infrastructure.EntityFramework;` to the module. The migrator applies pending EF Core migrations at startup in Development. It does not delete the database. If deployment tooling owns schema changes, apply migrations there instead.

The design-time factory reads the same module configuration when EF tools run:

```csharp title="CustomersDbContextFactory.cs"
namespace Customers.Web;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class CustomersDbContextFactory : SqlServerModuleDbContextFactory<CustomersDbContext>
{
    public CustomersDbContextFactory()
        : base("Modules:customers:ConnectionStrings:Default",
            (options, connectionString) => options.UseSqlServer(connectionString))
    {
    }
}
```

With the app stopped, create the first schema migration:

```powershell
dotnet ef migrations add InitialCustomers --context CustomersDbContext
```

!!! verify "Check it"

    Start the app in Development. The migrator runs after the host starts. Wait for the Customers table to appear in SQL Server, then request the module status route. No customer data or previous migration was required before this step.

<!-- agent-prompt 6 source="docs/features-modules.md" -->

---

## Register the Customer repository { #step-7 .tutorial-step data-step="07" data-toc-label="07 Repository" }

The repository exposes persistence through the domain abstraction. Add this registration to `CustomersModule.Register` after the context registration:

```csharp
services.AddEntityFrameworkRepository<Customer, CustomersDbContext>()
    .WithBehavior<RepositoryLoggingBehavior<Customer>>();
```

Add `using BridgingIT.DevKit.Domain.Repositories;` to the module. Application handlers can now depend on `IGenericRepository<Customer>`.

!!! verify "Check it"

    Build and start the app. The existing status routes still respond. The next steps add the use cases and HTTP routes that exercise the repository.

[Repositories](reference/features-domain-repositories.md) covers querying, behaviors, and provider capabilities.

<!-- agent-prompt 7 topics="repositories" -->

---

## Add commands, queries, and validation { #step-8 .tutorial-step data-step="08" data-toc-label="08 Handlers" }

Add a command for creating customers and a query for finding one. The handlers use the repository from the start.

??? example "CustomerCreateCommand.cs"

    ```csharp title="Modules/Customers/CustomerCreateCommand.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Common;
    using FluentValidation;
    using BridgingIT.DevKit.Domain.Repositories;

    public class CustomerCreateCommand : RequestBase<Customer>
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public class Validator : AbstractValidator<CustomerCreateCommand>
        {
            public Validator()
            {
                this.RuleFor(c => c.FirstName).NotEmpty();
                this.RuleFor(c => c.LastName).NotEmpty();
                this.RuleFor(c => c.Email).NotEmpty().EmailAddress();
            }
        }
    }

    public class CustomerCreateCommandHandler(IGenericRepository<Customer> repository)
        : RequestHandlerBase<CustomerCreateCommand, Customer>
    {
        protected override async Task<Result<Customer>> HandleAsync(
            CustomerCreateCommand request, SendOptions options, CancellationToken cancellationToken)
        {
            var customer = Customer.Create(request.FirstName, request.LastName, request.Email);
            await repository.InsertAsync(customer, cancellationToken);

            return customer;
        }
    }
    ```

??? example "CustomerFindOneQuery.cs"

    ```csharp title="Modules/Customers/CustomerFindOneQuery.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Common;
    using BridgingIT.DevKit.Domain.Repositories;

    public class CustomerFindOneQuery(Guid id) : RequestBase<Customer>
    {
        public Guid Id { get; } = id;
    }

    public class CustomerFindOneQueryHandler(IGenericRepository<Customer> repository)
        : RequestHandlerBase<CustomerFindOneQuery, Customer>
    {
        protected override async Task<Result<Customer>> HandleAsync(
            CustomerFindOneQuery request, SendOptions options, CancellationToken cancellationToken)
        {
            return await repository.FindOneResultAsync(
                request.Id, cancellationToken: cancellationToken);
        }
    }
    ```

In `Program.cs`, before `builder.Build()`, register the requester and validation behavior:

```csharp
builder.Services.AddRequester()
    .AddHandlers()
    .WithBehavior(typeof(ValidationPipelineBehavior<,>));
```

!!! verify "Check it"

    Run `dotnet build`. The handlers, validator, and repository registration compile together. Step 9 exposes them through the API.

[Commands and queries](reference/features-application-commands-queries.md), [Requester and Notifier](reference/features-requester-notifier.md), and [Results](reference/features-results.md).

<!-- agent-prompt 8 topics="commands_queries requester_notifier results" -->

---

## Add the Customer endpoints { #step-9 .tutorial-step data-step="09" data-toc-label="09 Endpoints" }

The endpoints map HTTP requests to the handlers and their results to status codes.

??? example "CustomerEndpoints.cs"

    ```csharp title="Modules/Customers/CustomerEndpoints.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Common;
    using BridgingIT.DevKit.Presentation.Web;
    using Microsoft.AspNetCore.Mvc;

    public class CustomerEndpoints : EndpointsBase
    {
        public override void Map(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/customers").WithTags("Customers");

            group.MapGet("/{id:guid}", async (
                Guid id,
                [FromServices] IRequester requester,
                CancellationToken cancellationToken) =>
            {
                var query = new CustomerFindOneQuery(id);
                var result = await requester.SendAsync(
                    query, cancellationToken: cancellationToken);

                return result.MapHttpOk();
            });

            group.MapPost("/", async (
                [FromBody] CustomerCreateCommand command,
                [FromServices] IRequester requester,
                CancellationToken cancellationToken) =>
            {
                var result = await requester.SendAsync(
                    command, cancellationToken: cancellationToken);

                return result.MapHttpCreated(customer => $"/api/customers/{customer.Id}");
            });
        }
    }
    ```

Add `services.AddEndpoints<CustomerEndpoints>();` in `CustomersModule.Register`. Add `app.MapEndpoints();` in `Program.cs` after `app.MapModules();`.

!!! verify "Check it"

    ```powershell
    $customer = Invoke-RestMethod http://localhost:5199/api/customers -Method Post -ContentType application/json -Body '{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.test"}'
    Invoke-RestMethod "http://localhost:5199/api/customers/$($customer.id)"
    ```

    Expect `201` when creating a customer and `200` when reading it. Restart the app and read the same ID to confirm SQL Server persistence. An empty last name or invalid email returns `400`. A missing ID returns `404`.

[Endpoints](reference/features-presentation-endpoints.md) covers endpoint discovery and result mapping.

<!-- agent-prompt 9 source="docs/features-presentation-endpoints.md" -->

---

## Publish a domain event { #step-10 .tutorial-step data-step="10" data-toc-label="10 Events" }

Add this step when creating a customer should trigger a separate reaction. Register the event in `Customer.Create` before returning the new customer:

```csharp
customer.DomainEvents.Register(new CustomerCreatedDomainEvent(customer.Id));
```

??? example "Event and handler"

    ```csharp title="CustomerCreatedDomainEvent.cs"
    namespace Customers.Web;

    using BridgingIT.DevKit.Domain;

    public class CustomerCreatedDomainEvent(Guid customerId) : DomainEventBase
    {
        public Guid CustomerId { get; } = customerId;
    }
    ```

    ```csharp title="Modules/Customers/CustomerCreatedDomainEventHandler.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Domain;

    public class CustomerCreatedDomainEventHandler(ILoggerFactory loggerFactory)
        : DomainEventHandlerBase<CustomerCreatedDomainEvent>(loggerFactory)
    {
        public override bool CanHandle(CustomerCreatedDomainEvent @event) => true;

        public override Task Process(CustomerCreatedDomainEvent @event, CancellationToken cancellationToken)
        {
            this.Logger.LogInformation("customer {CustomerId} created", @event.CustomerId);

            return Task.CompletedTask;
        }
    }
    ```

Register the notifier before `builder.Build()`:

```csharp
builder.Services.AddNotifier()
    .AddHandlers();
```

Extend the repository registration to publish events after saving:

```csharp
services.AddEntityFrameworkRepository<Customer, CustomersDbContext>()
    .WithBehavior<RepositoryLoggingBehavior<Customer>>()
    .WithBehavior<RepositoryDomainEventPublisherBehavior<Customer>>();
```

!!! verify "Check it"

    Repeat the create/read check. The customer is stored and the event handler runs after the insert. Domain events remain internal to the application. This step introduces no schema change.

[Domain events](reference/features-domain-events.md) covers immediate publication. The [outbox recipe](getting-started-existing-app-features.md#domain-event-outbox) adds durable publication when needed.

<!-- agent-prompt 10 topics="domain_events requester_notifier" -->

<span id="what-you-have-now"></span>

## Complete setup

??? example "Program.cs"

    ```csharp title="Program.cs"
    using BridgingIT.DevKit.Common;
    using BridgingIT.DevKit.Presentation.Web;
    using Customers.Web.Modules.Customers;

    var builder = DevKitWebApplication.CreateBuilder(args);

    builder.Services.AddModules(builder.Configuration, builder.Environment, modules => modules
        .WithModule<CustomersModule>());

    builder.Services.AddRequester()
        .AddHandlers()
        .WithBehavior(typeof(ValidationPipelineBehavior<,>));

    builder.Services.AddNotifier()
        .AddHandlers();

    var app = builder.Build();

    app.UseRequestModuleContext();
    app.UseModules();
    app.MapModules();
    app.MapEndpoints();

    app.MapGet("/", () => "Customers API is running.");

    app.Run();
    ```

??? example "CustomersModule.cs"

    ```csharp title="Modules/Customers/CustomersModule.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Common;
    using BridgingIT.DevKit.Domain.Repositories;
    using BridgingIT.DevKit.Infrastructure.EntityFramework;

    public class CustomersModule : WebModuleBase
    {
        private CustomersModuleConfiguration moduleConfiguration = null!;

        public override IServiceCollection Register(
            IServiceCollection services,
            IConfiguration? configuration = null,
            IWebHostEnvironment? environment = null)
        {
            this.moduleConfiguration = this.Configure<CustomersModuleConfiguration,
                CustomersModuleConfiguration.Validator>(services, configuration);

            services.AddSqlServerDbContext<CustomersDbContext>(options => options
                    .UseConnectionString(this.moduleConfiguration.ConnectionStrings["Default"]))
                .WithDatabaseMigratorService(options => options
                    .Enabled(environment?.IsDevelopment() == true)
                    .HaltOnFailure());

            services.AddEntityFrameworkRepository<Customer, CustomersDbContext>()
                .WithBehavior<RepositoryLoggingBehavior<Customer>>()
                .WithBehavior<RepositoryDomainEventPublisherBehavior<Customer>>();

            services.AddEndpoints<CustomerEndpoints>();

            return services;
        }

        public override IApplicationBuilder Use(
            IApplicationBuilder app,
            IConfiguration? configuration = null,
            IWebHostEnvironment? environment = null) => app;

        public override IEndpointRouteBuilder Map(
            IEndpointRouteBuilder app,
            IConfiguration? configuration = null,
            IWebHostEnvironment? environment = null)
        {
            app.MapGet("/api/customers/status", () => Results.Ok(new
            {
                module = this.Name,
                message = this.moduleConfiguration.Greeting,
            })).WithTags("Customers");

            return app;
        }
    }
    ```

Keep domain types in Domain, handlers in Application, persistence in Infrastructure, and endpoints and module composition in Presentation when you split into layer projects. The guide keeps one project so each addition is visible.

## Use an agent { #hand-to-an-agent data-toc-label="Use an agent" }

Start with the minimal API in step 1 and connect bdk MCP in step 2. Then use these prompts to plan and build the features. Keep the connection string pointed at a dedicated development SQL Server database.

??? prompt "1. Plan the change"

    ```text
    Plan how to build up this Web API with bITdevKit. Do not change application code yet.

    Read "Build an app with DevKit" with bdk_docs_get, source docs/site/getting-started-existing-app.md. If the response is truncated, read the full document from its returned URL. Use the bdk MCP connection established in step 2. Call bdk_guidance_list and bdk_guidance_get for modules, domain, repositories, commands_queries, requester_notifier, and domain_events. Verify proposed DevKit signatures with bdk_api_search and bdk_api_get.

    Inspect the host, target framework, nullable setting, configuration files, and existing routes. Start from a minimal Web API. Do not assume an entity, DbContext, repository, database, or EF migration already exists.

    Write devkit-plan.md with:
    1. Names: the app project, namespace, module, entity, context, routes, and URL. Use the guide's Customer feature or map it to a feature I requested.
    2. Configuration: the Modules:customers section, its typed configuration, the SQL Server instance, and a dedicated development database. Preserve unrelated configuration.
    3. Steps: the files each of the ten steps adds or changes. Mark existing equivalent capabilities as complete. Domain events are optional.
    4. Checks: the root route first, configured module status after step 4, SQL Server schema after step 6, and customer create/read, persistence, validation, and not-found behavior after step 9.
    5. Rules: change one step at a time, preserve existing working features, keep DevKit packages on the same version, and stop if a required API is unavailable or a check fails. Create the InitialCustomers migration in step 6. Never delete a database or alter unrelated tables. Use environment configuration for SQL credentials. Stop application processes started for a check. Never commit devkit-plan.md.

    Stop for my review of the plan before implementing the remaining steps.
    ```

??? prompt "2. Apply the steps"

    ```text
    Build up this Web API following devkit-plan.md and "Build an app with DevKit". If the plan is missing, run the planning prompt first and wait for my review.

    Use a branch for this work. Work through unchecked steps in order. Read each step and its references with bdk_docs_get; use bdk_docs_search for additional guides. Read the matching bdk_guidance_get topics and verify DevKit signatures with bdk_api_search and bdk_api_get. If documentation is truncated, read the full document from its returned URL.

    Start from the minimal app, then add the DevKit builder, module with appsettings configuration, entity, SQL Server context, repository, handlers, endpoints, and optional domain event. Do not convert existing entities or persistence just to reproduce the sample.

    Build after each step and run its check from the plan. The database first appears in step 6. Preserve unrelated routes, features, and data. Record each result and stop processes used for the check. Leave changes uncommitted for review and report the files changed, checks, and any deviations.
    ```

<span id="add-features"></span>

## Choose the next step

Add the capabilities your application needs through the independent [feature recipes](getting-started-existing-app-features.md). Logging, request correlation, background work, storage, dashboards, and AI runtime tools can be enabled separately.

<nav class="next-step-strip" aria-label="Next steps">
  <a href="../getting-started-existing-app-features/"><small>one feature at a time</small>Add features</a>
  <a href="../architecture/"><small>understand the boundaries</small>Architecture</a>
  <a href="../docs/"><small>find a feature guide</small>Documentation overview</a>
</nav>
