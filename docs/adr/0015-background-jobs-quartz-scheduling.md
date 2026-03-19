# ADR-0015: Background Jobs & Scheduling with Quartz.NET

## Status

Accepted

## Context

Enterprise applications require reliable background job execution for tasks like:

- **Scheduled Operations**: Regular data exports, report generation, cleanup tasks
- **Long-Running Operations**: Data processing that shouldn't block HTTP requests
- **Recurring Tasks**: Hourly, daily, weekly operations (CRON-based scheduling)
- **Deferred Processing**: Tasks triggered by events but executed asynchronously
- **Maintenance Operations**: Database maintenance, cache warming, health checks

Without a robust scheduling solution, applications face:

- **Reliability Issues**: Jobs failing without retry logic or monitoring
- **Concurrency Problems**: Multiple instances executing same job simultaneously
- **Persistence Challenges**: Lost jobs after application restarts
- **Scalability Limits**: Manual coordination across multiple application instances
- **Monitoring Gaps**: No visibility into job execution success/failure

The application needed a scheduling strategy that:

1. Provides reliable, persistent job scheduling
2. Supports CRON expressions for flexible timing
3. Prevents concurrent execution of same job
4. Enables retry logic for transient failures
5. Integrates with dependency injection and logging
6. Scales across multiple application instances

## Decision

Adopt **Quartz.NET** for background job scheduling with **bITdevKit integration** providing standardized job patterns, retry configuration, and module-level registration.

### Job Implementation Pattern

Jobs derive from `JobBase` and override `Process()` method:

```csharp
[DisallowConcurrentExecution]
public class CustomerExportJob(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory) : JobBase(loggerFactory), IRetryJobScheduling
{
    RetryJobSchedulingOptions IRetryJobScheduling.Options => new()
    {
        Attempts = 3,
        Backoff = TimeSpan.FromSeconds(1)
    };

    public override async Task Process(
        IJobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<Customer>>();

        this.Logger.LogInformation("{JobName}: Starting export operation", nameof(CustomerExportJob));

        var customersResult = await repository.FindAllResultAsync(cancellationToken: cancellationToken);
        if (customersResult.IsFailure)
        {
            this.Logger.LogError("{JobName}: Failed: {Error}", nameof(CustomerExportJob), customersResult.ToString());
            return;
        }

        foreach (var customer in customersResult.Value)
        {
            this.Logger.LogInformation("{JobName}: Exporting customer (id={CustomerId})", nameof(CustomerExportJob), customer.Id);
            // Export logic here
        }
    }
}
```

### Job Registration Pattern

Jobs registered per-module using fluent configuration:

```csharp
// In CoreModuleModule.cs
services.AddJobScheduling(o => o
    .StartupDelay(configuration["JobScheduling:StartupDelay"]), configuration)
    .WithJob<CustomerExportJob>()
        .Cron(CronExpressions.EveryMinute)
        .Named($"{this.Name}_{nameof(CustomerExportJob)}")
        .RegisterScoped();
```

### Key Features

1. **Persistent Scheduling**: Jobs stored in database (Quartz tables)
2. **CRON Expressions**: Flexible timing using standard CRON syntax
3. **Concurrency Control**: `[DisallowConcurrentExecution]` attribute prevents overlapping runs
4. **Retry Configuration**: `IRetryJobScheduling` interface enables automatic retries with backoff
5. **Scoped Dependencies**: `IServiceScopeFactory` for proper scoped service resolution
6. **Structured Logging**: Logger passed to `JobBase` for consistent logging
7. **Module Isolation**: Jobs registered per-module with naming prefix
8. **Startup Delay**: Configurable delay before scheduler starts

## Rationale

### Why Quartz.NET

1. **Industry Standard**: Mature, battle-tested job scheduling framework (.NET port of Java Quartz)
2. **Persistence**: Database-backed job storage survives application restarts
3. **Clustering**: Supports load balancing across multiple application instances
4. **CRON Support**: Rich CRON expression syntax for complex schedules
5. **Flexibility**: Supports simple triggers, calendar-based schedules, custom triggers
6. **Community**: Large community, extensive documentation, active maintenance
7. **Integration**: First-class .NET integration with DI, configuration, logging

### Why bITdevKit Job Abstractions

1. **Standardization**: Consistent job pattern across all modules
2. **Retry Logic**: Built-in retry configuration via `IRetryJobScheduling`
3. **Logging**: Automatic logger injection via `JobBase`
4. **Scoping**: Proper handling of scoped dependencies via `IServiceScopeFactory`
5. **Registration**: Fluent API simplifies job configuration
6. **Module Alignment**: Jobs registered alongside other module concerns

### Why Database Persistence

1. **Reliability**: Jobs survive application crashes and restarts
2. **Clustering**: Shared job state across multiple instances
3. **Auditability**: Job execution history stored for debugging
4. **Coordination**: Prevents duplicate execution across instances
5. **Scalability**: Horizontal scaling without additional coordination infrastructure

## Consequences

### Positive

- **Reliability**: Jobs automatically retry on transient failures with configurable backoff
- **Persistence**: Job schedules survive application restarts
- **Scalability**: Horizontal scaling with automatic coordination via database
- **Flexibility**: CRON expressions support any scheduling pattern
- **Monitoring**: Structured logging provides visibility into job execution
- **Testability**: Jobs can be unit tested independently of scheduler
- **Consistency**: Standard pattern across all background jobs
- **Integration**: Seamless DI integration via `IServiceScopeFactory`
- **Prevention**: `[DisallowConcurrentExecution]` prevents resource contention

### Negative

- **Database Overhead**: Quartz tables add database complexity and storage
- **Configuration Complexity**: CRON expressions require learning curve
- **Migration Overhead**: Database migrations needed for Quartz tables
- **Performance**: Database persistence adds latency compared to in-memory scheduling
- **Dependencies**: Additional NuGet packages (Quartz.NET, Quartz.Serialization.Json)

### Neutral

- **Module Registration**: Each module registers its own jobs in `Module.cs`
- **Scoping Pattern**: Jobs must manually create scopes for scoped dependencies
- **Startup Delay**: Configurable delay prevents job execution during application startup
- **Naming Convention**: Jobs named with module prefix for identification

## Implementation Guidelines

### Job Class Template

```csharp
namespace <Module>.Application;

using BridgingIT.DevKit.Application.JobScheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

[DisallowConcurrentExecution]
public class <JobName>Job(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory) : JobBase(loggerFactory), IRetryJobScheduling
{
    RetryJobSchedulingOptions IRetryJobScheduling.Options => new()
    {
        Attempts = 3,
        Backoff = TimeSpan.FromSeconds(1)
    };

    public override async Task Process(
        IJobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();

        // Resolve scoped dependencies
        var service = scope.ServiceProvider.GetRequiredService<IMyService>();

        this.Logger.LogInformation("{JobName}: Starting", nameof(<JobName>Job));

        try
        {
            // Job logic here
            await service.DoWorkAsync(cancellationToken);

            this.Logger.LogInformation("{JobName}: Completed successfully", nameof(<JobName>Job));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "{JobName}: Failed with exception", nameof(<JobName>Job));
            throw; // Re-throw to trigger retry
        }
    }
}
```

### Job Registration Template

```csharp
// In ModuleModule.cs
services.AddJobScheduling(o => o
    .StartupDelay(configuration["JobScheduling:StartupDelay"]), configuration)
    .WithJob<MyJob>()
        .Cron(CronExpressions.EveryHour) // or custom: "0 0 2 * * ?" (daily at 2 AM)
        .Named($"{this.Name}_{nameof(MyJob)}")
        .RegisterScoped(); // or .RegisterSingleton()
```

### Common CRON Expressions

```csharp
// bITdevKit provides constants:
CronExpressions.EveryMinute      // "0 * * * * ?"
CronExpressions.EveryFiveMinutes // "0 */5 * * * ?"
CronExpressions.EveryHour        // "0 0 * * * ?"
CronExpressions.EveryDay         // "0 0 0 * * ?"
CronExpressions.EveryWeek        // "0 0 0 ? * SUN"
CronExpressions.EveryMonth       // "0 0 0 1 * ?"

// Custom examples:
"0 0 2 * * ?"        // Daily at 2 AM
"0 30 8 ? * MON-FRI" // Weekdays at 8:30 AM
"0 0 12 1 * ?"       // First day of month at noon
"0 0/15 * * * ?"     // Every 15 minutes
```

### Retry Configuration

```csharp
RetryJobSchedulingOptions IRetryJobScheduling.Options => new()
{
    Attempts = 5,                        // Number of retry attempts
    Backoff = TimeSpan.FromSeconds(2)    // Delay between retries (exponential)
};
```

### Scoped Dependencies

```csharp
public override async Task Process(
    IJobExecutionContext context,
    CancellationToken cancellationToken = default)
{
    using var scope = scopeFactory.CreateScope();

    // Resolve scoped services
    var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<T>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();
    var scopedService = scope.ServiceProvider.GetRequiredService<IScopedService>();

    // Use services within scope
    // ...
}
```

### Job Context Data

```csharp
public override async Task Process(
    IJobExecutionContext context,
    CancellationToken cancellationToken = default)
{
    // Access job data (passed during registration or trigger)
    var param = context.MergedJobDataMap.GetString("ParameterKey");

    // Store result for next execution
    context.Result = "Job completed successfully";
}
```

### Testing Jobs

```csharp
[Fact]
public async Task Process_WithCustomers_ExportsSuccessfully()
{
    // Arrange
    var loggerFactory = this.ServiceProvider.GetService<ILoggerFactory>();
    var scopeFactory = this.ServiceProvider.GetService<IServiceScopeFactory>();
    var job = new CustomerExportJob(loggerFactory, scopeFactory);
    var context = Substitute.For<IJobExecutionContext>();

    // Act
    await job.Process(context, CancellationToken.None);

    // Assert - no exception thrown means success
}
```

## Alternatives Considered

### Alternative 1: Hangfire

```csharp
BackgroundJob.Enqueue(() => ExportCustomers());
RecurringJob.AddOrUpdate("export", () => ExportCustomers(), Cron.Daily);
```

**Rejected because**:

- Less mature clustering support
- Dashboard is overkill for simple scheduling
- More opinionated about persistence and configuration
- Quartz.NET is more established in enterprise .NET

### Alternative 2: Azure Functions / AWS Lambda (Serverless)

**Rejected because**:

- Requires cloud infrastructure
- More complex deployment and monitoring
- Higher cost for frequent jobs
- Not suitable for self-hosted scenarios
- Preference for keeping jobs in-process with application

### Alternative 3: HostedService with Timer

```csharp
public class TimerHostedService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWork();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

**Rejected because**:

- No CRON expression support
- No persistence (lost on restart)
- No retry logic
- No clustering support
- Manual coordination needed for multiple instances

### Alternative 4: Windows Task Scheduler / Cron Jobs (OS-Level)

**Rejected because**:

- Platform-dependent
- No integration with application logging/DI
- Harder to test
- Requires separate deployment artifacts
- No shared state with application

## Related Decisions

- [ADR-0003](0003-modular-monolith-architecture.md): Jobs registered per-module
- [ADR-0016](0016-logging-observability-strategy.md): Structured logging in jobs via `JobBase`
- [ADR-0017](0017-dependency-injection-service-lifetimes.md): Scoped dependency resolution pattern
- [ADR-0007](0007-entity-framework-core-code-first-migrations.md): Quartz tables added via migration

## References

- [Quartz.NET Documentation](https://www.quartz-scheduler.net/documentation/)
- [Quartz.NET GitHub](https://github.com/quartznet/quartznet)
- [CRON Expression Guide](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html)
- [bITdevKit Job Scheduling](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-jobscheduling.md)

## Notes

### Quartz Tables

Migration adds Quartz persistence tables to database:

- `QRTZ_JOB_DETAILS`: Job definitions
- `QRTZ_TRIGGERS`: Trigger schedules
- `QRTZ_CRON_TRIGGERS`: CRON trigger details
- `QRTZ_FIRED_TRIGGERS`: Currently executing jobs
- `QRTZ_LOCKS`: Cluster coordination locks
- `QRTZ_SCHEDULER_STATE`: Scheduler instance state

### Configuration

```json
{
  "JobScheduling": {
    "StartupDelay": "00:00:10"
  }
}
```

### Monitoring

Jobs can be monitored via:

- Structured logs (Serilog to Seq/OpenTelemetry)
- Quartz Admin UI (separate package)
- Custom health checks querying Quartz tables
- OpenTelemetry tracing spans

### Best Practices

1. **Always use `[DisallowConcurrentExecution]`** to prevent resource contention
2. **Always implement `IRetryJobScheduling`** for transient failure handling
3. **Always create scopes** for scoped dependencies via `IServiceScopeFactory`
4. **Always log** start, success, and failure with structured data
5. **Keep jobs idempotent** - safe to run multiple times
6. **Use CancellationToken** to support graceful shutdown
7. **Avoid long-running jobs** - break into smaller units or use separate processing queue

### Common Pitfalls

WRONG **Injecting scoped dependencies directly into job constructor**:

```csharp
// WRONG - DbContext is scoped, job is singleton
public class MyJob(ILoggerFactory loggerFactory, MyDbContext dbContext) : JobBase(loggerFactory)
```

CORRECT **Use IServiceScopeFactory instead**:

```csharp
public class MyJob(ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory) : JobBase(loggerFactory)
{
    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();
    }
}
```

### Implementation Location

- **Jobs**: `src/Modules/<Module>/<Module>.Application/Jobs/`
- **Job Registration**: `src/Modules/<Module>/<Module>.Presentation/ModuleModule.cs`
- **Job Tests**: `tests/Modules/<Module>/<Module>.UnitTests/Application/Jobs/`
- **Quartz Tables**: Added via EF Core migration in Infrastructure project
