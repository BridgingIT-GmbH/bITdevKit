# ADR-0016: Logging & Observability Strategy with Serilog

## Status

Accepted

## Context

Modern distributed applications require comprehensive logging and observability for:

- **Troubleshooting**: Diagnosing production issues and understanding system behavior
- **Performance Monitoring**: Identifying bottlenecks and optimization opportunities
- **Audit Trails**: Tracking user actions and system changes
- **Security Analysis**: Detecting anomalies and security incidents
- **Business Intelligence**: Understanding usage patterns and feature adoption
- **Correlation**: Tracing requests across services and layers

Traditional logging approaches face challenges:

- **Unstructured Logs**: Free-text logs difficult to query and analyze
- **Context Loss**: Missing correlation IDs across distributed operations
- **Verbosity**: Too much noise or too little signal
- **Performance Impact**: Synchronous logging blocks request threads
- **Centralization**: Logs scattered across multiple files/locations
- **Retention**: No automated log rotation or cleanup

The application needed a logging strategy that:

1. Provides **structured logging** with queryable properties
2. Supports **multiple sinks** (console, file, centralized aggregation)
3. Enables **contextual enrichment** (correlation ID, thread ID, module)
4. Integrates with **distributed tracing** (OpenTelemetry)
5. Offers **configurable log levels** per namespace
6. Maintains **high performance** with async sinks
7. Follows **.NET logging abstractions** (`ILogger<T>`)

## Decision

Adopt **Serilog** as the primary logging framework with **structured logging**, **enrichers** for contextual data, **multiple sinks** (Console, File, Seq, OpenTelemetry), and **configuration-based log level control**.

### Serilog Configuration

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File", "Serilog.Sinks.OpenTelemetry", "Serilog.Sinks.Seq"],
    "Enrich": [
      "FromLogContext",
      "WithEnvironmentName",
      "WithMachineName",
      "WithThreadId",
      "WithShortTypeName"
    ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Literate, Serilog.Sinks.Console",
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] cid:{CorrelationId} fid:{FlowId} tid:{ThreadId} | mod:{ModuleName} | {ShortTypeName} | {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] cid:{CorrelationId} fid:{FlowId} tid:{ThreadId} | mod:{ModuleName} | {ShortTypeName} | {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "OpenTelemetry",
        "Args": {
          "endpoint": "http://localhost:4317",
          "protocol": "Grpc"
        }
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341"
        }
      }
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

### Structured Logging Pattern

```csharp
// V GOOD: Structured logging with named properties
this.logger.LogInformation(
    "Customer {CustomerId} created with email {Email} by user {UserId}",
    customer.Id,
    customer.Email.Value,
    userId);

// WRONG: String interpolation loses structure
this.logger.LogInformation($"Customer {customer.Id} created");

// V GOOD: Exception logging with context
this.logger.LogError(
    exception,
    "Failed to process order {OrderId} for customer {CustomerId}",
    orderId,
    customerId);
```

### Enrichers for Context

```csharp
// Enrichers automatically add properties to all log events
.Enrich.FromLogContext()          // Adds properties via LogContext.PushProperty()
.Enrich.WithEnvironmentName()     // Adds "EnvironmentName" (Development, Staging, Production)
.Enrich.WithMachineName()          // Adds "MachineName" for multi-instance debugging
.Enrich.WithThreadId()             // Adds "ThreadId" for concurrency analysis
.Enrich.WithShortTypeName()        // Adds "ShortTypeName" for logger source class
```

### Correlation ID Propagation

```csharp
// Middleware adds correlation ID to all requests
app.UseRequestCorrelation();

// Enricher makes it available to all logs in request scope
LogContext.PushProperty("CorrelationId", correlationId);

// Logs automatically include CorrelationId
this.logger.LogInformation("Processing request"); // Includes CorrelationId
```

### Log Level Configuration

```json
{
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",                      // Suppress EF/ASP.NET noise
      "Microsoft.Hosting.Lifetime": "Information", // Keep startup messages
      "Microsoft.EntityFrameworkCore": "Warning",  // Suppress query logs
      "System": "Warning",                         // Suppress system noise
      "MyApp.Domain": "Debug",                     // Enable debug for domain
      "MyApp.Application.Commands": "Trace"        // Trace specific commands
    }
  }
}
```

## Rationale

### Why Serilog

1. **Structured Logging**: First-class support for structured log events with named properties
2. **Multiple Sinks**: Write to console, file, Seq, OpenTelemetry, Application Insights, etc.
3. **Performance**: Async sinks prevent blocking request threads
4. **Enrichers**: Automatic property injection (correlation ID, thread ID, environment)
5. **Configuration**: JSON-based configuration without code changes
6. **Ecosystem**: Rich ecosystem of sinks, enrichers, and formatters
7. **ILogger Integration**: Seamless integration with .NET `ILogger<T>` abstraction

### Why Multiple Sinks

1. **Console Sink**: Development debugging with color-coded output
2. **File Sink**: Persistent logs with daily rotation for auditing
3. **Seq Sink**: Centralized log aggregation with powerful querying (SQL-like)
4. **OpenTelemetry Sink**: Distributed tracing integration with spans/metrics

### Why Enrichers

1. **CorrelationId**: Trace requests across layers, services, and async boundaries
2. **ThreadId**: Debug concurrency issues and thread pool starvation
3. **ModuleName**: Filter logs by module in modular monolith
4. **ShortTypeName**: Identify logger source class without full namespace
5. **EnvironmentName**: Distinguish logs from different deployment environments
6. **MachineName**: Debug issues in multi-instance deployments

### Why Structured Properties

1. **Queryability**: Seq/Splunk can filter by specific property values
2. **Performance**: Indexed properties faster than full-text search
3. **Consistency**: Named properties enforce structure across team
4. **Aggregation**: Group/count logs by specific dimensions
5. **Alerting**: Create alerts based on specific property values

## Consequences

### Positive

- **Powerful Querying**: Seq provides SQL-like queries over structured properties
- **Correlation**: CorrelationId traces requests across all layers and async operations
- **Performance**: Async sinks prevent logging from blocking request threads
- **Flexibility**: Add/remove sinks via configuration without code changes
- **Debugging**: ThreadId/ModuleName/ShortTypeName provide rich debugging context
- **Production Diagnostics**: Centralized logs (Seq) with powerful filtering
- **OpenTelemetry Integration**: Logs correlated with distributed traces and metrics
- **Cost Control**: File retention and log level tuning reduce storage costs
- **Security**: Sensitive data can be filtered/masked before logging

### Negative

- **Learning Curve**: Team must learn structured logging syntax and Seq query language
- **Configuration Complexity**: Managing log levels across many namespaces
- **Infrastructure**: Requires Seq or similar log aggregation infrastructure
- **Storage Costs**: High-volume logs consume significant storage
- **Performance Overhead**: Structured logging slightly slower than simple text logging
- **Sink Failures**: If Seq is down, logs may be lost (unless buffered)

### Neutral

- **Output Templates**: Customizable per sink but requires balancing readability vs. detail
- **Sensitive Data**: Must be careful not to log PII or secrets (use masking enrichers)
- **Log Retention**: Configured per sink (daily rotation for files, retention policy in Seq)

## Implementation Guidelines

### Structured Logging Best Practices

```csharp
// Use named properties
this.logger.LogInformation("Order {OrderId} placed by {UserId}", orderId, userId);

// Avoid string interpolation
this.logger.LogInformation($"Order {orderId} placed");

// Log complex objects (serialized to JSON)
this.logger.LogInformation("Order created: {@Order}", order);

// Use destructuring operator @ for objects
this.logger.LogDebug("Request received: {@Request}", request);

// Don't log full entities (causes EF lazy loading)
this.logger.LogInformation("Customer: {@Customer}", customer); // May load all navigation properties
```

### Log Level Guidelines

- **Trace**: Very detailed diagnostics (e.g., entering/exiting methods)
- **Debug**: Detailed information useful during development (e.g., variable values)
- **Information**: General informational messages (e.g., request started/completed)
- **Warning**: Unexpected but recoverable issues (e.g., retry succeeded)
- **Error**: Errors requiring attention (e.g., operation failed)
- **Critical**: Application-wide failures (e.g., database unavailable)

```csharp
// Trace: Method execution flow
this.logger.LogTrace("Entering {MethodName}", nameof(ProcessOrder));

// Debug: Variable values
this.logger.LogDebug("Processing order {OrderId} with {ItemCount} items", orderId, items.Count);

// Information: Business events
this.logger.LogInformation("Order {OrderId} placed successfully", orderId);

// Warning: Recoverable issues
this.logger.LogWarning("Order {OrderId} payment delayed, will retry", orderId);

// Error: Failures
this.logger.LogError(exception, "Failed to process order {OrderId}", orderId);

// Critical: System-wide failures
this.logger.LogCritical("Database connection failed, application cannot start");
```

### Sensitive Data Handling

```csharp
// DON'T log sensitive data
this.logger.LogInformation("User logged in with password {Password}", password);

// DO log non-sensitive identifiers
this.logger.LogInformation("User {UserId} logged in successfully", userId);

// DO mask sensitive properties
this.logger.LogInformation("Email sent to {Email}", MaskEmail(email));

// DO use custom destructuring for sensitive objects
public class OrderLogView
{
    public Guid OrderId { get; init; }
    public decimal Total { get; init; }
    // Exclude credit card, PII
}

this.logger.LogInformation("Order created: {@Order}", new OrderLogView { ... });
```

### Correlation Context

```csharp
// Add properties to current scope (applies to all logs in scope)
using (LogContext.PushProperty("OrderId", orderId))
using (LogContext.PushProperty("UserId", userId))
{
    this.logger.LogInformation("Processing order");
    await ProcessOrder();
    this.logger.LogInformation("Order completed");
    // Both logs automatically include OrderId and UserId
}
```

### Exception Logging

```csharp
try
{
    await repository.AddAsync(customer);
}
catch (DbUpdateException ex)
{
    // CORRECT Pass exception as first parameter (structured)
    this.logger.LogError(ex, "Failed to save customer {CustomerId}", customer.Id);

    // WRONG Don't stringify exception
    this.logger.LogError("Failed: " + ex.ToString());
}
```

### Module-Specific Logging

```csharp
// Each class gets typed logger
public class CustomerCreateCommandHandler(ILogger<CustomerCreateCommandHandler> logger)
{
    public async Task<Result<CustomerId>> Handle(...)
    {
        // ShortTypeName enricher extracts "CustomerCreateCommandHandler"
        this.logger.LogInformation("Creating customer with email {Email}", request.Email);
    }
}
```

### Performance Logging

```csharp
// Log operation duration
var sw = Stopwatch.StartNew();
try
{
    await DoWork();
    this.logger.LogInformation("Operation completed in {DurationMs}ms", sw.ElapsedMilliseconds);
}
catch (Exception ex)
{
    this.logger.LogError(ex, "Operation failed after {DurationMs}ms", sw.ElapsedMilliseconds);
}
```

## Alternatives Considered

### Alternative 1: NLog

**Rejected because**:

- Less idiomatic structured logging syntax
- Fewer ecosystem sinks compared to Serilog
- Serilog has stronger community momentum in .NET space
- Serilog's fluent configuration more maintainable

### Alternative 2: Built-in .NET Logging (Microsoft.Extensions.Logging only)

**Rejected because**:

- Limited structured logging support
- No enrichers or contextual properties
- Basic file provider lacks rotation
- No centralized log aggregation sinks
- Serilog provides better production-ready features

### Alternative 3: Application Insights Only

**Rejected because**:

- Azure-specific (vendor lock-in)
- Higher cost for high-volume logging
- Requires cloud connectivity
- Preference for self-hosted Seq for development
- Can still integrate via Serilog sink if needed

### Alternative 4: ELK Stack (Elasticsearch, Logstash, Kibana)

**Rejected because**:

- Higher infrastructure complexity (three components)
- Resource-intensive (Elasticsearch memory usage)
- Seq simpler for .NET-specific needs
- Serilog can still write to Elasticsearch if needed later

## Related Decisions

- [ADR-0015](0015-background-jobs-quartz-scheduling.md): Jobs use structured logging via `JobBase`
- [ADR-0017](0017-integration-testing-strategy.md): Tests capture logs via `ITestOutputHelper`
- [ADR-0003](0003-modular-monolith-architecture.md): ModuleName enricher enables module-specific filtering
- [ADR-0004](0004-repository-decorator-behaviors.md): Repository logging behavior uses structured logging

## References

- [Serilog Documentation](https://github.com/serilog/serilog/wiki)
- [Serilog Best Practices](https://github.com/serilog/serilog/wiki/Writing-Log-Events)
- [Seq Documentation](https://docs.datalust.co/docs)
- [OpenTelemetry Logging](https://opentelemetry.io/docs/instrumentation/net/logging/)
- [.NET Logging Guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)

## Notes

### Seq Query Examples

```sql
-- Find all errors for specific customer
CustomerId = '12345' AND @Level = 'Error'

-- Find slow operations
DurationMs > 1000

-- Count errors by exception type
SELECT Count(*) FROM stream GROUP BY @Exception

-- Find requests by correlation ID
CorrelationId = 'abc-123-def'
```

### Environment-Specific Configuration

```json
// appsettings.Development.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"  // More verbose in dev
    }
  }
}

// appsettings.Production.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"  // Less verbose in prod
    },
    "WriteTo": [
      {
        "Name": "ApplicationInsights",  // Add prod-specific sinks
        "Args": { "instrumentationKey": "..." }
      }
    ]
  }
}
```

### Log Retention

- **File Sink**: Daily rolling, delete files older than 30 days (configured via `retainedFileCountLimit`)
- **Seq**: Retention policy configured in Seq UI (default 30 days)
- **OpenTelemetry**: Retention managed by backend (Jaeger, Tempo, etc.)

### Performance Tips

1. **Use async sinks** to avoid blocking request threads
2. **Filter at source** via MinimumLevel configuration
3. **Avoid logging in tight loops** - aggregate and log summary
4. **Use scoped properties** instead of repeating in every log statement
5. **Destructure carefully** - `@` operator serializes entire object graph

### Common Pitfalls

WRONG **Logging sensitive data**:

```csharp
this.logger.LogInformation("User login: {@User}", user); // May contain password hash
```

WRONG **Over-logging in hot paths**:

```csharp
foreach (var item in items)
{
    this.logger.LogDebug("Processing item {ItemId}", item.Id); // Log thousands of times
}
```

Correct **Log summary instead**:

```csharp
this.logger.LogInformation("Processing {ItemCount} items", items.Count);
```

WRONG **String interpolation**:

```csharp
this.logger.LogInformation($"Order {orderId}"); // Loses structured property
```

### Testing with Logs

```csharp
// Integration tests can capture logs
public class CustomerEndpointsTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CreateCustomer_LogsCreationEvent()
    {
        // Arrange
        var sink = new TestSink();
        Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        // Act
        await this.CreateCustomer();

        // Assert
        sink.LogEvents.Should().Contain(e =>
            e.MessageTemplate.Text.Contains("Customer {CustomerId} created"));
    }
}
```
