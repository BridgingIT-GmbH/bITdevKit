# bITdevKit Patterns

Common patterns and features provided by the bITdevKit framework used throughout the application.

## IRequester/INotifier (Mediator Pattern)

**ADR-0005**: Requester/Notifier (Mediator) Pattern

### IRequester (Commands & Queries)

Used for request-response communication (commands/queries → handlers).

```csharp
// ✅ Endpoint injects IRequester
private async Task<IResult> CreateCustomerAsync(
    [FromBody] CustomerCreateCommand command,
    [FromServices] IRequester requester,
    CancellationToken ct)
{
    var result = await requester.SendAsync(command, ct);
    return result.MapHttpCreated(r => $"/api/customers/{r.Id}");
}
```

### INotifier (Domain Events & Integration Events)

Used for publish-subscribe communication (events → multiple handlers).

```csharp
// ✅ Publish integration event
await this.notifier.PublishAsync(
    new CustomerCreatedIntegrationEvent(customer.Id, customer.Email),
    ct);
```

### Pipeline Behaviors

Automatically applied via registration:
- `ValidationPipelineBehavior`: Executes FluentValidation validators
- `RetryPipelineBehavior`: Retries failed operations
- `TimeoutPipelineBehavior`: Enforces timeout on operations
- `ModuleScopeBehavior`: Sets module scope for logging

```csharp
// ✅ Register pipeline behaviors
services.AddRequester(o => o
    .WithBehavior<ValidationPipelineBehavior>()
    .WithBehavior<ModuleScopeBehavior>()
    .WithBehavior<RetryPipelineBehavior>()
    .WithBehavior<TimeoutPipelineBehavior>());
```

---

## Repository Behaviors (Decorator Pattern)

**ADR-0004**: Repository Pattern with Decorator Behaviors

Repository decorators provide cross-cutting concerns transparently.

### Available Behaviors

- `RepositoryLoggingBehavior<T>`: Logs repository operations
- `RepositoryAuditBehavior<T>`: Tracks creation/modification audit fields
- `RepositoryDomainEventBehavior<T>`: Publishes domain events after persistence
- `RepositoryTracingBehavior<T>`: Adds tracing spans

### Registration Pattern

```csharp
// ✅ Register repository with decorators
services.AddEntityFrameworkRepository<Customer, CoreModuleDbContext>()
    .WithBehavior<RepositoryLoggingBehavior<Customer>>()
    .WithBehavior<RepositoryAuditBehavior<Customer>>()
    .WithBehavior<RepositoryDomainEventBehavior<Customer>>()
    .WithBehavior<RepositoryTracingBehavior<Customer>>();
```

### Usage in Handlers

```csharp
// ✅ Inject repository abstraction
public class CustomerCreateCommandHandler
{
    private readonly IGenericRepository<Customer> repository;

    public async Task<Result<CustomerModel>> Handle(
        CustomerCreateCommand request,
        CancellationToken ct)
    {
        var customer = Customer.Create(...);
        await this.repository.InsertAsync(customer.Value, ct); // Decorators applied automatically
        return Result<CustomerModel>.Success(...);
    }
}
```

---

## Module Registration (IModule Interface)

Each module implements `IModule` for self-registration.

```csharp
// ✅ Module registration
public class CoreModule : IModule
{
    public string Name => "CoreModule";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext
        services.AddSqlServerDbContext<CoreModuleDbContext>(
            connectionString: configuration.GetConnectionString("CoreModuleDb"));

        // Register repositories with decorators
        services.AddEntityFrameworkRepository<Customer, CoreModuleDbContext>()
            .WithBehavior<RepositoryLoggingBehavior<Customer>>()
            .WithBehavior<RepositoryAuditBehavior<Customer>>()
            .WithBehavior<RepositoryDomainEventBehavior<Customer>>();

        // Register mapping
        services.AddMapping().WithMapster<CoreModuleMapperRegister>();

        // Register requester/notifier
        services.AddRequester(o => o
            .WithBehavior<ValidationPipelineBehavior>()
            .WithBehavior<ModuleScopeBehavior>());

        // Register endpoints
        services.AddEndpoints<CustomerEndpoints>();
    }
}

// ✅ Host registers modules
builder.Services.AddModule<CoreModule>(builder.Configuration);
```

---

## Startup Tasks (IStartupTask)

Background tasks that run on application startup.

```csharp
// ✅ Startup task for database migration
public class MigrateDatabaseStartupTask : IStartupTask
{
    private readonly CoreModuleDbContext context;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        await this.context.Database.MigrateAsync(ct);
    }
}

// ✅ Register startup task
services.AddStartupTask<MigrateDatabaseStartupTask>();
```

---

## Quartz Jobs (IJob)

**ADR-0015**: Background Jobs & Scheduling with Quartz.NET

```csharp
// ✅ Job implementation
public class CustomerExportJob : IJob
{
    private readonly IRequester requester;

    public async Task Execute(IJobExecutionContext context)
    {
        var command = new CustomerExportCommand();
        await this.requester.SendAsync(command, context.CancellationToken);
    }
}

// ✅ Register job with schedule
services.AddJob<CustomerExportJob>(schedule: "0 0 2 * * ?"); // Daily at 2 AM
```

---

## Specifications (ISpecification<T>)

**ADR-0019**: Specification Pattern for Repository Queries

```csharp
// ✅ Specification implementation
public class ActiveCustomersSpecification : Specification<Customer>
{
    public ActiveCustomersSpecification()
    {
        this.AddExpression(c => c.Status == CustomerStatus.Active);
        this.AddInclude(c => c.Addresses);
        this.AddOrdering(c => c.LastName);
    }
}

// ✅ Use with repository
var specification = new ActiveCustomersSpecification();
var customers = await repository.FindAllAsync(specification, ct);
```

---

## Summary

**bITdevKit provides**:
- **IRequester/INotifier**: Mediator pattern for decoupling
- **Repository decorators**: Cross-cutting concerns (logging, audit, events)
- **Module registration**: Self-contained module setup
- **Startup tasks**: Database migrations and initialization
- **Quartz jobs**: Background job scheduling
- **Specifications**: Query encapsulation

**References**:
- **(ADR-0004)**: Repository Pattern with Decorator Behaviors
- **(ADR-0005)**: Requester/Notifier (Mediator) Pattern
- **(ADR-0015)**: Background Jobs & Scheduling with Quartz.NET
- **(ADR-0019)**: Specification Pattern for Repository Queries
- [bITdevKit Documentation](https://github.com/BridgingIT-GmbH/bITdevKit/tree/main/docs)
