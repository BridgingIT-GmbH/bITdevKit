# ADR-0018: Dependency Injection & Service Lifetime Management

## Status

Accepted

## Context

Modern .NET applications rely heavily on Dependency Injection (DI) for:

- **Loose Coupling**: Components depend on abstractions, not concrete types
- **Testability**: Easy to substitute implementations with mocks/fakes
- **Configuration**: Services configured centrally, not scattered throughout code
- **Lifetime Management**: Framework manages object creation and disposal
- **Cross-Cutting Concerns**: Decorators, interceptors, middleware
- **Module Composition**: Pluggable modules with isolated registrations

However, improper DI usage leads to:

- **Memory Leaks**: Capturing scoped dependencies in singletons
- **Concurrency Issues**: Shared mutable state in singleton services
- **ObjectDisposedException**: Using disposed scoped services
- **Performance Degradation**: Creating expensive objects too frequently
- **Complexity**: Service resolution failures difficult to diagnose

The application needed a DI strategy that:

1. Defines **clear lifetime rules** (singleton, scoped, transient)
2. Organizes registrations **per module** for modularity
3. Prevents **captive dependencies** (scoped in singleton)
4. Uses **factory patterns** where lifetime management is complex
5. Supports **decorator patterns** (repository behaviors)
6. Integrates with **bITdevKit module infrastructure**

## Decision

Adopt **Microsoft.Extensions.DependencyInjection** with **module-based registration**, **explicit lifetime choices**, **factory pattern for scoped dependencies in singletons**, and **decorator chains for cross-cutting concerns**.

### Service Lifetime Guidelines

| Lifetime | When to Use | Examples |
| -------- | ----------- | -------- |
| **Singleton** | Stateless services shared across application lifetime | Configuration, mappers, clients (if thread-safe), caches |
| **Scoped** | Services tied to request/operation scope | DbContext, repositories, request handlers, UnitOfWork |
| **Transient** | Lightweight, stateless services created per usage | Validators, specifications, value object factories |

### Module Registration Pattern

```csharp
public class CoreModule : WebModuleBase
{
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        // Configuration
        var moduleConfiguration = this.Configure<CoreModuleConfiguration>(services, configuration);

        // Domain Services (usually scoped or transient)
        services.AddScoped<ICustomerDomainService, CustomerDomainService>();

        // Application Services (handlers registered by MediatR)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(
            typeof(CoreModule).Assembly));

        // Infrastructure - Database
        services.AddSqlServerDbContext<CoreDbContext>(o => o
            .UseConnectionString(moduleConfiguration.ConnectionStrings["Default"])
            .UseLogger(true, System.Diagnostics.Tracing.EventLevel.Warning));

        // Infrastructure - Repositories with Behaviors
        services.AddEntityFrameworkRepository<Customer, CoreDbContext>()
            .WithBehavior<RepositoryLoggingBehavior<Customer>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<Customer>>()
            .WithBehavior<RepositoryAuditStateBehavior<Customer>>();

        // Presentation - Endpoints
        services.AddEndpoints<CustomerEndpoints>();

        // Presentation - Mapping
        services.AddMapping().WithMapster<CoreModuleMapperRegister>();

        // Jobs
        services.AddJobScheduling(o => o
            .StartupDelay(configuration["JobScheduling:StartupDelay"]), configuration)
            .WithJob<CustomerExportJob>()
                .Cron(CronExpressions.EveryMinute)
                .Named($"{this.Name}_{nameof(CustomerExportJob)}")
                .RegisterScoped();

        return services;
    }
}
```

### Singleton Services (Stateless, Thread-Safe)

```csharp
// Configuration objects (immutable after bind)
services.AddSingleton<CoreModuleConfiguration>(sp =>
    configuration.GetSection("Modules:Core").Get<CoreModuleConfiguration>());

// Mapster mapper (stateless after configuration)
services.AddSingleton<IMapper>(sp =>
    new Mapper(TypeAdapterConfig.GlobalSettings));

// HttpClient factory (thread-safe)
services.AddHttpClient<IExternalApiClient, ExternalApiClient>();

// Caches (thread-safe implementations)
services.AddSingleton<IMemoryCache, MemoryCache>();
```

### Scoped Services (Per-Request/Operation)

```csharp
// DbContext (NOT thread-safe, must be scoped)
services.AddDbContext<CoreDbContext>(options =>
    options.UseSqlServer(connectionString), ServiceLifetime.Scoped);

// Repositories (depend on scoped DbContext)
services.AddScoped<IGenericRepository<Customer>, EntityFrameworkRepository<Customer, CoreDbContext>>();

// Request Handlers (operate on single request)
services.AddScoped<IRequestHandler<CustomerCreateCommand, Result<CustomerId>>, CustomerCreateCommandHandler>();

// Unit of Work (transaction scope)
services.AddScoped<IUnitOfWork, UnitOfWork>();
```

### Transient Services (New Instance Per Injection)

```csharp
// Validators (lightweight, stateless)
services.AddTransient<IValidator<CustomerCreateCommand>, CustomerCreateCommand.Validator>();

// Specifications (query building objects)
services.AddTransient<ISpecification<Customer>, CustomerEmailSpecification>();

// Value Object Factories (creation logic)
services.AddTransient<IEmailAddressFactory, EmailAddressFactory>();
```

### Factory Pattern for Scoped Dependencies in Singletons

```csharp
// WRONG: Captive dependency (scoped in singleton)
public class CustomerExportJob : JobBase
{
    private readonly IGenericRepository<Customer> repository; // SCOPED!

    public CustomerExportJob(IGenericRepository<Customer> repository) // Injected into SINGLETON job
    {
        this.repository = repository; // ObjectDisposedException when scope ends
    }
}

// Correct: Use IServiceScopeFactory
[DisallowConcurrentExecution]
public class CustomerExportJob(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory) : JobBase(loggerFactory)
{
    public override async Task Process(
        IJobExecutionContext context,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<Customer>>();

        // Use repository within scope
        var result = await repository.FindAllResultAsync(cancellationToken: cancellationToken);
    }
}
```

### Decorator Pattern for Behaviors

```csharp
// Register repository with chained decorators
services.AddEntityFrameworkRepository<Customer, CoreDbContext>()
    .WithBehavior<RepositoryLoggingBehavior<Customer>>()          // Logs all operations
    .WithBehavior<RepositoryDomainEventPublisherBehavior<Customer>>() // Publishes domain events
    .WithBehavior<RepositoryAuditStateBehavior<Customer>>();      // Sets audit fields

// Execution order (onion pattern):
// Request → Logging → DomainEvents → Audit → Actual Repository
```

### Open Generic Registration

```csharp
// Register generic handler for all request types
services.AddScoped(typeof(IRequestHandler<,>), typeof(GenericCommandHandler<,>));

// Register generic repository for all entity types
services.AddScoped(typeof(IGenericRepository<>), typeof(EntityFrameworkRepository<>));

// Register generic validator for all command types
services.AddTransient(typeof(IValidator<>), typeof(FluentValidationValidator<>));
```

## Rationale

### Why Microsoft.Extensions.DependencyInjection

1. **Built-in**: Part of .NET, no additional dependencies
2. **Standards-Based**: Industry-standard DI container
3. **Performance**: Optimized for ASP.NET Core scenarios
4. **Tooling**: First-class support in Visual Studio, analyzers
5. **Ecosystem**: Works with most .NET libraries
6. **Simplicity**: Good balance of features vs. complexity

### Why Module-Based Registration

1. **Cohesion**: Services registered alongside related components
2. **Isolation**: Each module manages its own dependencies
3. **Maintainability**: Changes scoped to single module
4. **Testability**: Modules can be registered independently in tests
5. **Discovery**: Easy to find where services are registered

### Why Explicit Lifetimes

1. **Predictability**: Clear when objects are created/disposed
2. **Performance**: Avoid unnecessary object creation
3. **Safety**: Prevents captive dependencies
4. **Documentation**: Lifetime expresses intent

### Why Factory Pattern for Captive Dependencies

1. **Correctness**: Prevents `ObjectDisposedException` from scoped in singleton
2. **Explicit**: Code clearly shows scope creation
3. **Flexibility**: Can create multiple scopes per operation
4. **Standard Pattern**: Well-known .NET pattern

## Consequences

### Positive

- **Testability**: Easy to replace implementations with mocks via constructor injection
- **Modularity**: Each module self-contained with own service registrations
- **Lifetime Safety**: Factory pattern prevents captive dependencies
- **Performance**: Singleton caching for expensive, stateless services
- **Maintainability**: Services registered in predictable locations (module `Register()`)
- **Discoverability**: IDE navigation from interface to implementation
- **Composition**: Decorator pattern enables cross-cutting concerns
- **Validation**: Compile-time constructor checks ensure dependencies available

### Negative

- **Complexity**: Understanding lifetimes requires learning curve
- **Indirection**: More interfaces and abstractions
- **Debugging**: Stack traces deeper due to decorator chains
- **Boilerplate**: Factory pattern adds code in singletons
- **Resolution Failures**: Service not registered errors only at runtime

### Neutral

- **Module Registration**: Each module calls `services.Add*()` in `Register()` method
- **Open Generics**: Powerful but can be confusing for newcomers
- **Constructor Injection**: Preferred over property/method injection

## Implementation Guidelines

### Service Registration Checklist

When registering a service, ask:

1. **Is it stateless and thread-safe?** → Singleton
2. **Does it depend on DbContext/request data?** → Scoped
3. **Is it lightweight and stateless?** → Transient
4. **Does it implement IDisposable?** → Scoped or Transient (never Singleton)
5. **Is it injected into a singleton?** → Use factory pattern if scoped

### Module Registration Template

```csharp
public class MyModule : WebModuleBase
{
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        // 1. Configuration
        var moduleConfig = this.Configure<MyModuleConfiguration>(services, configuration);

        // 2. Domain Services
        services.AddScoped<IMyDomainService, MyDomainService>();

        // 3. Application Services (MediatR)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MyModule).Assembly));

        // 4. Infrastructure - Database
        services.AddSqlServerDbContext<MyDbContext>(o => o
            .UseConnectionString(moduleConfig.ConnectionStrings["Default"]));

        // 5. Infrastructure - Repositories
        services.AddEntityFrameworkRepository<MyEntity, MyDbContext>()
            .WithBehavior<RepositoryLoggingBehavior<MyEntity>>();

        // 6. Presentation - Endpoints
        services.AddEndpoints<MyEndpoints>();

        // 7. Presentation - Mapping
        services.AddMapping().WithMapster<MyModuleMapperRegister>();

        // 8. Jobs (if any)
        services.AddJobScheduling(o => o.StartupDelay("00:00:10"), configuration)
            .WithJob<MyJob>()
                .Cron(CronExpressions.EveryHour)
                .Named($"{this.Name}_{nameof(MyJob)}")
                .RegisterScoped();

        return services;
    }
}
```

### Service Lifetime Examples

```csharp
// Singleton - Configuration (immutable)
services.AddSingleton<IMyConfiguration>(sp =>
{
    var config = new MyConfiguration();
    configuration.Bind("MyModule", config);
    return config;
});

// Singleton - Mapper (stateless)
services.AddSingleton<IMapper>(sp =>
{
    var config = new TypeAdapterConfig();
    config.Scan(typeof(MyModule).Assembly);
    return new Mapper(config);
});

// Scoped - DbContext
services.AddDbContext<MyDbContext>(ServiceLifetime.Scoped);

// Scoped - Repository (depends on DbContext)
services.AddScoped<IGenericRepository<MyEntity>, EntityFrameworkRepository<MyEntity, MyDbContext>>();

// Scoped - Request Handler
services.AddScoped<IRequestHandler<MyCommand, Result>, MyCommandHandler>();

// Transient - Validator
services.AddTransient<IValidator<MyCommand>, MyCommand.Validator>();

// Transient - Specification
services.AddTransient<ISpecification<MyEntity>, MyEntitySpecification>();
```

### Avoiding Captive Dependencies

```csharp
// WRONG: Repository (scoped) injected into singleton
public class MySingletonService
{
    private readonly IGenericRepository<Customer> repository; // SCOPED dependency

    public MySingletonService(IGenericRepository<Customer> repository)
    {
        this.repository = repository; // Captured in singleton!
    }

    public async Task DoWork()
    {
        await this.repository.FindAllAsync(); // ObjectDisposedException after first request
    }
}

// Correct: Use IServiceScopeFactory
public class MySingletonService
{
    private readonly IServiceScopeFactory scopeFactory;

    public MySingletonService(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    public async Task DoWork()
    {
        using var scope = this.scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<Customer>>();

        await repository.FindAllAsync();
    }
}
```

### Testing with DI

```csharp
// Override services in WebApplicationFactory
builder.ConfigureServices(services =>
{
    // Replace real repository with mock
    services.RemoveAll<IGenericRepository<Customer>>();
    services.AddScoped<IGenericRepository<Customer>>(sp =>
    {
        var mock = Substitute.For<IGenericRepository<Customer>>();
        mock.FindAllAsync().Returns(Result<IEnumerable<Customer>>.Success(testCustomers));
        return mock;
    });
});
```

### Open Generic Registration Example

```csharp
// Register IGenericRepository<T> for all entity types
services.AddScoped(typeof(IGenericRepository<>), typeof(EntityFrameworkRepository<,>));

// Resolves to:
// IGenericRepository<Customer> → EntityFrameworkRepository<Customer, CoreDbContext>
// IGenericRepository<Order> → EntityFrameworkRepository<Order, OrderDbContext>

// Register IRequestHandler<TRequest, TResponse> for all commands
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CoreModule).Assembly));
```

### Conditional Registration

```csharp
// Register different implementations based on environment
if (environment.IsDevelopment())
{
    services.AddSingleton<IEmailSender, FakeEmailSender>();
}
else
{
    services.AddSingleton<IEmailSender, SmtpEmailSender>();
}

// Register based on configuration
if (moduleConfiguration.UseCache)
{
    services.AddSingleton<ICache, RedisCache>();
}
else
{
    services.AddSingleton<ICache, MemoryCache>();
}
```

## Alternatives Considered

### Alternative 1: Autofac

```csharp
var builder = new ContainerBuilder();
builder.RegisterType<CustomerRepository>().As<IGenericRepository<Customer>>().InstancePerLifetimeScope();
```

**Rejected because**:

- Additional dependency (Microsoft.Extensions.DependencyInjection built-in)
- ASP.NET Core optimized for built-in container
- More features than needed for this application
- Team already familiar with MS DI

### Alternative 2: Manual Service Creation (No DI)

```csharp
var repository = new CustomerRepository(new CoreDbContext());
var handler = new CustomerCreateCommandHandler(repository, logger);
```

**Rejected because**:

- No lifetime management (manual disposal)
- Hard to test (tightly coupled)
- No decorator patterns
- Violates dependency inversion principle

### Alternative 3: Service Locator Pattern

```csharp
var repository = ServiceLocator.GetService<IGenericRepository<Customer>>();
```

**Rejected because**:

- Anti-pattern (hides dependencies)
- Harder to test (requires global state)
- No compile-time safety
- Obscures dependency graph

### Alternative 4: Property Injection

```csharp
public class MyService
{
    [Inject]
    public ILogger Logger { get; set; }
}
```

**Rejected because**:

- Not well-supported in Microsoft.Extensions.DependencyInjection
- Constructor injection preferred (explicit dependencies)
- Nullable properties require null checks
- Harder to enforce required dependencies

## Related Decisions

- [ADR-0003](0003-modular-monolith-architecture.md): Module-based registration pattern
- [ADR-0015](0015-background-jobs-quartz-scheduling.md): Factory pattern for scoped dependencies in jobs
- [ADR-0017](0017-integration-testing-strategy.md): Service replacement in tests
- [ADR-0004](0004-repository-decorator-behaviors.md): Decorator registration pattern

## References

- [.NET Dependency Injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Service Lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes)
- [Captive Dependency](https://blog.ploeh.dk/2014/06/02/captive-dependency/)
- [bITdevKit Module Infrastructure](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-modules.md)

## Notes

### Common Registration Patterns

```csharp
// Simple registration
services.AddScoped<IMyService, MyService>();

// Factory registration
services.AddScoped<IMyService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<MyService>>();
    var config = sp.GetRequiredService<IConfiguration>();
    return new MyService(logger, config["MyKey"]);
});

// Implementation instance
services.AddSingleton<IMyService>(new MyService());

// Try add (only if not already registered)
services.TryAddScoped<IMyService, MyService>();

// Replace existing registration
services.Replace(ServiceDescriptor.Scoped<IMyService, NewMyService>());

// Remove registration
services.RemoveAll<IMyService>();
```

### Lifetime Diagnostics

```csharp
// Enable scope validation in development
public static IHost CreateHost(string[] args)
{
    return Host.CreateDefaultBuilder(args)
        .UseDefaultServiceProvider((context, options) =>
        {
            options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
            options.ValidateOnBuild = true;
        })
        .Build();
}
```

### Common Pitfalls

WRONG **Captive Dependency**:

```csharp
services.AddSingleton<MySingleton>(); // Captures scoped DbContext
```

WRONG **Disposable Singleton**:

```csharp
services.AddSingleton<IDisposable, MyDisposable>(); // Never disposed
```

WRONG **Circular Dependency**:

```csharp
services.AddScoped<A>(); // Depends on B
services.AddScoped<B>(); // Depends on A
```

CORRECT **Use Lazy\<T> for circular dependencies**:

```csharp
public class A(Lazy<B> lazyB) { }
public class B(Lazy<A> lazyA) { }
```

### Implementation Location

- **Module Registration**: `src/Modules/<Module>/<Module>.Presentation/ModuleModule.cs`
- **Service Interfaces**: `src/Modules/<Module>/<Module>.Application/` (abstractions)
- **Service Implementations**: Layer-specific folders (Domain, Application, Infrastructure)
