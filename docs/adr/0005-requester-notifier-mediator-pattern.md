# ADR-0005: Requester/Notifier (Mediator) Pattern

## Status

Accepted

## Context

In layered architectures, presentation and application layers need a mechanism to invoke use cases without tight coupling to handler implementations:

**Challenges**:

- Endpoints should not directly instantiate command/query handlers
- Cross-cutting concerns (validation, retry, timeout, logging) would be duplicated across handlers
- Changing handler implementation requires modifying endpoint code
- Testing endpoints requires mocking every handler dependency
- No centralized place to apply pipeline behaviors

**Requirements**:

1. Decouple request senders (endpoints) from handlers
2. Support cross-cutting concerns via pipeline behaviors
3. Enable consistent request/response patterns
4. Maintain single responsibility (handlers focus on business logic)
5. Support both synchronous (commands/queries) and asynchronous (events) patterns

## Decision

Adopt bITdevKit's **Requester/Notifier pattern**, an implementation of the Mediator pattern, for all command, query, and event handling.

### Pattern Components

**IRequester**: Synchronous request/response (Commands & Queries)

```csharp
public interface IRequester
{
    Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        SendOptions options = null,
        CancellationToken cancellationToken = default);
}
```

**INotifier**: Asynchronous publish/subscribe (Domain Events)

```csharp
public interface INotifier
{
    Task PublishAsync<TNotification>(
        TNotification notification,
        PublishOptions options = null,
        CancellationToken cancellationToken = default);
}
```

### Pipeline Behaviors

Behaviors execute in order around each handler:

1. **ModuleScopeBehavior**: Sets current module context
2. **ValidationPipelineBehavior**: Validates request using FluentValidation
3. **RetryPipelineBehavior**: Retries transient failures (configurable)
4. **TimeoutPipelineBehavior**: Enforces operation timeout

### Request Flow

```
Endpoint
  → IRequester.SendAsync(command)
    → ModuleScopeBehavior
      → ValidationPipelineBehavior
        → RetryPipelineBehavior
          → TimeoutPipelineBehavior
            → Handler.HandleAsync()
            ← Result<T>
          ← (timeout enforcement)
        ← (retry on failure)
      ← (validation errors)
    ← (module context)
  ← Result<T>
```

### Registration Pattern

```csharp
builder.Services.AddRequester()
    .AddHandlers()
    .WithDefaultBehaviors();

builder.Services.AddNotifier()
    .AddHandlers()
    .WithDefaultBehaviors();
```

## Rationale

1. **Decoupling**: Endpoints depend on `IRequester`, not concrete handlers
2. **Cross-Cutting Concerns**: Pipeline behaviors apply consistently to all requests
3. **Single Responsibility**: Handlers focus on business logic, not validation/retry/timeout
4. **Testability**: Can test handlers independently or through requester
5. **Consistency**: All commands/queries follow the same invocation pattern
6. **Extensibility**: Easy to add new pipeline behaviors for new concerns
7. **Module Scoping**: Module context automatically set for multi-module scenarios

## Consequences

### Positive

- Endpoints have minimal dependencies (just `IRequester`)
- Cross-cutting concerns centralized in pipeline behaviors (no duplication)
- Consistent validation, retry, and timeout logic across all requests
- Handlers are testable in isolation (no mediator dependency)
- Easy to add new behaviors without modifying handlers
- Clear separation between request definition and handling
- Module context automatically tracked for logging and filtering

### Negative

- Indirection through mediator (one extra hop)
- Request/handler types must be registered explicitly
- Developers must understand pipeline behavior order
- Stack traces include pipeline behavior frames

### Neutral

- Commands/Queries implement `IRequest<TResponse>`
- Handlers implement `IRequestHandler<TRequest, TResponse>`
- Behaviors wrap all handlers uniformly
- Assembly scanning automatically discovers handlers

## Alternatives Considered

- **Alternative 1: Direct Handler Injection in Endpoints**
  - Rejected because endpoints would need dependencies on every handler
  - Cross-cutting concerns duplicated in every handler
  - Violates Open/Closed Principle (adding concern requires modifying handlers)

- **Alternative 2: MediatR Library**
  - Considered but bITdevKit Requester/Notifier provides similar functionality
  - bITdevKit integrates better with other framework features (modules, Result pattern)
  - Keeping dependencies consistent within bITdevKit ecosystem

- **Alternative 3: Service Layer with Manual Validation/Retry**
  - Rejected because it requires manual cross-cutting concern implementation
  - No standardized request/response patterns
  - More boilerplate in every service method

## Related Decisions

- [ADR-0002](0002-result-pattern-error-handling.md): Handlers return Results through requester
- [ADR-0009](0009-fluent-validation-strategy.md): ValidationPipelineBehavior uses FluentValidation
- [ADR-0011](0011-application-logic-in-commands-queries.md): Handlers contain application logic

## References

- [bITdevKit Requester/Notifier Documentation](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-requester-notifier.md)
- [README - Requester/Notifier Pattern](../../README.md#requesternotifier-pattern-mediator)
- [README - Pipeline Behaviors](../../README.md#pipeline-behaviors)

## Notes

### Endpoint Usage Example

```csharp
public class CustomerEndpoints : EndpointsBase
{
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/coremodule/customers")
            .WithTags("CoreModule.Customers");

        group.MapPost("",
            async (IRequester requester, CustomerModel model, CancellationToken ct) =>
                (await requester.SendAsync(new CustomerCreateCommand(model), cancellationToken: ct))
                    .MapHttpCreated(v => $"/api/coremodule/customers/{v.Id}"))
            .WithName("CoreModule.Customers.Create");
    }
}
```

### Command Definition

```csharp
public class CustomerCreateCommand(CustomerModel model) : RequestBase<CustomerModel>
{
    public CustomerModel Model { get; set; } = model;

    public class Validator : AbstractValidator<CustomerCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Model).NotNull();
            this.RuleFor(c => c.Model.FirstName).NotNull().NotEmpty();
            this.RuleFor(c => c.Model.Email).EmailAddress();
        }
    }
}
```

### Handler Implementation

```csharp
public class CustomerCreateCommandHandler(
    ILogger<CustomerCreateCommandHandler> logger,
    IGenericRepository<Customer> repository,
    ...)
    : RequestHandlerBase<CustomerCreateCommand, CustomerModel>(logger)
{
    protected override async Task<Result<CustomerModel>> HandleAsync(
        CustomerCreateCommand request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        // Business logic here
        // Validation already executed by ValidationPipelineBehavior
        // Retry/Timeout managed by respective behaviors
    }
}
```

### Pipeline Behavior Configuration

Behaviors execute in the order they're registered:

```csharp
builder.Services.AddRequester()
    .AddHandlers()  // Scans assemblies for IRequestHandler implementations
    .WithDefaultBehaviors();  // Adds ModuleScope, Validation, Retry, Timeout
```

### Custom Behavior Example

```csharp
public class LoggingPipelineBehavior<TRequest, TResponse> :
    IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}

// Register
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
```

### Behavior Order Importance

The default behavior order is intentional:

1. **ModuleScope**: Sets context for logging/filtering in subsequent behaviors
2. **Validation**: Fails fast before expensive operations
3. **Retry**: Retries after validation passes
4. **Timeout**: Innermost to measure actual handler execution

### Domain Events via Notifier

```csharp
// Publish event
await notifier.PublishAsync(new CustomerCreatedDomainEvent(customer), cancellationToken: ct);

// Handler
public class CustomerCreatedDomainEventHandler :
    DomainEventHandlerBase<CustomerCreatedDomainEvent>
{
    public override async Task Process(
        CustomerCreatedDomainEvent notification,
        CancellationToken ct)
    {
        // React to domain event (send email, update read model, etc.)
    }
}
```

### Testing Strategies

**Unit Test Handler Directly** (no mediator):

```csharp
var handler = new CustomerCreateCommandHandler(logger, repository, ...);
var result = await handler.Handle(command, CancellationToken.None);
result.ShouldBeSuccess();
```

**Integration Test Through Requester** (with behaviors):

```csharp
var requester = serviceProvider.GetRequiredService<IRequester>();
var result = await requester.SendAsync(command, cancellationToken: CancellationToken.None);
// Validation, retry, timeout behaviors all execute
```

### Implementation Files

- **Requester setup**: `src/Presentation.Web.Server/Program.cs`
- **Command example**: `src/Modules/CoreModule/CoreModule.Application/Commands/CustomerCreateCommand.cs`
- **Handler example**: `src/Modules/CoreModule/CoreModule.Application/Commands/CustomerCreateCommandHandler.cs`
- **Endpoint usage**: `src/Modules/CoreModule/CoreModule.Presentation/Web/Endpoints/CustomerEndpoints.cs`
