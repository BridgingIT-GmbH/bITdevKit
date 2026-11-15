---
title: Add DevKit to an existing app
hide:
  - footer
---

<!-- markdownlint-disable-file MD033 -->

!!! path-b "Path B: existing app"

    [**01** Basics](getting-started-existing-app.md){ aria-current="page" }
    [**02** Add features](getting-started-existing-app-features.md)
    <span class="switch">Starting from scratch? Use [Path A: new solution](getting-started-template.md).</span>
    { .path-pager }

# Add DevKit to an existing app

Connect your agent to [bdk MCP](#step-1), then swap the host builder and add the features your app needs. The examples use a Customer API with EF Core and SQLite. Each step keeps the app working.

<div class="host-stack">
  <div class="host-stack__caption"><span>Your host, step by step</span><span>Each row links to its step</span></div>
  <ol>
    <li class="is-base"><a href="#step-1"><span class="host-stack__n">01</span><img class="bdk-doc-icon" src="assets/features/mcp.svg" width="18" height="18" alt=""><span class="host-stack__name">bdk MCP</span><code class="host-stack__api">dotnet tool run bdk mcp</code><span class="host-stack__tag">agent tools</span></a></li>
    <li><a href="#step-2"><span class="host-stack__n">02</span><img class="bdk-doc-icon" src="assets/features/web-host.svg" width="18" height="18" alt=""><span class="host-stack__name">DevKit host</span><code class="host-stack__api">DevKitWebApplication.CreateBuilder(args)</code><span class="host-stack__tag">1 line</span></a></li>
    <li><a href="#step-3"><span class="host-stack__n">03</span><img class="bdk-doc-icon" src="assets/features/logging.svg" width="18" height="18" alt=""><span class="host-stack__name">Logging</span><code class="host-stack__api">.AddLogging()</code><span class="host-stack__tag">1 package</span></a></li>
    <li><a href="#step-4"><span class="host-stack__n">04</span><img class="bdk-doc-icon" src="assets/features/correlation-ids.svg" width="18" height="18" alt=""><span class="host-stack__name">Requests</span><code class="host-stack__api">UseRequestCorrelation(), UseRequestLogging()</code><span class="host-stack__tag">no package</span></a></li>
    <li><a href="#step-5"><span class="host-stack__n">05</span><img class="bdk-doc-icon" src="assets/features/endpoints.svg" width="18" height="18" alt=""><span class="host-stack__name">Endpoints</span><code class="host-stack__api">AddEndpoints&lt;CustomerEndpoints&gt;(), MapEndpoints()</code><span class="host-stack__tag">1 file</span></a></li>
    <li><a href="#step-6"><span class="host-stack__n">06</span><img class="bdk-doc-icon" src="assets/features/requester.svg" width="18" height="18" alt=""><span class="host-stack__name">Handlers</span><code class="host-stack__api">AddRequester(), IRequester.SendAsync</code><span class="host-stack__tag">2 files</span></a></li>
    <li><a href="#step-7"><span class="host-stack__n">07</span><img class="bdk-doc-icon" src="assets/features/modules.svg" width="18" height="18" alt=""><span class="host-stack__name">Module</span><code class="host-stack__api">AddModules(...), UseModules(), MapModules()</code><span class="host-stack__tag">1 file</span></a></li>
    <li><a href="#step-8"><span class="host-stack__n">08</span><img class="bdk-doc-icon" src="assets/features/provider-entityframework.svg" width="18" height="18" alt=""><span class="host-stack__name">DbContext</span><code class="host-stack__api">AddSqliteDbContext(...).WithDatabaseMigratorService()</code><span class="host-stack__tag">1 package</span></a></li>
    <li><a href="#step-9"><span class="host-stack__n">09</span><img class="bdk-doc-icon" src="assets/features/repositories.svg" width="18" height="18" alt=""><span class="host-stack__name">Repository</span><code class="host-stack__api">AddEntityFrameworkRepository&lt;Customer, CustomersDbContext&gt;()</code><span class="host-stack__tag">4 files</span></a></li>
    <li><a href="#step-10"><span class="host-stack__n">10</span><img class="bdk-doc-icon" src="assets/features/domain.svg" width="18" height="18" alt=""><span class="host-stack__name">Domain</span><code class="host-stack__api">Customer.Create(...), AddNotifier().AddHandlers()</code><span class="host-stack__tag">2 files</span></a></li>
    <li class="is-next"><a href="../getting-started-existing-app-features/"><span class="host-stack__n">+</span><span></span><span class="host-stack__name">Features</span><span class="host-stack__icons"><img class="bdk-doc-icon" src="assets/features/notifications.svg" width="16" height="16" alt=""><img class="bdk-doc-icon" src="assets/features/queueing.svg" width="16" height="16" alt=""><img class="bdk-doc-icon" src="assets/features/messaging.svg" width="16" height="16" alt=""><img class="bdk-doc-icon" src="assets/features/orchestrations.svg" width="16" height="16" alt=""><img class="bdk-doc-icon" src="assets/features/jobs.svg" width="16" height="16" alt=""><img class="bdk-doc-icon" src="assets/features/storage.svg" width="16" height="16" alt=""><img class="bdk-doc-icon" src="assets/features/dashboard.svg" width="16" height="16" alt=""></span><span class="host-stack__tag">on the next page</span></a></li>
  </ol>
</div>


Each step ends with a copyable **AI prompt**. For the whole guide, use the [planning and apply prompts](#hand-to-an-agent).

---

## Install bdk and connect MCP { #step-1 .tutorial-step data-step="01" data-toc-label="01 MCP" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/mcp.svg" width="16" height="16" alt="">Agent docs tools</span>
  <span><b>Tool</b><code>BridgingIT.DevKit.Cli</code></span>
  <span><b>Touches</b>tool manifest, MCP config</span>
</div>

Install the local `bdk` CLI so your agent can search DevKit guides, get feature guidance, and check API signatures before changing the app. These tools work without a running DevKit host.

Solutions created from the [DevKit template](getting-started-template.md) already have bdk MCP installed and configured.

Run from your app's repository root in PowerShell 7.3 or later:

```powershell
if (-not (Test-Path .config/dotnet-tools.json)) { dotnet new tool-manifest }
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

Use your existing .NET 10 ASP.NET Core app. Before changing the host, confirm its create and read routes work. These examples use `POST /api/customers` and `GET /api/customers/{id}`.

Replace the port with your app's URL. With controllers, skip step 5 and omit `AddEndpoints` from the module.

!!! verify "Check it"

    ```powershell
    $customer = Invoke-RestMethod http://localhost:5199/api/customers -Method Post -ContentType application/json -Body '{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.test"}'
    Invoke-RestMethod "http://localhost:5199/api/customers/$($customer.id)"
    ```

    Expect a created customer and a successful read with the same fields. Repeat this check after each step.

??? example "Reference app used in the examples"

    The sample already has an EF Core migration, the local `dotnet-ef` tool, and a `Customers` connection string. Use your existing setup in your app.

    ```csharp title="Program.cs"
    using Microsoft.EntityFrameworkCore;
    using Customers.Web;

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddDbContext<CustomersDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("Customers")));

    var app = builder.Build();

    app.MapGet("/api/customers/{id:guid}", async (Guid id, CustomersDbContext db, CancellationToken cancellationToken) =>
        await db.Customers.FindAsync([id], cancellationToken) is { } customer
            ? Results.Ok(customer)
            : Results.NotFound());

    app.MapPost("/api/customers", async (Customer customer, CustomersDbContext db, ILogger<Program> logger, CancellationToken cancellationToken) =>
    {
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("customer {CustomerId} created", customer.Id);

        return Results.Created($"/api/customers/{customer.Id}", customer);
    });

    app.Run();
    ```

    ```csharp title="Customer.cs"
    namespace Customers.Web;

    public class Customer
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }
    ```

    ```csharp title="CustomersDbContext.cs"
    namespace Customers.Web;

    using Microsoft.EntityFrameworkCore;

    public class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => this.Set<Customer>();
    }
    ```

    ```json title="appsettings.json"
    {
      "ConnectionStrings": {
        "Customers": "Data Source=customers.db"
      }
    }
    ```


<!-- agent-prompt 1 source="docs/features-cli-mcp-clients.md" -->

---

## Swap the host builder { #step-2 .tutorial-step data-step="02" data-toc-label="02 Host" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/web-host.svg" width="16" height="16" alt="">DevKit host</span>
  <span><b>Packages</b><code>BridgingIT.DevKit.Presentation.Web</code></span>
  <span><b>Touches</b><code>Program.cs</code></span>
</div>

Install the host package and replace `WebApplication.CreateBuilder`. Keep the existing services, middleware, and routes.

```powershell
dotnet add package BridgingIT.DevKit.Presentation.Web
```

```csharp title="Program.cs"
using BridgingIT.DevKit.Presentation.Web;

var builder = DevKitWebApplication.CreateBuilder(args);
```

Keep all DevKit packages on the same version. If your app uses Central Package Management, add them to `Directory.Packages.props`.

!!! verify "Check it"

    Restart the app and repeat the create/read check. The responses stay the same.

[Host reference](reference/features-presentation.md). For an API that expects `IHostApplicationBuilder`, pass `builder.WebApplicationBuilder`.

??? note "Find the host with the bdk CLI"

    ```powershell
    dotnet tool run bdk hosts list
    ```

    In `Development`, the running host appears as `Ready`. This uses the CLI installed in step 1.

<!-- agent-prompt 2 source="docs/features-presentation.md" -->


---

## Add DevKit logging { #step-3 .tutorial-step data-step="03" data-toc-label="03 Logging" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/logging.svg" width="16" height="16" alt="">Logging</span>
  <span><b>Packages</b><code>BridgingIT.DevKit.Presentation.Serilog</code></span>
  <span><b>Touches</b><code>Program.cs</code>, <code>appsettings.json</code></span>
</div>

Install Serilog support and chain `.AddLogging()`. Keep your existing Serilog setup if the app already has one.

```powershell
dotnet add package BridgingIT.DevKit.Presentation.Serilog
```

```csharp title="Program.cs"
var builder = DevKitWebApplication.CreateBuilder(args)
    .AddLogging();
```

??? note "Console logging configuration"

    Add this section to `appsettings.json`. A sink such as `Console` is required.

    ```json title="appsettings.json"
    "Serilog": {
      "MinimumLevel": {
        "Default": "Information",
        "Override": {
          "Microsoft.AspNetCore": "Warning",
          "Microsoft.EntityFrameworkCore": "Warning"
        }
      },
      "WriteTo": [
        {
          "Name": "Console",
          "Args": {
            "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} cid:{CorrelationId}{NewLine}{Exception}"
          }
        }
      ],
      "Enrich": ["FromLogContext"]
    }
    ```

!!! verify "Check it"

    Restart the app and repeat the create/read check.

[Logging reference](reference/features-presentation-logging.md). `.AddLogging()` leaves an already configured Serilog logger in place.

<!-- agent-prompt 3 source="docs/features-presentation-logging.md" -->


---

## Correlate and log requests { #step-4 .tutorial-step data-step="04" data-toc-label="04 Requests" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/correlation-ids.svg" width="16" height="16" alt="">Request correlation</span>
  <span><b>Packages</b>none new</span>
  <span><b>Touches</b><code>Program.cs</code></span>
</div>

Add correlation and request logging after `builder.Build()`, before authentication and endpoints. If you use explicit `UseRouting()`, put it before request logging.

```csharp title="Program.cs"
app.UseRequestCorrelation();
app.UseRequestLogging();
```

!!! verify "Check it"

    ```powershell
    curl.exe -i -H "CorrelationId: customers-test-1" -X POST http://localhost:5199/api/customers -H "Content-Type: application/json" -d '{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.test"}'
    ```

    Expect `201 Created` and `CorrelationId: customers-test-1` in the response headers.

[Correlation IDs](reference/features-presentation-correlationid.md) and [request logging](reference/features-presentation-logging.md).

<!-- agent-prompt 4 source="docs/features-presentation-correlationid.md" -->


---

## Move routes into an endpoints class { #step-5 .tutorial-step data-step="05" data-toc-label="05 Endpoints" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/endpoints.svg" width="16" height="16" alt="">Endpoints</span>
  <span><b>Packages</b>none new</span>
  <span><b>Touches</b><code>Program.cs</code>, 1 new file</span>
</div>

Move the existing routes into `CustomerEndpoints.Map`. Register the class before `builder.Build()`, and replace the inline route mappings with `app.MapEndpoints()`.

```csharp title="Program.cs"
using Customers.Web.Modules.Customers;

builder.Services.AddEndpoints<CustomerEndpoints>();
```

```csharp title="Program.cs"
app.MapEndpoints();
```

??? example "CustomerEndpoints.cs"

    ```csharp title="Modules/Customers/CustomerEndpoints.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Presentation.Web;

    public class CustomerEndpoints : EndpointsBase
    {
        public override void Map(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/customers").WithTags("Customers");

            group.MapGet("/{id:guid}", async (Guid id, CustomersDbContext db, CancellationToken cancellationToken) =>
                await db.Customers.FindAsync([id], cancellationToken) is { } customer
                    ? Results.Ok(customer)
                    : Results.NotFound());

            group.MapPost("/", async (Customer customer, CustomersDbContext db, ILogger<CustomerEndpoints> logger, CancellationToken cancellationToken) =>
            {
                db.Customers.Add(customer);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("customer {CustomerId} created", customer.Id);

                return Results.Created($"/api/customers/{customer.Id}", customer);
            });
        }
    }
    ```

!!! verify "Check it"

    Repeat the create/read check. The routes and response bodies stay the same.

[Endpoints reference](reference/features-presentation-endpoints.md).

<!-- agent-prompt 5 source="docs/features-presentation-endpoints.md" -->


---

## Handle requests with commands and queries { #step-6 .tutorial-step data-step="06" data-toc-label="06 Handlers" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/requester.svg" width="16" height="16" alt="">Requester</span>
  <span><b>Packages</b>none new</span>
  <span><b>Touches</b><code>Program.cs</code>, <code>CustomerEndpoints.cs</code>, 2 new files</span>
</div>

Add a command and query, move the route logic into their handlers, and let the endpoints dispatch through `IRequester`.

```csharp title="Program.cs"
using BridgingIT.DevKit.Common;

builder.Services.AddRequester()
    .AddHandlers()
    .WithBehavior(typeof(ValidationPipelineBehavior<,>));
```

??? example "Command and query files"

    ```csharp title="Modules/Customers/CustomerCreateCommand.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Common;
    using FluentValidation;

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

    public class CustomerCreateCommandHandler(
        CustomersDbContext dbContext,
        ILogger<CustomerCreateCommandHandler> logger)
        : RequestHandlerBase<CustomerCreateCommand, Customer>
    {
        protected override async Task<Result<Customer>> HandleAsync(
            CustomerCreateCommand request, SendOptions options, CancellationToken cancellationToken)
        {
            var customer = new Customer
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
            };
            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("customer {CustomerId} created", customer.Id);

            return customer;
        }
    }
    ```

    ```csharp title="Modules/Customers/CustomerFindOneQuery.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Common;

    public class CustomerFindOneQuery(Guid id) : RequestBase<Customer>
    {
        public Guid Id { get; } = id;
    }

    public class CustomerFindOneQueryHandler(CustomersDbContext dbContext)
        : RequestHandlerBase<CustomerFindOneQuery, Customer>
    {
        protected override async Task<Result<Customer>> HandleAsync(
            CustomerFindOneQuery request, SendOptions options, CancellationToken cancellationToken)
        {
            var customer = await dbContext.Customers.FindAsync([request.Id], cancellationToken);

            return customer is null
                ? Result<Customer>.Failure(new NotFoundError($"customer {request.Id} not found"))
                : Result<Customer>.Success(customer);
        }
    }
    ```

The endpoint maps each result to HTTP. A successful command returns `201`, a successful query returns `200`, validation returns `400`, and a missing customer returns `404`.

??? example "Updated endpoint mappings"

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

!!! verify "Check it"

    Repeat the create/read check. Send an empty `lastName` or an invalid `email` and expect `400`. Request an unknown customer ID and expect `404`.

[Commands and queries](reference/features-application-commands-queries.md), [requester](reference/features-requester-notifier.md), and [results](reference/features-results.md).

??? note "Source-generated handlers"

    `[Command]` or `[Query]` on a partial class with a `[Handle]` method can generate these types. That requires `BridgingIT.DevKit.Common.Utilities.CodeGen`. See the source-generator appendix in the [requester guide](reference/features-requester-notifier.md).

<!-- agent-prompt 6 topics="commands_queries requester_notifier results" -->


---

## Group the feature into a module { #step-7 .tutorial-step data-step="07" data-toc-label="07 Module" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/modules.svg" width="16" height="16" alt="">Module</span>
  <span><b>Packages</b>none new</span>
  <span><b>Touches</b><code>Program.cs</code>, 1 new file</span>
</div>

Move the context and endpoint registrations into `CustomersModule.Register`. Remove those two registrations from `Program.cs`, and add the module instead. Keep the shared requester in `Program.cs`.

```csharp title="Program.cs"
builder.Services.AddModules(builder.Configuration, builder.Environment, modules => modules
    .WithModule<CustomersModule>());
```

??? example "CustomersModule.cs"

    ```csharp title="Modules/Customers/CustomersModule.cs"
    namespace Customers.Web.Modules.Customers;

    using BridgingIT.DevKit.Common;
    using Microsoft.EntityFrameworkCore;

    public class CustomersModule : WebModuleBase
    {
        public override IServiceCollection Register(
            IServiceCollection services,
            IConfiguration? configuration = null,
            IWebHostEnvironment? environment = null)
        {
            services.AddDbContext<CustomersDbContext>(options =>
                options.UseSqlite(configuration?.GetConnectionString("Customers")));
            services.AddEndpoints<CustomerEndpoints>();

            return services;
        }

        public override IApplicationBuilder Use(
            IApplicationBuilder app,
            IConfiguration? configuration = null,
            IWebHostEnvironment? environment = null)
        {
            return app;
        }

        public override IEndpointRouteBuilder Map(
            IEndpointRouteBuilder app,
            IConfiguration? configuration = null,
            IWebHostEnvironment? environment = null)
        {
            return app;
        }
    }
    ```

Add module context before request logging. Keep the existing endpoint mapping:

```csharp title="Program.cs"
app.UseRequestCorrelation();
app.UseRequestModuleContext();
app.UseRequestLogging();
app.UseModules();
app.MapModules();
app.MapEndpoints();
```

!!! verify "Check it"

    Repeat the create/read check. The response now also includes `ModuleName: customers`.

??? note "Module conventions"

    Keep the feature namespace under `Customers.Web.Modules.Customers` so type-based lookups can identify the module. With nullable disabled, omit the `?` annotations in the module signatures.

    `Use` and `Map` are required overrides even when empty. Do not omit their host calls. Setting `Modules:customers:Enabled` to false rejects requests but still runs registration and module composition.

[Modules reference](reference/features-modules.md).

<!-- agent-prompt 7 topics="modules" -->


---

## Register the `DbContext` with DevKit { #step-8 .tutorial-step data-step="08" data-toc-label="08 DbContext" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/provider-entityframework.svg" width="16" height="16" alt="">DbContext</span>
  <span><b>Packages</b><code>BridgingIT.DevKit.Infrastructure.EntityFramework.Sqlite</code></span>
  <span><b>Touches</b><code>CustomersModule.cs</code></span>
</div>

Replace the module's `AddDbContext` registration with the DevKit registration for your database provider. The SQLite example uses:

```powershell
dotnet add package BridgingIT.DevKit.Infrastructure.EntityFramework.Sqlite
```

```csharp title="Modules/Customers/CustomersModule.cs"
services.AddSqliteDbContext<CustomersDbContext>(
        configuration?.GetConnectionString("Customers"))
    .WithDatabaseMigratorService();
```

Use the connection-string overload shown here. The context and model do not change.

Use a local development database while following this guide. If your deployment pipeline applies migrations, omit `WithDatabaseMigratorService()`. To stop the app after a migration failure, pass `options => options.HaltOnFailure()`.

??? note "Other database providers"

    ```powershell
    dotnet add package BridgingIT.DevKit.Infrastructure.EntityFramework.SqlServer
    # Or:
    dotnet add package BridgingIT.DevKit.Infrastructure.EntityFramework.Postgres
    ```

    Use the matching provider registration in your module. Keep the same connection string and existing migrations.

!!! verify "Check it"

    Restart the app and repeat the create/read check. Pending migrations apply at startup if you enabled the migrator.

[Persistence setup](reference/features-domain-repositories.md) and [module registration](reference/features-modules.md).

<!-- agent-prompt 8 source="docs/features-modules.md" -->


---

## Read and write through a repository { #step-9 .tutorial-step data-step="09" data-toc-label="09 Repository" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/repositories.svg" width="16" height="16" alt="">Repository</span>
  <span><b>Packages</b>none new</span>
  <span><b>Touches</b><code>Customer.cs</code>, <code>CustomersModule.cs</code>, 2 handler files</span>
</div>

Make `Customer` an `AggregateRoot<Guid>` and remove its own `Id` property. The base class supplies it. Keep the three customer properties.

??? example "Customer.cs"

    ```csharp title="Customer.cs"
    namespace Customers.Web;

    using BridgingIT.DevKit.Domain.Model;

    public class Customer : AggregateRoot<Guid>
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }
    ```

Register the repository in the module:

```csharp title="Modules/Customers/CustomersModule.cs"
using BridgingIT.DevKit.Domain.Repositories;

services.AddEntityFrameworkRepository<Customer, CustomersDbContext>()
    .WithBehavior<RepositoryLoggingBehavior<Customer>>();
```

Replace the handlers' `CustomersDbContext` dependencies with `IGenericRepository<Customer>`. `InsertAsync` saves the customer; `FindOneResultAsync` returns a failed result when the ID is missing.

??? example "Repository handler changes"

    Keep each command or query definition. Replace only its handler, and add the listed `using` to the file's imports.

    ```csharp title="Modules/Customers/CustomerCreateCommand.cs"
    using BridgingIT.DevKit.Domain.Repositories;

    public class CustomerCreateCommandHandler(
        IGenericRepository<Customer> repository,
        ILogger<CustomerCreateCommandHandler> logger)
        : RequestHandlerBase<CustomerCreateCommand, Customer>
    {
        protected override async Task<Result<Customer>> HandleAsync(
            CustomerCreateCommand request, SendOptions options, CancellationToken cancellationToken)
        {
            var customer = new Customer
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
            };
            await repository.InsertAsync(customer, cancellationToken);
            logger.LogInformation("customer {CustomerId} created", customer.Id);

            return customer;
        }
    }
    ```

    ```csharp title="Modules/Customers/CustomerFindOneQuery.cs"
    using BridgingIT.DevKit.Domain.Repositories;

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

!!! verify "Check it"

    Stop the app and run:

    ```powershell
    dotnet ef migrations has-pending-model-changes
    ```

    Expect no model changes. Restart and repeat the create/read and validation checks.

[Repositories](reference/features-domain-repositories.md) and [domain model](reference/features-domain.md).

<!-- agent-prompt 9 topics="repositories" -->


---

## Give `Customer` a domain model { #step-10 .tutorial-step data-step="10" data-toc-label="10 Domain" }

<div class="step-meta">
  <span class="is-feature"><b>Adds</b><img class="bdk-doc-icon" src="assets/features/domain.svg" width="16" height="16" alt="">Domain events</span>
  <span><b>Packages</b>none new</span>
  <span><b>Touches</b><code>Customer.cs</code>, <code>CustomersModule.cs</code>, <code>Program.cs</code>, the command handler, 2 new files</span>
</div>

Give `Customer` a factory that records `CustomerCreatedDomainEvent`. Keep the event beside the aggregate and handle side effects in a separate handler.

??? example "Customer and domain event files"

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
            customer.DomainEvents.Register(new CustomerCreatedDomainEvent(customer.Id));

            return customer;
        }
    }
    ```

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

Register the notifier in `Program.cs`, even if no event handlers are registered yet:

```csharp title="Program.cs"
builder.Services.AddNotifier()
    .AddHandlers();
```

Extend the existing repository registration to publish events after saving:

```csharp title="Modules/Customers/CustomersModule.cs"
services.AddEntityFrameworkRepository<Customer, CustomersDbContext>()
    .WithBehavior<RepositoryLoggingBehavior<Customer>>()
    .WithBehavior<RepositoryDomainEventPublisherBehavior<Customer>>();
```

In `CustomerCreateCommandHandler`, replace `new Customer { ... }` with the factory call. Keep the repository insert and returned result:

```csharp title="Modules/Customers/CustomerCreateCommand.cs"
var customer = Customer.Create(request.FirstName, request.LastName, request.Email);
await repository.InsertAsync(customer, cancellationToken);

return customer;
```

!!! verify "Check it"

    Run `dotnet ef migrations has-pending-model-changes` and expect no model changes. Restart and repeat the create/read check. The response fields stay the same; domain events remain internal.

[Domain events](reference/features-domain-events.md) explains immediate publication and the durable outbox option.

<!-- agent-prompt 10 topics="domain domain_events" -->


<span id="what-you-have-now"></span>

## Complete setup

??? example "Final Program.cs"

    ```csharp title="Program.cs"
    using BridgingIT.DevKit.Common;
    using BridgingIT.DevKit.Presentation.Web;
    using Customers.Web.Modules.Customers;

    var builder = DevKitWebApplication.CreateBuilder(args)
        .AddLogging();

    builder.Services.AddModules(builder.Configuration, builder.Environment, m => m
        .WithModule<CustomersModule>());
    builder.Services.AddRequester()
        .AddHandlers()
        .WithBehavior(typeof(ValidationPipelineBehavior<,>));
    builder.Services.AddNotifier()
        .AddHandlers();

    var app = builder.Build();

    app.UseRequestCorrelation();
    app.UseRequestModuleContext();
    app.UseRequestLogging();
    app.UseModules();
    app.MapModules();
    app.MapEndpoints();

    app.Run();
    ```

??? note "When your app splits into layers"

    Keep the aggregate and its events in Domain, the handlers in Application, the context and repository implementation in Infrastructure, and endpoints and module composition in Presentation. See [Architecture](architecture.md).

## Use an agent { #hand-to-an-agent data-toc-label="Use an agent" }

Use an agent that can edit files and run `dotnet`. Connect [bdk MCP in step 1](#step-1) before planning so the agent can consult the guides. Start on a clean branch and use a local development database. Review the plan, then apply it.

??? prompt "1. Plan the change"

    ```text
    Plan how to add bITdevKit to this application. Do not change any code yet.

    Read the guide "Add DevKit to an existing app" from start to end.
    Use the bdk MCP connection from step 1. Call bdk_docs_get with source docs/site/getting-started-existing-app.md. If the response is truncated, read the full document from its returned URL.
    If MCP is not connected, follow step 1 at https://bridgingit-gmbh.github.io/bITdevKit/getting-started-existing-app/#step-1 before planning.
    Call bdk_guidance_list, then use bdk_guidance_get for the relevant topics. Use bdk_docs_search and bdk_docs_get to resolve the app's differences from the guide. Verify proposed DevKit APIs with bdk_api_search and bdk_api_get using the returned uid.

    Inspect this repository:
    - the ASP.NET Core host project, its Program.cs, target framework and Nullable setting
    - how logging is set up and which middleware the pipeline uses
    - whether the routes are minimal APIs or controllers
    - the DbContext, its database provider and connection string name
    - whether the app uses EF Core migrations and the dotnet-ef tool, or creates its schema another way
    - the test projects, and any CI or deployment step that applies migrations

    Write devkit-plan.md in the repository root with five sections:
    1. Names. A table that maps each sample name in the guide to its name in this app. Cover Customers.Web, Customer, CustomersDbContext, the Customers feature folder and its namespace, the two routes, the URL the app listens on, and the provider package with its Add...DbContext method. Record the MCP client and its configuration file.
    2. Steps. For each of the ten steps, the files it changes and every deviation from the guide. Check at least these cases:
       - controllers instead of minimal APIs, which skips step 5
       - Serilog already set up before the host builds
       - a key type other than Guid
       - code outside the entity that sets its properties, which step 10's private setters break
       - a schema created without migrations, or migrations applied by a pipeline, which changes step 8
       - a database provider the guide does not cover
    3. Checks. Each step's check, rewritten for this app's routes, names and URL.
    4. Rules. The rules below, copied word for word.
    5. Checklist. Steps 1 to 10 as checkboxes. Tick step 1 after checking the MCP connection and existing routes. Tick another step only if the app already contains its change.

    If the app has more than one entity or feature area, pick one to start with and say why.
    Then stop and wait for my approval. I may edit the plan before you continue.

    Rules:
    - Before application changes, the working tree must be clean apart from devkit-plan.md and the tool manifest and MCP configuration from step 1. If it is not, stop and tell me.
    - Work on a branch made for this change, never on the default branch.
    - Change only what the current step changes. Routes, successful responses and the DbContext model stay as they are.
    - Install the latest stable BridgingIT.DevKit packages, all on the same version.
    - Never add an EF Core migration. After steps 9 and 10, dotnet ef migrations has-pending-model-changes must report no changes.
    - Run the app only against a local development database. Step 8 applies migrations at startup.
    - If a check fails, or the app needs a change the guide does not show, stop and report. Do not work around it.
    - If the guide names a type or method that the installed packages do not have, stop and report.
    - Stop every process you started before you finish a step.
    - Never commit devkit-plan.md.
    ```

??? prompt "2. Apply the steps"

    ```text
    Add bITdevKit to this application, following devkit-plan.md and the guide "Add DevKit to an existing app".
    If devkit-plan.md does not exist, stop and ask me to run the planning prompt first.
    Read the whole plan and follow its rules in every step.
    If you are not on a branch for this work, create one named feature/devkit-adoption.

    Work through the unchecked steps of the checklist in order. For each step:
    1. Use the bdk MCP connection from step 1. If it is unavailable, reconnect before editing. Read the step and its linked references with bdk_docs_get. If a response is truncated, read the full document from its returned URL.
    2. Read the step's linked feature guides with bdk_docs_get. Call bdk_guidance_get with topics commands_queries, requester_notifier, and results for step 6, modules for step 7, repositories for step 9, and domain and domain_events for step 10. Use bdk_docs_search for other feature guides. Verify DevKit signatures with bdk_api_search followed by bdk_api_get using the returned uid.
    3. Make the change with the names from the plan. Step 1 changes tooling configuration only.
    4. Build the whole solution, test projects included. Fix build errors this step caused. If the fix needs a change the guide does not show, stop and report.
    5. Start the app, run the step's check from the plan, and compare status codes, response headers and bodies with the guide. Stop the app.
    6. Tick the step in the checklist and add a one-line result.
    7. Commit the step on its own in this repository's commit message style. Leave out devkit-plan.md and do not push.

    After the last step, run the existing tests.
    Report the steps you finished, the files each step changed, each check result, and every deviation from the guide.
    ```

    To commit by hand, delete item 7 before you paste the prompt.

Each step above also has a prompt for that step alone.

<span id="add-features"></span>

## Choose the next step { data-toc-label="Next steps" }

Add only the features your app needs. [Add features](getting-started-existing-app-features.md) has independent recipes for background work, storage, and operations.

Optional host configuration sources are covered in [Presentation configuration](reference/features-presentation-configuration.md).

<nav class="next-step-strip" aria-label="Next steps">
  <a href="../getting-started-existing-app-features/"><small>one feature at a time</small>Add features</a>
  <a href="../architecture/"><small>understand the layers</small>Architecture</a>
  <a href="../docs/"><small>find a feature guide</small>Documentation overview</a>
  <a href="../guides/"><small>decide between options</small>Guides</a>
</nav>
