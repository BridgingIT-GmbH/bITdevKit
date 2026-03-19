# ADR-0014: Minimal API Endpoints with DTO Exposure and OpenAPI Integration

## Status

Accepted

## Context

When building RESTful APIs in .NET, there are multiple approaches to defining HTTP endpoints:

- **Traditional MVC Controllers**: Class-based controllers with action methods decorated with route attributes
- **Minimal APIs**: Lightweight, inline route handlers introduced in .NET 6+
- **API Controllers with attributes**: Attribute-based routing with `[ApiController]` and `[Route]` attributes

Additionally, decisions must be made about:

- **What to expose**: Domain aggregates/entities directly vs. Data Transfer Objects (DTOs)
- **OpenAPI documentation**: How to generate comprehensive API documentation for consumers
- **Layer boundaries**: How to maintain clean architecture while exposing APIs
- **Consistency**: How to ensure uniform endpoint patterns across modules

The application needed an API strategy that:

1. Provides lightweight, performant HTTP endpoints
2. Maintains clean architecture by not exposing domain internals
3. Generates comprehensive OpenAPI/Swagger documentation automatically
4. Supports modular organization aligned with domain modules
5. Enables explicit request/response contracts with proper HTTP semantics
6. Allows easy testing and minimal boilerplate

## Decision

Adopt **ASP.NET Core Minimal APIs** organized as endpoint classes, with **DTO/Model exposure** (never domain entities), and **comprehensive OpenAPI metadata**.

### Endpoint Structure

1. **Endpoint Classes**: Derive from `EndpointsBase` and override `Map(IEndpointRouteBuilder)`
2. **Route Groups**: Use `MapGroup()` to organize related endpoints with common prefixes and policies
3. **DTO Exposure**: API contracts use `*Model` DTOs from Application layer, never domain entities
4. **IRequester Pattern**: Endpoints delegate to commands/queries via `IRequester.SendAsync()`
5. **Result Mapping**: Use `.MapHttpOk()`, `.MapHttpCreated()`, `.MapHttpNoContent()` for `Result<T>` responses
6. **OpenAPI Metadata**: Every endpoint includes `.WithName()`, `.WithSummary()`, `.WithDescription()`, `.Produces<T>()`, `.ProducesProblem()`

### Example Endpoint Class

```csharp
namespace CoreModule.Presentation.Web;

[ExcludeFromCodeCoverage]
public class CustomerEndpoints : EndpointsBase
{
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/coremodule/customers")
            .RequireAuthorization()
            .WithTags("CoreModule.Customers");

        // GET /{id:guid} -> Find one customer by ID
        group.MapGet("/{id:guid}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string id, CancellationToken ct)
                   => (await requester
                    .SendAsync(new CustomerFindOneQuery(id), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("CoreModule.Customers.GetById")
            .WithSummary("Get customer by ID")
            .WithDescription("Retrieves a single customer by their unique identifier.")
            .Produces<CustomerModel>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesResultProblem(StatusCodes.Status400BadRequest);

        // POST -> Create new customer
        group.MapPost("",
            async ([FromServices] IRequester requester,
                   [FromBody] CustomerModel model, CancellationToken ct)
                   => (await requester
                    .SendAsync(new CustomerCreateCommand(model), cancellationToken: ct))
                    .MapHttpCreated(v => $"/api/coremodule/customers/{v.Id}"))
            .WithName("CoreModule.Customers.Create")
            .WithSummary("Create a new customer")
            .Accepts<CustomerModel>("application/json")
            .Produces<CustomerModel>(StatusCodes.Status201Created, "application/json")
            .ProducesResultProblem(StatusCodes.Status400BadRequest);
    }
}
```

### DTO Exposure Rules

**Never expose domain entities directly**:

- WRONG `Customer` (domain aggregate)
- WRONG `EmailAddress` (value object)
- CORRECT `CustomerModel` (DTO with primitive properties)
- CORRECT `CustomerUpdateStatusRequestModel` (DTO for specific operations)

**Mapping layer**:

- Mapster configurations in `MapperRegister` classes convert between domain and DTOs
- Handlers return `Result<CustomerModel>`, not `Result<Customer>`
- Endpoints receive and return DTOs only

### OpenAPI Metadata Requirements

Every endpoint must include:

- **Name**: `.WithName("Module.Resource.Operation")` for client code generation
- **Summary**: `.WithSummary("Brief title")` for OpenAPI UI
- **Description**: `.WithDescription("Detailed explanation")` for developer documentation
- **Request Body**: `.Accepts<TModel>("application/json")` when accepting JSON
- **Success Response**: `.Produces<TModel>(StatusCodes.Status200OK)` with model type
- **Error Responses**: `.ProducesProblem()` or `.ProducesResultProblem()` for each error status
- **Tags**: Applied via `.WithTags()` for grouping in OpenAPI UI

### Result<T> HTTP Mapping Extensions

- **`.MapHttpOk()`**: Maps `Result<T>` → HTTP 200 with body
- **`.MapHttpOkAll()`**: Maps `Result<IEnumerable<T>>` → HTTP 200 with collection
- **`.MapHttpCreated(locationFactory)`**: Maps `Result<T>` → HTTP 201 with Location header
- **`.MapHttpNoContent()`**: Maps `Result` → HTTP 204 (no body)

These extensions automatically:

- Return 200/201/204 on success with appropriate body
- Return 400 with ProblemDetails on validation failure
- Return 404 when result is empty/not found
- Return 500 with ProblemDetails on unexpected errors

## Rationale

### Why Minimal APIs

1. **Performance**: Minimal APIs have lower overhead than MVC controllers (no model binding complexity)
2. **Simplicity**: Inline handlers reduce ceremony and boilerplate compared to controllers
3. **Modern**: Aligned with .NET's direction (introduced .NET 6, enhanced .NET 7+)
4. **Explicit**: Route handlers are explicit and co-located with route definitions
5. **Testability**: Easy to test without needing controller context infrastructure
6. **Less Magic**: No attribute-based routing discovery; everything is explicit

### Why DTO Exposure (Not Domain Entities)

1. **Layer Protection**: Prevents external clients from depending on domain implementation details
2. **Versioning**: DTOs can evolve independently from domain model for API compatibility
3. **Security**: Domain entities may contain sensitive logic/data not meant for external exposure
4. **Serialization Control**: DTOs designed for JSON serialization; domain entities designed for business logic
5. **Backward Compatibility**: Can maintain old DTO versions while evolving domain model
6. **Explicit Contracts**: API contracts are clear and don't leak aggregate structures

### Why Comprehensive OpenAPI Metadata

1. **Client Generation**: Enables automatic client SDK generation (TypeScript, C#, etc.)
2. **Documentation**: OpenAPI UI (Swagger) provides interactive API exploration for developers
3. **Validation**: Tools can validate requests/responses against OpenAPI schema
4. **Discoverability**: New developers can understand API capabilities without reading code
5. **Standards Compliance**: OpenAPI is industry standard for REST API documentation
6. **Testing**: OpenAPI spec can drive automated API testing tools

### Why Endpoint Classes (Not Inline in Program.cs)

1. **Organization**: Endpoints grouped by module/resource in dedicated classes
2. **Discoverability**: Easy to find all endpoints for a resource in one file
3. **Testability**: Endpoint classes can be unit tested independently
4. **Separation of Concerns**: Keeps `Program.cs` clean and focused on composition
5. **Module Alignment**: Each module registers its own endpoints via `services.AddEndpoints<T>()`

## Consequences

### Positive

- **Clean Separation**: API layer completely decoupled from domain layer via DTOs
- **Performance**: Minimal APIs are faster than MVC controllers (lower memory allocation, faster routing)
- **OpenAPI Quality**: Comprehensive metadata produces excellent API documentation automatically
- **Maintainability**: Clear endpoint classes easy to locate and modify
- **Type Safety**: Strongly-typed DTOs provide compile-time safety for API contracts
- **Testability**: Thin endpoint classes are easy to test; business logic tested in handlers
- **Consistency**: Standardized pattern across all modules and endpoints
- **Evolution**: DTOs can version independently; domain can refactor without breaking API
- **Client Experience**: Generated clients are type-safe and well-documented

### Negative

- **Mapping Overhead**: Every request/response requires mapping between domain and DTOs
- **Duplication**: DTOs may appear similar to domain entities, feeling redundant
- **Maintenance**: Changes to domain model require updating DTOs and mappings
- **Learning Curve**: Minimal APIs are newer; some team members may be unfamiliar
- **Tooling**: IDE tooling for Minimal APIs less mature than for MVC controllers (improving)

### Neutral

- **OpenAPI Metadata Verbosity**: Comprehensive metadata makes endpoints verbose but documents well
- **Mapping Configuration**: Mapster configurations centralized in `MapperRegister` classes
- **Endpoint Registration**: Each module registers endpoints via `services.AddEndpoints<T>()`
- **Result Mapping**: Custom extensions simplify `Result<T>` → HTTP response conversion

## Implementation Guidelines

### Endpoint Class Template

```csharp
namespace <Module>.Presentation.Web;

[ExcludeFromCodeCoverage] // Endpoints are thin adapters, tested via integration tests
public class <Resource>Endpoints : EndpointsBase
{
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/<module>/<resource>")
            .RequireAuthorization() // Apply if auth required
            .WithTags("<Module>.<Resource>");

        // Define endpoints here using MapGet, MapPost, MapPut, MapDelete
    }
}
```

### Endpoint Method Pattern

```csharp
group.MapGet("/{id:guid}",
    async ([FromServices] IRequester requester,
           [FromRoute] string id,
           CancellationToken ct)
           => (await requester
            .SendAsync(new <Resource>FindOneQuery(id), cancellationToken: ct))
            .MapHttpOk())
    .WithName("<Module>.<Resource>.GetById")
    .WithSummary("<Brief summary>")
    .WithDescription("<Detailed description with examples>")
    .Produces<TModel>(StatusCodes.Status200OK, "application/json")
    .Produces(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesResultProblem(StatusCodes.Status400BadRequest)
    .ProducesResultProblem(StatusCodes.Status500InternalServerError);
```

### DTO Design Guidelines

**DTOs should**:

- Use primitive types (`string`, `int`, `decimal`, `DateTime`, `Guid`)
- Be serializable to/from JSON without custom converters
- Include XML documentation for OpenAPI schema generation
- Have nullable reference types for optional fields
- Be immutable (records) when possible

**Example DTO**:

```csharp
namespace CoreModule.Application.Models;

/// <summary>
/// Represents a customer in the system.
/// </summary>
public sealed record CustomerModel
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the customer number.
    /// </summary>
    public string CustomerNumber { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the customer status (Lead, Active, Retired).
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the concurrency version for optimistic locking.
    /// </summary>
    public string ConcurrencyVersion { get; set; }
}
```

### Mapping Configuration

Mapster configurations in module `MapperRegister`:

```csharp
namespace CoreModule.Application;

internal sealed class MapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Domain → DTO
        config.NewConfig<Customer, CustomerModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.CustomerNumber, src => src.CustomerNumber.Value)
            .Map(dest => dest.Email, src => src.Email.Value)
            .Map(dest => dest.Status, src => src.Status.Name)
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString());

        // DTO → Domain (for commands)
        config.NewConfig<CustomerModel, Customer>()
            .ConstructUsing(src => Customer.Create(
                CustomerNumber.Create(src.CustomerNumber).Value,
                EmailAddress.Create(src.Email).Value,
                src.FirstName,
                src.LastName).Value)
            .IgnoreNonMapped(true);
    }
}
```

### HTTP Status Code Guidelines

- **200 OK**: Successful GET, PUT (returns updated resource)
- **201 Created**: Successful POST (returns created resource + Location header)
- **204 No Content**: Successful DELETE (no response body)
- **400 Bad Request**: Validation failure or invalid input (ProblemDetails)
- **401 Unauthorized**: Authentication required but missing/invalid
- **404 Not Found**: Resource not found
- **409 Conflict**: Concurrency conflict (optimistic locking failure)
- **500 Internal Server Error**: Unexpected server error (ProblemDetails)

### Route Constraints

Use route constraints for type safety:

- `{id:guid}` - Ensures ID is a valid GUID
- `{id:int}` - Ensures ID is an integer
- `{id:minlength(5)}` - Minimum length validation

### Parameter Binding Attributes

Be explicit with parameter sources:

- `[FromServices]` - Dependency injection
- `[FromRoute]` - URL path parameters
- `[FromQuery]` - Query string parameters
- `[FromBody]` - Request body JSON
- `[FromHeader]` - HTTP headers

## Alternatives Considered

### Alternative 1: MVC Controllers

```csharp
[ApiController]
[Route("api/coremodule/customers")]
public class CustomersController : ControllerBase
{
    private readonly IRequester requester;

    public CustomersController(IRequester requester)
    {
        this.requester = requester;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var result = await this.requester.SendAsync(new CustomerFindOneQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
```

**Rejected because**:

- More boilerplate (class with constructor, fields)
- Slower performance than Minimal APIs
- More "magic" (attribute-based routing discovery)
- Harder to see all endpoints at a glance
- Not aligned with .NET's modern direction

### Alternative 2: Expose Domain Entities Directly

```csharp
.Produces<Customer>(StatusCodes.Status200OK) // X Domain entity
```

**Rejected because**:

- Violates clean architecture layer boundaries
- Couples external API to domain implementation details
- Makes API versioning difficult
- Exposes internal domain structure to external consumers
- Prevents domain refactoring without breaking API
- May leak sensitive business logic or data

### Alternative 3: Minimal OpenAPI Metadata

```csharp
group.MapGet("/{id}", async (string id) => await GetCustomer(id));
// No .WithName, .WithSummary, .Produces, etc.
```

**Rejected because**:

- OpenAPI generation is incomplete and low-quality
- Client generation produces poor results
- Developers can't discover API capabilities
- No documentation for external consumers
- Testing tools can't validate contracts

### Alternative 4: Inline Endpoints in Program.cs

```csharp
// In Program.cs
app.MapGet("/api/customers/{id}", async (string id) => { /* handler */ });
app.MapPost("/api/customers", async (CustomerModel model) => { /* handler */ });
```

**Rejected because**:

- `Program.cs` becomes massive and unmanageable
- Endpoints not organized by module/resource
- Hard to discover all endpoints for a resource
- Can't easily test endpoint registration
- Doesn't scale for modular architecture

## Related Decisions

- [ADR-0001](0001-clean-onion-architecture.md): Clean Architecture enforces DTO exposure at boundaries
- [ADR-0002](0002-result-pattern-error-handling.md): Result\<T> pattern maps to HTTP responses via extensions
- [ADR-0003](0003-modular-monolith-architecture.md): Each module registers its own endpoints
- [ADR-0005](0005-requester-notifier-mediator-pattern.md): Endpoints use IRequester to send commands/queries
- [ADR-0010](0010-mapster-object-mapping.md): Mapster handles domain ↔ DTO mapping

## References

- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
- [OpenAPI Specification](https://swagger.io/specification/)
- [REST API Design Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design)
- [Martin Fowler - DTO Pattern](https://martinfowler.com/eaaCatalog/dataTransferObject.html)
- [Microsoft - Web API Documentation with Swagger](https://learn.microsoft.com/en-us/aspnet/core/tutorials/web-api-help-pages-using-swagger)

## Notes

### OpenAPI Generation

OpenAPI specification automatically generated at `/openapi/v1.json` and available via Swagger UI at `/swagger`.

### Endpoint Registration

Endpoints registered per module:

```csharp
// In module's Module.cs
services.AddEndpoints<CustomerEndpoints>();
```

### Testing Strategy

- **Unit Tests**: Not typically needed for thin endpoint classes
- **Integration Tests**: Test endpoints end-to-end with `WebApplicationFactory`
- **OpenAPI Validation**: Automated tools validate OpenAPI spec correctness

### Naming Conventions

- **Endpoint Class**: `<Resource>Endpoints` (plural)
- **Endpoint Name**: `<Module>.<Resource>.<Operation>` (e.g., `CoreModule.Customers.GetById`)
- **Route Group**: `api/<module>/<resource>` (lowercase)
- **Tags**: `<Module>.<Resource>` (e.g., `CoreModule.Customers`)

### Security

- `.RequireAuthorization()` applied at group level for all endpoints
- Individual endpoints can override: `.AllowAnonymous()`
- Policy-based authorization: `.RequireAuthorization("PolicyName")`

### Implementation Location

- **Endpoint Classes**: `src/Modules/<Module>/<Module>.Presentation/Web/Endpoints/`
- **DTO Models**: `src/Modules/<Module>/<Module>.Application/Models/`
- **Mapping Configs**: `src/Modules/<Module>/<Module>.Presentation/MapperRegister.cs`
- **Integration Tests**: `tests/Modules/<Module>/<Module>.IntegrationTests/Endpoints/`

### Common Patterns

**Search with complex filters** (POST instead of GET for large filter payloads):

```csharp
group.MapPost("search",
    async ([FromServices] IRequester requester,
           [FromBody] FilterModel filter, CancellationToken ct)
           => (await requester.SendAsync(new CustomerFindAllQuery { Filter = filter }, ct))
            .MapHttpOkAll())
    .WithName("CoreModule.Customers.Search");
```

**Partial update** (specific field updates):

```csharp
group.MapPut("/{id}/status",
    async ([FromRoute] string id,
           [FromBody] CustomerUpdateStatusRequestModel body, CancellationToken ct)
           => (await requester.SendAsync(new CustomerUpdateStatusCommand(id, body.Status), ct))
            .MapHttpOk());
```

**Bulk operations**:

```csharp
group.MapPost("bulk",
    async ([FromBody] IEnumerable<CustomerModel> models, CancellationToken ct)
           => (await requester.SendAsync(new CustomerBulkCreateCommand(models), ct))
            .MapHttpOk());
```
