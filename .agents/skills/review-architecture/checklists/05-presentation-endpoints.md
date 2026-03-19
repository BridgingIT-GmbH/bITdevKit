# Checklist: Presentation Endpoints

This checklist helps verify that minimal API endpoints follow the thin adapter pattern and delegate properly to the application layer.

## Endpoint Structure (🟡 IMPORTANT)

**ADR-0014 (Minimal API Endpoints with DTO Exposure)**: Minimal API endpoints should be thin adapters that translate HTTP requests into commands/queries and map results back to HTTP responses. They should contain NO business logic.

### Checklist

- [ ] Endpoints derive from `EndpointsBase`
- [ ] Endpoints use `.MapGroup()` for common route prefixes
- [ ] Endpoint methods are private
- [ ] Each endpoint delegates to `IRequester.SendAsync()`
- [ ] Uses route constraints (e.g., `{id:guid}`, `{status:int}`)
- [ ] Parameter binding attributes explicit: `[FromRoute]`, `[FromQuery]`, `[FromBody]`, `[FromServices]`

### Example: EndpointsBase Structure

```csharp
// ✅ CORRECT: Thin adapter pattern
namespace MyApp.Presentation.Web.Endpoints;

using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public class CustomerEndpoints : EndpointsBase
{
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/customers")
            .WithTags("Customers");

        // ✅ Map endpoints
        group.MapPost("", this.CreateCustomerAsync)
            .WithName("CreateCustomer")
            .WithSummary("Creates a new customer");

        group.MapGet("{id:guid}", this.GetCustomerByIdAsync)
            .WithName("GetCustomerById")
            .WithSummary("Retrieves a customer by ID");

        group.MapPut("{id:guid}", this.UpdateCustomerAsync)
            .WithName("UpdateCustomer");

        group.MapDelete("{id:guid}", this.DeleteCustomerAsync)
            .WithName("DeleteCustomer");
    }

    // ✅ Private endpoint methods
    private async Task<IResult> CreateCustomerAsync(
        [FromBody] CustomerCreateCommand command,
        [FromServices] IRequester requester,
        CancellationToken ct)
    {
        var result = await requester.SendAsync(command, ct);
        return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
    }

    private async Task<IResult> GetCustomerByIdAsync(
        [FromRoute] Guid id,
        [FromServices] IRequester requester,
        CancellationToken ct)
    {
        var query = new CustomerFindOneQuery(id);
        var result = await requester.SendAsync(query, ct);
        return result.MapHttpOk();
    }
}
```

**Reference**: ADR-0014

## Thin Adapter Validation (🔴 CRITICAL)

**ADR-0014**: Endpoints must NOT contain business logic. All business rules belong in the domain layer; orchestration belongs in the application layer.

### Checklist

- [ ] NO business logic in endpoints (no validation, calculations, or business rules)
- [ ] NO direct domain method calls (use commands/queries instead)
- [ ] NO repository usage in endpoints
- [ ] NO DbContext usage in endpoints
- [ ] Endpoints only: receive HTTP → create command/query → delegate to IRequester → map result to HTTP

### Example: Thin Adapter

```csharp
// ✅ CORRECT: Thin adapter (no business logic)
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerCreateCommand command, // ✅ Command from HTTP body
    [FromServices] IRequester requester, // ✅ IRequester injected
    CancellationToken ct)
{
    // ✅ Delegate to application layer (no business logic here)
    var result = await requester.SendAsync(command, ct);

    // ✅ Map Result to HTTP response
    return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
}
```

### Common Violations

```csharp
// ❌ WRONG: Business logic in endpoint
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerCreateCommand command,
    [FromServices] IRequester requester,
    CancellationToken ct)
{
    // ❌ Validation logic in endpoint (should be in validator)
    if (string.IsNullOrWhiteSpace(command.Model.FirstName))
    {
        return Results.BadRequest("First name is required");
    }

    // ❌ Business rule in endpoint (should be in domain)
    if (command.Model.LastName == "notallowed")
    {
        return Results.BadRequest("Invalid last name");
    }

    var result = await requester.SendAsync(command, ct);
    return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
}
```

```csharp
// ❌ WRONG: Direct domain/repository access in endpoint
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerModel model,
    [FromServices] IGenericRepository<Customer> repository, // ❌ Direct repository
    CancellationToken ct)
{
    // ❌ Domain logic in endpoint
    var customer = Customer.Create(model.FirstName, model.LastName, model.Email, CustomerNumber.Create());

    if (customer.IsFailure)
    {
        return Results.BadRequest(customer.Errors);
    }

    // ❌ Repository in endpoint
    await repository.InsertAsync(customer.Value, ct);

    return Results.Created($"/api/customers/{customer.Value.Id}", customer.Value);
}
```

**Reference**: ADR-0014, ADR-0011 (Application Logic in Commands/Queries)

## IRequester Delegation (🟡 IMPORTANT)

**ADR-0005 (Requester/Notifier Mediator Pattern)**: Endpoints must use `IRequester.SendAsync()` to delegate to application handlers, not instantiate handlers directly.

### Checklist

- [ ] Endpoints inject `IRequester` (not handlers)
- [ ] Endpoints call `await requester.SendAsync(command, ct)` or `await requester.SendAsync(query, ct)`
- [ ] No direct handler instantiation
- [ ] Pipeline behaviors applied automatically (validation, retry, timeout)

### Example

```csharp
// ✅ CORRECT: IRequester delegation
private async Task<IResult> UpdateCustomerAsync(
    [FromRoute] Guid id,
    [FromBody] CustomerModel model,
    [FromServices] IRequester requester, // ✅ IRequester
    CancellationToken ct)
{
    var command = new CustomerUpdateCommand(id, model);
    var result = await requester.SendAsync(command, ct); // ✅ Delegate to handler
    return result.MapHttpOk();
}
```

**Reference**: ADR-0005

## OpenAPI Documentation (🟢 SUGGESTION)

**ADR-0014**: Endpoints should include OpenAPI metadata for API documentation and discoverability.

### Checklist

- [ ] `.WithName("UniqueName")` for route name
- [ ] `.WithSummary("Brief description")` for summary
- [ ] `.WithDescription("Detailed description")` for long description (optional)
- [ ] `.Produces<T>(StatusCodes.Status200OK)` for success responses
- [ ] `.ProducesResultProblem()` for failure responses (Result<T> errors)
- [ ] `.Accepts<T>("application/json")` for request body
- [ ] `.WithTags("TagName")` on group

### Example: Full OpenAPI Metadata

```csharp
// ✅ CORRECT: Complete OpenAPI metadata
public override void Map(IEndpointRouteBuilder app)
{
    var group = app.MapGroup("api/customers")
        .WithTags("Customers"); // ✅ Tag for grouping

    group.MapPost("", this.CreateCustomerAsync)
        .WithName("CreateCustomer") // ✅ Unique route name
        .WithSummary("Creates a new customer") // ✅ Brief summary
        .WithDescription("Creates a new customer with the provided details and returns the created customer resource.") // ✅ Detailed description
        .Produces<CustomerModel>(StatusCodes.Status201Created) // ✅ Success response
        .ProducesResultProblem() // ✅ Error response (Result<T> errors)
        .Accepts<CustomerCreateCommand>("application/json"); // ✅ Request body type

    group.MapGet("{id:guid}", this.GetCustomerByIdAsync)
        .WithName("GetCustomerById")
        .WithSummary("Retrieves a customer by ID")
        .Produces<CustomerModel>(StatusCodes.Status200OK)
        .ProducesResultProblem();
}
```

**Reference**: ADR-0014

## Result<T> Mapping (🔴 CRITICAL)

**ADR-0002 (Result Pattern for Error Handling)**: Endpoints must map `Result<T>` to HTTP responses using bITdevKit extension methods, not manual `if/else` checks.

### Checklist

- [ ] Use `.MapHttpOk()` for GET (200 OK)
- [ ] Use `.MapHttpCreated(location)` for POST (201 Created)
- [ ] Use `.MapHttpNoContent()` for PUT/DELETE (204 No Content)
- [ ] Use `.MapHttpOkAll()` for collections (200 OK)
- [ ] NO manual `if (result.IsSuccess)` checks
- [ ] NO manual `Results.Ok()`, `Results.Created()`, `Results.BadRequest()`

### Example: Result Mapping

```csharp
// ✅ CORRECT: Result<T> mapping with extension methods
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerCreateCommand command,
    [FromServices] IRequester requester,
    CancellationToken ct)
{
    var result = await requester.SendAsync(command, ct);
    return result.MapHttpCreated(r => $"/api/customers/{r.Id}"); // ✅ Maps Result<T> to 201 Created
}

private async Task<IResult> GetCustomerByIdAsync(
    [FromRoute] Guid id,
    [FromServices] IRequester requester,
    CancellationToken ct)
{
    var query = new CustomerFindOneQuery(id);
    var result = await requester.SendAsync(query, ct);
    return result.MapHttpOk(); // ✅ Maps Result<T> to 200 OK or problem details
}

private async Task<IResult> DeleteCustomerAsync(
    [FromRoute] Guid id,
    [FromServices] IRequester requester,
    CancellationToken ct)
{
    var command = new CustomerDeleteCommand(new CustomerId(id));
    var result = await requester.SendAsync(command, ct);
    return result.MapHttpNoContent(); // ✅ Maps Result to 204 No Content or problem details
}
```

### Common Violations

```csharp
// ❌ WRONG: Manual Result<T> checks
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerCreateCommand command,
    [FromServices] IRequester requester,
    CancellationToken ct)
{
    var result = await requester.SendAsync(command, ct);

    // ❌ Manual if/else instead of .MapHttpCreated()
    if (result.IsSuccess)
    {
        return Results.Created($"/api/customers/{result.Value.Id}", result.Value);
    }
    else
    {
        return Results.BadRequest(result.Errors);
    }
}
```

**Reference**: ADR-0002, ADR-0014

## CancellationToken (🟡 IMPORTANT)

**ADR-0014**: All async endpoint methods must include `CancellationToken` parameter to enable request cancellation.

### Checklist

- [ ] All async endpoint methods have `CancellationToken ct` parameter
- [ ] CancellationToken passed to `IRequester.SendAsync(command, ct)`
- [ ] Parameter named `ct` (short form)

### Example

```csharp
// ✅ CORRECT: CancellationToken included
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerCreateCommand command,
    [FromServices] IRequester requester,
    CancellationToken ct) // ✅ CancellationToken parameter
{
    var result = await requester.SendAsync(command, ct); // ✅ Pass ct to SendAsync
    return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
}
```

**Reference**: ADR-0014

## Summary

**Thin adapter endpoints are CRITICAL** for maintaining separation of concerns. Endpoints should only translate HTTP requests to commands/queries and delegate to the application layer.

**Key takeaways**:
- **Thin adapters**: NO business logic in endpoints
- **EndpointsBase**: Derive from `EndpointsBase`, use `.MapGroup()`
- **IRequester**: Delegate to `IRequester.SendAsync()`, not direct handlers
- **Result<T> mapping**: Use `.MapHttpOk()`, `.MapHttpCreated()`, `.MapHttpNoContent()`
- **OpenAPI**: Include `.WithName()`, `.WithSummary()`, `.Produces<T>()`
- **CancellationToken**: Always include `CancellationToken ct` parameter

**ADRs Referenced**:
- **ADR-0002**: Result Pattern for Error Handling
- **ADR-0005**: Requester/Notifier (Mediator) Pattern
- **ADR-0011**: Application Logic in Commands & Queries
- **ADR-0014**: Minimal API Endpoints with DTO Exposure
