# Presentation Endpoints Feature Documentation

> Define minimal API endpoints as modular classes with automatic discovery and mapping.

[TOC]

## Overview

The Endpoints feature provides a clean, modular way to define Minimal API endpoints as composable classes. Each endpoint set implements a small contract and can be registered and mapped automatically, with configurable route groups, OpenAPI metadata, authorization, CORS, rate limiting, and discovery via DI scanning.

### Challenges

- Modularity: Keep endpoint definitions isolated per feature or module.
- Discovery: Register endpoints without manual wiring for each class.
- Consistency: Apply route grouping, endpoint metadata, authorization, CORS, and rate limiting consistently across endpoints.
- Observability: Log registration to aid debugging in multi-module apps.

### Solution

- Contracts: `IEndpoints` defines the minimal surface; `EndpointsBase` provides group and route-name helpers.
- Registration: DI helpers add endpoints by type, instance, or assembly scanning.
- Mapping: A single `MapEndpoints()` call maps all registered endpoints.
- Group configuration: `EndpointsOptionsBase` and `EndpointsOptionsBuilderBase` centralize group path, tags, OpenAPI metadata, authorization, CORS, rate limiting, route naming, and custom metadata.

## Core Contracts

- `IEndpoints` ([src/Presentation.Web/Endpoints/IEndpoints.cs](src/Presentation.Web/Endpoints/IEndpoints.cs))
  - `Enabled`: Toggle endpoint availability.
  - `IsRegistered`: Prevent duplicate mapping.
  - `Map(IEndpointRouteBuilder)`: Define routes.
- `EndpointsBase` ([src/Presentation.Web/Endpoints/EndpointsBase.cs](src/Presentation.Web/Endpoints/EndpointsBase.cs))
  - `MapGroup(app, options)`: Build a `RouteGroupBuilder` with path, tags, OpenAPI metadata, authorization, CORS, rate limiting, and custom metadata.
  - `BuildRouteName(options, name)`: Build endpoint names with an optional group-level route name prefix.
  - Common properties: `Enabled`, `IsRegistered`.
- Endpoint naming extensions ([src/Presentation.Web/Endpoints/EndpointConventionBuilderExtensions.cs](src/Presentation.Web/Endpoints/EndpointConventionBuilderExtensions.cs))
  - `WithName(options, name)`: Assign an endpoint name after internally calling `BuildRouteName(options, name)`.
- `EndpointsOptionsBase` ([src/Presentation.Web/Endpoints/EndpointsOptionsBase.cs](src/Presentation.Web/Endpoints/EndpointsOptionsBase.cs))
  - Routing and docs: `GroupPath`, `NormalizeGroupPath`, `GroupTag`, `GroupTags`, `GroupName`, `Summary`, `Description`, `Deprecated`, `ExcludeFromDescription`.
  - Authorization: `RequireAuthorization`, `AllowAnonymous`, `RequireRoles`, `RequirePolicy`, `RequireAuthenticationSchemes`.
  - Cross-cutting group policies: `RequireCorsPolicy`, `DisableCors`, `RequireRateLimitingPolicy`, `DisableRateLimiting`.
  - Extensibility: `RouteNamePrefix`, `Metadata`.
- `EndpointsOptionsBuilderBase<TOptions,TBuilder>` ([src/Presentation.Web/Endpoints/EndpointsOptionsBuilderBase.cs](src/Presentation.Web/Endpoints/EndpointsOptionsBuilderBase.cs))
  - Fluent builder base for concrete endpoint options builders.
  - Exposes shared methods such as `GroupPath(...)`, `GroupTag(...)`, `RequirePolicy(...)`, `RequireCorsPolicy(...)`, and `WithMetadata(...)`.
- `EndpointRouteNamePrefixMetadata` ([src/Presentation.Web/Endpoints/EndpointRouteNamePrefixMetadata.cs](src/Presentation.Web/Endpoints/EndpointRouteNamePrefixMetadata.cs))
  - Stores the configured route-name prefix as endpoint metadata.
  - Endpoint implementations still call `BuildRouteName(options, name)` when assigning names with `WithName(...)`.
- Registration helpers ([src/Presentation.Web/Endpoints/ServiceCollectionExtensions.cs](src/Presentation.Web/Endpoints/ServiceCollectionExtensions.cs))
  - `AddEndpoints(assemblies)` / `AddEndpoints(assembly)` / `AddEndpoints<T>()` / `AddEndpoints(endpoints)`.
  - Logs added endpoint types for visibility.
- Mapping extension ([src/Presentation.Web/Endpoints/ApplicationBuilderExtensions.cs](src/Presentation.Web/Endpoints/ApplicationBuilderExtensions.cs))
  - `MapEndpoints(WebApplication, RouteGroupBuilder?)`: Iterates `IEndpoints` in DI and calls `Map` when `Enabled && !IsRegistered`.

## Architecture

### Class Diagram

```mermaid
classDiagram
  class IEndpoints {
    +bool Enabled
    +bool IsRegistered
    +Map(IEndpointRouteBuilder)
  }
  class EndpointsBase
  class EndpointsOptionsBase {
    +bool Enabled
    +string GroupPath
    +bool NormalizeGroupPath
    +string GroupTag
    +string[] GroupTags
    +string GroupName
    +string Summary
    +string Description
    +bool Deprecated
    +string RouteNamePrefix
    +bool RequireAuthorization
    +bool AllowAnonymous
    +string[] RequireRoles
    +string RequirePolicy
    +string[] RequireAuthenticationSchemes
    +string RequireCorsPolicy
    +bool DisableCors
    +string RequireRateLimitingPolicy
    +bool DisableRateLimiting
    +bool ExcludeFromDescription
    +IList~object~ Metadata
  }
  class EndpointsOptionsBuilderBase~TOptions,TBuilder~
  class EndpointRouteNamePrefixMetadata {
    +string Prefix
  }
  class SystemEndpoints
  class SystemEndpointsOptions
  class LogEntryEndpoints
  class ServiceCollectionExtensions
  class ApplicationApplicationExtensions

  IEndpoints <|.. EndpointsBase
  EndpointsOptionsBase <|-- SystemEndpointsOptions
  EndpointsBase <|-- SystemEndpoints
  EndpointsBase <|-- LogEntryEndpoints
  EndpointsBase ..> EndpointsOptionsBase : configure group
  EndpointsBase ..> EndpointRouteNamePrefixMetadata : add metadata
  EndpointsOptionsBuilderBase~TOptions,TBuilder~ ..> EndpointsOptionsBase : builds
  ServiceCollectionExtensions ..> IEndpoints : register
  ApplicationApplicationExtensions ..> IEndpoints : map
```

### Sequence (Registration → Mapping)

```mermaid
sequenceDiagram
  participant Module as Module/Startup
  participant DI as IServiceCollection
  participant App as WebApplication
  participant Ext as MapEndpoints()
  participant Ep as IEndpoints

  Module->>DI: AddEndpoints<CoreTodoItemEndpoints>()
  Module->>DI: AddEndpoints<CoreEnumerationEndpoints>()
  Note over DI: Endpoints registered as IEndpoints (Singleton)
  Module->>App: build/start app
  App->>Ext: MapEndpoints()
  Ext->>Ep: foreach Enabled && !IsRegistered
  Ep-->>Ext: Map(routes)
  Ext-->>Ep: IsRegistered = true
```

## Getting Started

### DI Registration

Register endpoints explicitly by type, by instance, or via assembly scanning. Type-based registrations are added as singleton `IEndpoints` services.

```csharp
// Per type
services.AddEndpoints<CoreTodoItemEndpoints>();
services.AddEndpoints<CoreEnumerationEndpoints>();

// Or register all endpoint classes from assemblies
services.AddEndpoints(AppDomain.CurrentDomain.GetAssemblies());
```

### Mapping in the Pipeline

Map all registered endpoints once during app startup.

```csharp
// After building the WebApplication
app.MapEndpoints();

// Optionally map within a preconfigured RouteGroup
var api = app.MapGroup("/api");
app.MapEndpoints(api);
```

### Grouping and Authorization

Use `EndpointsBase.MapGroup(...)` with options to apply path, tags, OpenAPI metadata, authorization, CORS, rate limiting, and custom metadata consistently.

```csharp
public sealed class MyEndpoints(MyEndpointsOptions options) : EndpointsBase
{
  public override void Map(IEndpointRouteBuilder app)
  {
    var group = this.MapGroup(app, options);

    group.MapGet("items", () => Results.Ok())
      .WithName(options, "ListItems");
  }
}

public sealed class MyEndpointsOptions : EndpointsOptionsBase
{
  public MyEndpointsOptions()
  {
    GroupPath = "/api/items";
    GroupTag = "Items";
    GroupName = "items";
    RouteNamePrefix = "Items";
    RequireAuthorization = true;
  }
}
```

For fluent option configuration, derive a concrete builder from `EndpointsOptionsBuilderBase<TOptions,TBuilder>`:

```csharp
public sealed class MyEndpointsOptionsBuilder
    : EndpointsOptionsBuilderBase<MyEndpointsOptions, MyEndpointsOptionsBuilder>;

var options = new MyEndpointsOptionsBuilder()
  .GroupPath("/api/items")
  .GroupTag("Items")
  .GroupName("items")
  .RouteNamePrefix("Items")
  .RequirePolicy("Items.Read")
  .RequireCorsPolicy("PublicApi")
  .RequireRateLimitingPolicy("DefaultApi")
  .Build();
```

## Built-in Endpoints

- `SystemEndpoints` ([src/Presentation.Web/Endpoints/SystemEndpoints.cs](src/Presentation.Web/Endpoints/SystemEndpoints.cs))
  - Grouped under a configurable `GroupPath`; provides a root discovery route plus optional `echo`, `info`, and `modules` routes.
  - `HideSensitiveInformation` hides host, network, process, runtime, memory, URL, and timezone values from the `info` response.
  - Options: `SystemEndpointsOptions` ([src/Presentation.Web/Endpoints/SystemEndpointsOptions.cs](src/Presentation.Web/Endpoints/SystemEndpointsOptions.cs)).
- `LogEntryEndpoints` ([src/Presentation.Web/Logging/LogEntryEndpoints.cs](src/Presentation.Web/Logging/LogEntryEndpoints.cs))
  - Minimal logging-related endpoints; configured via `LogEntryEndpointsOptions`.

- `JobSchedulingEndpoints` ([src/Presentation.Web.JobScheduling/JobSchedulingEndpoints.cs](src/Presentation.Web.JobScheduling/JobSchedulingEndpoints.cs))
  - Job management endpoints: list jobs, get job details, runs (with filters), run stats, triggers.
  - Control endpoints: trigger, pause, resume, interrupt, purge runs.
  - Uses Minimal API method chains: `Produces<T>()`, `WithName(...)`, `WithDescription(...)` for metadata.

## Example Usage

Define a minimal endpoint using `EndpointsBase` that serves `GET /echo`:

```csharp
public sealed class EchoEndpoints(EchoEndpointsOptions options) : EndpointsBase
{
  public override void Map(IEndpointRouteBuilder app)
  {
    var group = this.MapGroup(app, options);

    group.MapGet("echo", ([FromQuery] string? message) =>
        TypedResults.Ok(new EchoModel{ Message = message ?? "pong" }))
      .WithName(options, "Get")
      .WithDescription("Simple echo endpoint.")
      .Produces<EchoModel>()
      .ProducesResultProblem(StatusCodes.Status400BadRequest);
  }
}

public sealed class EchoEndpointsOptions : EndpointsOptionsBase
{
  public EchoEndpointsOptions()
  {
    GroupPath = "/api/system";
    GroupTag = "System";
    RouteNamePrefix = "Echo";
    RequireAuthorization = true;
  }
}
```

From a module register endpoints by type:

```csharp
services.AddEndpoints<EchoEndpoints>();
```

Then map them once at app startup:

```csharp
app.MapEndpoints();
```

## Troubleshooting

- Endpoints not appearing: Ensure `app.MapEndpoints()` is called after DI registration.
- Endpoint mapped twice: `IsRegistered` prevents re-map for the same endpoint instance; check whether multiple endpoint instances were registered.
- Authorization not enforced: Verify `RequireAuthorization = true`, that `AllowAnonymous` is not enabled, and that endpoint routes are mapped through `MapGroup(app, options)`.
- Roles ignored: Blank role values are filtered out. Roles take precedence over `RequirePolicy` when both are configured.
- Policy ignored: Policy metadata is applied only when authorization is required and no non-blank roles are configured.
- Authentication scheme not used: `RequireAuthenticationSchemes` is applied only when `RequireAuthorization` is enabled. Blank scheme values are filtered out.
- CORS policy not applied: `DisableCors = true` takes precedence over `RequireCorsPolicy`.
- Rate limiting policy not applied: `DisableRateLimiting = true` takes precedence over `RequireRateLimitingPolicy`.
- Route names missing prefix: Use `.WithName(options, name)` or `BuildRouteName(options, name)` when calling `WithName(...)`; the prefix metadata is not applied automatically by ASP.NET Core.
- Discovery by assembly: Confirm assemblies contain concrete classes implementing `IEndpoints`.

## Appendix A — Minimal API Syntax

Endpoints follow ASP.NET Core Minimal API conventions via fluent mapping on `RouteGroupBuilder`.

```csharp
// Requester-based GET (Result<T> → HTTP)
group.MapGet("items", async ([FromServices] IRequester requester,
                              [FromQuery] FilterModel filter,
                              CancellationToken ct) =>
    (await requester
        .SendAsync(new ItemsFindAllQuery { Filter = filter }, cancellationToken: ct))
        .MapHttpOkAll())
  .Produces<IEnumerable<ItemModel>>()
  .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
  .WithName("Items.Get")
  .WithDescription("Retrieves items matching the optional filter.");

// Requester-based POST create (Result<T> → HTTP)
group.MapPost("items", async ([FromServices] IRequester requester,
                               [FromBody] ItemModel model,
                               CancellationToken ct) =>
    (await requester
        .SendAsync(new ItemCreateCommand { Model = model }, cancellationToken: ct))
        .MapHttpCreated(v => $"/api/items/{v.Id}"))
  .Produces<ItemModel>(StatusCodes.Status201Created)
  .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
  .WithName("Items.Create")
  .WithDescription("Creates a new item.");

// Requester-based DELETE (Result → HTTP)
group.MapDelete("items/{id}", async ([FromServices] IRequester requester,
                                      [FromRoute] string id,
                                      CancellationToken ct) =>
    (await requester
        .SendAsync(new ItemDeleteCommand(id), cancellationToken: ct))
        .MapHttpNoContent())
  .Produces(StatusCodes.Status204NoContent)
  .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
  .WithName("Items.Delete")
  .WithDescription("Deletes the specified item.");

// Requester signature (Result<T>)
// Task<Result<TValue>> SendAsync<TValue>(IRequest<TValue> request, SendOptions options = null, CancellationToken ct = default);
```

### Grouping and Authorization Options

- Build groups with `EndpointsBase.MapGroup(app, options)` for consistent path, tags, docs metadata, authorization, CORS, rate limiting, and custom metadata.
- Routing and OpenAPI options:
  - `GroupPath`: base route path for the group.
  - `NormalizeGroupPath = true`: normalize slashes, backslashes, and leading/trailing separators before the group is mapped. The stored `GroupPath` value is unchanged.
  - `GroupTag` and `GroupTags`: apply one or more OpenAPI tags.
  - `GroupName`, `Summary`, `Description`, `Deprecated`: apply OpenAPI group metadata.
  - `RouteNamePrefix`: shared prefix for endpoint names when endpoints use `.WithName(options, name)` or `BuildRouteName(options, name)`.
- Authorization options:
  - `RequireAuthorization = true`: enforce default authorization.
  - `RequireRoles = ["Admin", "Ops"]`: role-based access via AuthorizeAttribute roles.
  - `RequirePolicy = "PolicyName"`: policy-based access.
  - `RequireAuthenticationSchemes = ["Bearer"]`: restrict authorization to named authentication schemes.
  - `AllowAnonymous = true`: apply anonymous access metadata and skip group authorization metadata.
- Authorization precedence:
  - `AllowAnonymous` wins over all authorization settings.
  - Non-blank roles win over policy.
  - Policy wins over scheme-only authorization.
  - Scheme-only authorization is applied when schemes are configured without roles or policy.
  - Default authorization is applied when `RequireAuthorization = true` and no roles, policy, or schemes are configured.
- CORS and rate limiting options:
  - `RequireCorsPolicy = "PolicyName"`: apply a named CORS policy.
  - `DisableCors = true`: disable CORS for the group.
  - `RequireRateLimitingPolicy = "PolicyName"`: apply a named rate limiting policy.
  - `DisableRateLimiting = true`: disable rate limiting for the group.
- Extensibility options:
  - `Metadata`: attach arbitrary endpoint metadata to the whole group.
- `ExcludeFromDescription = true`: hide endpoints from OpenAPI description.

### Endpoint Registration and Mapping Lifecycle

1. Register endpoint instances, types, or assemblies with `AddEndpoints(...)`.
2. Build the `WebApplication`.
3. Call `app.MapEndpoints()` once during startup, or pass a parent `RouteGroupBuilder` to map all registered endpoint sets under an existing group.
4. `MapEndpoints()` resolves `IEndpoints` from DI and maps only instances where `Enabled && !IsRegistered`.
5. After `Map` is called, `IsRegistered` is set to `true` for that endpoint instance.

This lifecycle keeps endpoint classes modular while still making duplicate mapping explicit and avoidable.

### Endpoint System Test Coverage

The endpoint system is covered by focused unit tests in [tests/Presentation.UnitTests/Web/Endpoints](tests/Presentation.UnitTests/Web/Endpoints):

- `EndpointsBaseTests`: group metadata, authorization precedence, path normalization, CORS, rate limiting, route-name prefix metadata, and custom metadata.
- `EndpointsOptionsBuilderBaseTests`: fluent builder methods and null-array normalization.
- `EndpointRegistrationTests`: DI registration overloads, disabled registration, type registration, `MapEndpoints(...)`, duplicate prevention, and mapping into a supplied route group.

### Result and Metadata

- Map `Result<T>` to HTTP using helpers (e.g., `MapHttpOk()`, `MapHttpOkAll()`, `MapHttpCreated(...)`, `MapHttpNoContent()`).
- Add descriptive metadata with `Produces<T>()`, `WithName(...)`, `WithDescription(...)` to improve API docs.

### Result Mapping Helpers

The endpoint examples above use the built-in mapping helpers that convert `Result` and `Result<T>` to typed Minimal API responses. These live in:

- Result helpers: [src/Presentation.Web/Result/Map/ResultMapExtensions.cs](src/Presentation.Web/Result/Map/ResultMapExtensions.cs)
- Core mapping: [src/Presentation.Web/Result/Map/ResultMapHttpExtensions.cs](src/Presentation.Web/Result/Map/ResultMapHttpExtensions.cs)

#### Helper Overview

- **`MapHttpOk<T>(Result<T>)`**: Returns `Ok<T>` on success; maps errors to `Unauthorized`, `BadRequest`, or `Problem`. For single resources.
- **`MapHttpOkAll<T>(Result<T>)`**: Returns `Ok<T>` on success without `NotFound` branch (for collections where empty is valid).
- **`MapHttpNoContent(Result)` / `MapHttpNoContent(Result<T>)`**: Returns `NoContent` on success (e.g., DELETE); handles `NotFound` and other errors.
- **`MapHttpCreated<T>(Result<T>, string uri)`**: Returns `Created<T>` with the provided URI on success.
- **`MapHttpCreated<T>(Result<T>, Func<T,string>)`**: Builds the URI from the value only when successful.
- **`MapHttpAccepted(Result, string location)` / `MapHttpAccepted<T>(Result<T>, string)`**: Returns `Accepted` for long-running operations.
- **`MapHttpAccepted<T>(Result<T>, Func<T,string>)`**: Location factory variant for `Accepted<T>`.
- **`MapHttpOkPaged<T>(ResultPaged<T>)`**: Returns `Ok<PagedResponse<T>>` with paging metadata.
- **`MapHttpFile(Result<FileContent>)`**: Returns `FileContentHttpResult` for downloads; overload accepts `Func<FileContent,string>` to derive the file name.
- **Generic `Map` overloads**: Advanced mapping to `Results<...>` with explicit success shapes (`Ok`, `NoContent`, `Created<T>`) and error variants.

All helpers perform error-to-HTTP mapping and support optional logging via `ILogger`.

#### Usage Examples

```csharp
// Paged listing (ResultPaged<T> → HTTP)
group.MapGet("items", async ([FromServices] IRequester requester, [FromQuery] int page, [FromQuery] int size, CancellationToken ct) =>
    (await requester.SendAsync(new ItemsFindPagedQuery(page, size), cancellationToken: ct))
        .MapHttpOkPaged())
  .Produces<PagedResponse<ItemModel>>()
  .WithName("Items.GetPaged");

// Long-running job trigger (Result → HTTP 202 Accepted)
group.MapPost("jobs/{id}/run", async ([FromServices] IRequester requester, [FromRoute] string id, CancellationToken ct) =>
    (await requester.SendAsync(new JobRunCommand(id), cancellationToken: ct))
        .MapHttpAccepted($"/api/jobs/{id}/runs/last"))
  .Produces<Accepted>()
  .WithName("Jobs.Run");

// File export (Result<FileContent> → HTTP file)
group.MapGet("items/export", async ([FromServices] IRequester requester, CancellationToken ct) =>
    (await requester.SendAsync(new ItemsExportQuery(), cancellationToken: ct))
        .MapHttpFile(fc => $"items-export-{DateTime.UtcNow:yyyyMMdd}.csv"))
  .Produces<FileContentHttpResult>()
  .WithName("Items.Export");
```

#### Custom Error Handlers

You can plug in custom error-to-HTTP mapping for specific Result errors. Register handlers once at startup; the registry is consulted before default mapping.

```csharp
// At startup (e.g., Program.cs)
ResultMapHttpExtensions.RegisterErrorHandler<CustomBusinessError>((logger, result) =>
{
  logger?.LogWarning("Business rule violation: {Error}", result.ToString());
  return TypedResults.Problem(
    detail: result.ToString(),
    statusCode: StatusCodes.Status422UnprocessableEntity,
    title: "Business Rule Violation",
    type: "https://example.com/errors/business-rule");
});
```

#### Notes and Constraints

- `MapHttpOk<T>`/`MapHttpCreated<T>` require `T : class`; null `Value` on success throws `InvalidOperationException`.
- `MapHttpCreated<T>` with `uriFactory` only computes the URI for successful results; failure paths ignore the URI.
- Generic `Map` overloads return typed `Results<...>` unions suitable for Minimal API signatures and OpenAPI.
- All helpers accept an optional `ILogger` to enrich error logging without changing the HTTP shape.
