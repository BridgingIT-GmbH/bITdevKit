
# Log Entries

> Query, stream, export, and manage persisted application logs through a stable application API.

[TOC]

## Overview

The Log Entries feature provides an application-level API for querying, streaming, exporting, and cleaning up persisted logs. It does not replace the logging pipeline itself. Instead, it gives the rest of the devkit a stable contract for operational access to log data once logs have already been written to a store.

`Application.Utilities` defines the contract in `ILogEntryService` and the DTOs used by callers. Infrastructure projects provide concrete implementations, and `Presentation.Web` exposes a ready-made HTTP endpoint set.

## Challenges

Persisted application logs are useful only when operators can search them without coupling application code to a sink schema. Querying a large log table also requires bounded pages, continuation, validation, and controlled maintenance so that dashboards and cleanup work do not become unbounded database operations.

## Solution

`ILogEntryService` defines provider-neutral query, stream, export, statistics, subscription, and cleanup operations. The SQL Server EF Core implementation maps those operations to an `ILoggingContext`, and the maintenance service processes archival and deletion requests outside the request path. Optional minimal API endpoints expose the same application contract.

## Key Features

- validated filtering by time, severity, trace, correlation, module, log key, type, and text
- continuation-token and tail-query support
- polling-based asynchronous streaming and subscriptions
- CSV, JSON, and plain-text export
- aggregated statistics by level and interval
- queued archival and deletion maintenance
- authorization-enabled operational endpoints

## Architecture

Application and presentation code depend on `ILogEntryService`. `LogEntryService<TContext>` queries a context that implements `ILoggingContext`, while `LogEntryMaintenanceQueue` and `LogEntryMaintenanceService<TContext>` separate retention work from request processing. The logging sink writes the rows; this feature reads and maintains them.

## Use Cases

- show recent warnings or errors in an operations dashboard
- follow new entries for a support console
- export a filtered time range for offline analysis
- correlate records from one distributed request or module
- archive and remove retained entries in bounded batches

## Basic Usage

This example assumes `AppDbContext` is already registered, implements `ILoggingContext`, and points to the same SQL Server table used by the logging sink. It registers the application service and maintenance worker, then exposes a bounded query with safe error handling and a visible result.

```csharp
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ILogEntryService, LogEntryService<AppDbContext>>();
builder.Services.AddSingleton<LogEntryMaintenanceQueue>();
builder.Services.AddHostedService<LogEntryMaintenanceService<AppDbContext>>();

var app = builder.Build();

app.MapGet("/ops/recent-errors", async (
    ILogEntryService logs,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await logs.QueryAsync(new LogEntryQueryRequest
        {
            StartTime = DateTimeOffset.UtcNow.AddHours(-1),
            Level = LogLevel.Error,
            PageSize = 100
        }, cancellationToken);

        return Results.Ok(new
        {
            Count = response.Items.Count,
            Entries = response.Items,
            response.ContinuationToken
        });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { exception.Message });
    }
    catch (Exception exception)
    {
        loggerFactory.CreateLogger("LogEntries")
            .LogError(exception, "Could not query persisted log entries");
        return Results.Problem("The log query failed.");
    }
});

app.Run();
```

`GET /ops/recent-errors` returns at most 100 error-or-higher entries from the previous hour and includes the continuation token when another page is available.

## Capabilities

- paged log queries with continuation tokens
- polling-based asynchronous enumeration and callback subscriptions
- export to CSV, JSON, or plain text
- aggregated statistics by level and time interval
- cleanup and archival-oriented maintenance operations
- correlation-oriented filtering by trace, correlation, module, and log key metadata

## Core contract

The central abstraction is `ILogEntryService`.

It exposes these operations:

- `QueryAsync(...)`
- `StreamAsync(...)`
- `ExportAsync(...)`
- `GetStatisticsAsync(...)`
- `CleanupAsync(...)`
- `SubscribeAsync(...)`

The application package also defines:

- `LogEntryQueryRequest`
- `LogEntryQueryResponse`
- `LogEntryModel`
- `LogEntryStatisticsModel`
- `LogEntryExportFormat`

## Setup

The log-entries feature needs more than just `ILogEntryService`. A working setup has four parts:

1. the application must write structured logs into a persistent store
2. the EF Core context used by the query service must implement `ILoggingContext`
3. the host must register `ILogEntryService` plus the maintenance queue and hosted service
4. the web host can optionally expose `LogEntryEndpoints`

The DoFiesta example wires those pieces together in [Program.cs](../examples/DoFiesta/DoFiesta.Presentation.Web.Server/Program.cs) and [CoreDbContext.cs](../examples/DoFiesta/DoFiesta.Infrastructure/Modules/Core/EntityFramework/CoreDbContext.cs).

### 1. Persist logs

The query feature only works if your logging pipeline writes log events into a durable store. Serilog needs to be configured with the [MSSQL](https://github.com/serilog-mssql/serilog-sinks-mssqlserver) sink in appsettings.json.

```json
"Serilog": {
    "WriteTo": [
      {
        "Name": "MSSqlServer",
        "Args": {
          "connectionString": "Server=localhost,14333;Database=db;User Id=sa;Password=pw",
          "sinkOptionsSection": {
            "tableName": "__Logging_LogEntries",
            "schemaName": "core",
            "autoCreateSqlTable": false,
            "batchPostingLimit": 1000,
            "batchPeriod": "00:00:15"
          },
          "columnOptionsSection": {
            "disableTriggers": true,
            "clusteredColumnstoreIndex": false,
            "primaryKeyColumnName": "Id",
            "addStandardColumns": [
              {
                "ColumnName": "Id",
                "DataType": "bigint"
              },
              "Message",
              "MessageTemplate",
              "Level",
              "TimeStamp",
              "Exception",
              "LogEvent",
              "TraceId",
              "SpanId"
            ],
            "removeStandardColumns": [ "Properties" ],
            "timeStamp": {
              "columnName": "TimeStamp",
              "DataType": "datetimeoffset",
              "convertToUtc": true
            },
            "additionalColumns": [
              {
                "ColumnName": "CorrelationId",
                "PropertyName": "CorrelationId",
                "DataType": "nvarchar",
                "DataLength": 128,
                "AllowNull": true
              },
              {
                "ColumnName": "LogKey",
                "PropertyName": "LogKey",
                "DataType": "nvarchar",
                "DataLength": 128,
                "AllowNull": true
              },
              {
                "ColumnName": "ModuleName",
                "PropertyName": "ModuleName",
                "DataType": "nvarchar",
                "DataLength": 128,
                "AllowNull": true
              },
              {
                "ColumnName": "ThreadId",
                "PropertyName": "ThreadId",
                "DataType": "nvarchar",
                "DataLength": 128,
                "AllowNull": true
              },
              {
                "ColumnName": "ShortTypeName",
                "PropertyName": "ShortTypeName",
                "DataType": "nvarchar",
                "DataLength": 128,
                "AllowNull": true
              }
            ]
          }
        }
      }
    ]
  },
```

That sink writes into the `core.__Logging_LogEntries` table and includes the extra columns the devkit query model expects, such as:

- `CorrelationId`
- `LogKey`
- `ModuleName`
- `ThreadId`
- `ShortTypeName`
- `TraceId`
- `SpanId`

The host itself enables the configured logging pipeline through:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureLogging();
builder.Host.ConfigureAppConfiguration();
```

### 2. Expose `LogEntries` in the DbContext

Your EF Core context must implement `ILoggingContext` and expose a `DbSet<LogEntry>`.

```csharp
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class CoreDbContext(DbContextOptions<CoreDbContext> options) :
    ModuleDbContextBase(options),
    ILoggingContext
{
    public DbSet<LogEntry> LogEntries { get; set; }
}
```

This is what allows `LogEntryService<TContext>` and `LogEntryMaintenanceService<TContext>` to query and maintain persisted log rows.

### 3. Register the application and maintenance services

DoFiesta registers the query service, maintenance queue, and hosted maintenance worker directly in the web host:

```csharp
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Presentation.Web;

builder.Services.AddScoped<ILogEntryService, LogEntryService<CoreDbContext>>();
builder.Services.AddSingleton<LogEntryMaintenanceQueue>();

if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration())
{
    builder.Services.AddHostedService<LogEntryMaintenanceService<CoreDbContext>>();
}

builder.Services.AddEndpoints<LogEntryEndpoints>(builder.Environment.IsDevelopment());
```

What each registration does:

- `ILogEntryService`: exposes the query, streaming, export, statistics, and cleanup API
- `LogEntryMaintenanceQueue`: collects cleanup/archive requests
- `LogEntryMaintenanceService<TContext>`: processes queued maintenance work and periodic retention tasks in the background
- `LogEntryEndpoints`: exposes the operational HTTP surface

### 4. Ensure the log table exists

Because the query service reads from persisted rows, your database schema must include the logging table used by the sink. The schema is expected to be managed explicitly instead of being created ad hoc by Serilog. That means it should have a migration in your infrastructure project that creates the logging table with the expected shape. The sink's `autoCreateSqlTable` option should be set to `false` to avoid conflicts.

In practice that means:

- your database must exist before you expect log queries to work
- the logging table shape must match the sink configuration
- the same database should be reachable by both the logging sink and `CoreDbContext`

### Minimal host example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureLogging();
builder.Host.ConfigureAppConfiguration();

builder.Services.AddScoped<ILogEntryService, LogEntryService<AppDbContext>>();
builder.Services.AddSingleton<LogEntryMaintenanceQueue>();
builder.Services.AddHostedService<LogEntryMaintenanceService<AppDbContext>>();
builder.Services.AddEndpoints<LogEntryEndpoints>(builder.Environment.IsDevelopment());
```

And the corresponding DbContext contract:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) :
    DbContext(options),
    ILoggingContext
{
    public DbSet<LogEntry> LogEntries { get; set; }
}
```

## Query model

`LogEntryQueryRequest` supports operational filters instead of hard-coding one reporting view.

Important filters include:

- `StartTime` and `EndTime`
- `Age`
- `Level`
- `TraceId`
- `SpanId`
- `CorrelationId`
- `LogKey`
- `ModuleName`
- `ShortTypeName`
- `SearchText`
- `PageSize`
- `ContinuationToken`
- `AfterId`

Important rules:

- `StartTime` and `Age` are mutually exclusive
- `AfterId` and `ContinuationToken` are mutually exclusive
- `PageSize` must be positive
- `SearchText` is validated to reject control characters

The service returns a `LogEntryQueryResponse` with:

- `Items`
- `ContinuationToken`
- `PageSize`

That makes the API suitable for dashboards, admin APIs, and support tooling without forcing offset-based paging.

## Typical usage

### Querying

```csharp
public sealed class OperationsService(ILogEntryService logs)
{
    public Task<LogEntryQueryResponse> GetRecentErrorsAsync(CancellationToken cancellationToken)
    {
        return logs.QueryAsync(new LogEntryQueryRequest
        {
            Age = TimeSpan.FromDays(1),
            Level = LogLevel.Error,
            PageSize = 200
        }, cancellationToken);
    }
}
```

### Streaming

```csharp
await foreach (var entry in logs.StreamAsync(
    startTime: DateTimeOffset.UtcNow.AddMinutes(-5),
    level: LogLevel.Warning,
    pollingInterval: TimeSpan.FromSeconds(2),
    cancellationToken: cancellationToken))
{
    Console.WriteLine($"{entry.TimeStamp:u} {entry.Level} {entry.Message}");
}
```

### Exporting

```csharp
await using var stream = await logs.ExportAsync(
    new LogEntryQueryRequest
    {
        Age = TimeSpan.FromDays(7),
        ModuleName = "Sales"
    },
    LogEntryExportFormat.Csv,
    cancellationToken);
```

### Statistics

```csharp
var stats = await logs.GetStatisticsAsync(
    startTime: DateTimeOffset.UtcNow.AddDays(-1),
    endTime: DateTimeOffset.UtcNow,
    groupByInterval: TimeSpan.FromHours(1),
    cancellationToken: cancellationToken);
```

## HTTP endpoints

`Presentation.Web` exposes this feature through `LogEntryEndpoints`.

By default the endpoint group is:

`/_bdk/api/logentries`

The built-in routes cover:

- `GET /_bdk/api/logentries`
- `GET /_bdk/api/logentries/stream`
- `GET /_bdk/api/logentries/stats`
- `GET /_bdk/api/logentries/export`
- `DELETE /_bdk/api/logentries`

The default endpoint options require authorization, which makes these endpoints suitable for internal admin and support surfaces rather than public APIs.

## Data shape

Each `LogEntryModel` exposes operational metadata that is useful when diagnosing distributed flows:

- message and message template
- level and timestamp
- exception text
- trace and span identifiers
- correlation identifier
- log key
- module name
- thread id
- short type name
- structured log event properties

That makes the feature especially useful when combined with module scoping and distributed tracing.

## Architecture details

```mermaid
flowchart LR
    App[Application code] --> Contract[ILogEntryService]
    Contract --> Infra[Infrastructure implementation]
    Infra --> Store[(Persisted log store)]
    Web[Presentation.Web endpoints] --> Contract
```

The important boundary is that `Application.Utilities` owns the contract, not the persistence strategy. This lets the same query and export model work with different infrastructure implementations while keeping consumers stable.

## Practical notes

- Query paging is continuation-token based, not page-number based.
- For queries, `Age` is subtracted from the start of the current UTC day; use an explicit `StartTime` for a rolling window such as the previous hour.
- `StreamAsync` polls in batches ordered by descending ID and then advances toward lower IDs. It does not tail entries with higher IDs that arrive after the initial batch.
- Cleanup is a maintenance operation, not a query concern.
- Archival marks matching active rows. Deletion removes only rows that are already archived.
- Export format is intentionally narrow and operational: `Csv`, `Json`, or `Txt`.

## Best practices

- Use `ContinuationToken` instead of trying to emulate offset paging.
- Prefer `Age` for operational dashboards and `StartTime`/`EndTime` for reporting screens.
- Filter by `TraceId` or `CorrelationId` when you need one end-to-end request or workflow.
- Filter by `ModuleName` and `LogKey` when you need module-level operational slices.
- Keep the HTTP endpoints behind authorization and treat them as operational tooling.
- Let infrastructure own retention and archival strategy; use `CleanupAsync(...)` as the application-facing maintenance entry point.

## Related docs

- [Presentation Endpoints](./features-presentation-endpoints.md)
- [Common Observability Tracing](./common-observability-tracing.md)
- [Modules](./features-modules.md)
