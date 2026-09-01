
# Storage Monitoring

> Detect file changes and process storage events through configurable monitoring pipelines.

[TOC]

## Overview

Storage Monitoring builds on the file-storage abstraction to detect file changes, scan locations on demand, and process resulting file events through configurable processor pipelines. It is the part of `Application.Storage` that turns passive file access into operational workflows such as inbox processing, archive movement, logging, or import triggering.

The feature combines:

- monitored locations backed by file-storage providers
- real-time watching where supported
- on-demand scanning for deterministic reconciliation
- a processor pipeline for reacting to detected file events
- status and runtime control through `IFileMonitoringService`

## Challenges

Storage providers can expose current files without explaining what changed since the previous inspection. Applications must reconcile snapshots, process bursts without overloading downstream work, and support both notification-capable file systems and scan-only stores.

## Solution

Storage Monitoring associates a named `IFileStorageProvider` with location options, an event store, and a processor pipeline. `IFileMonitoringService` coordinates real-time watchers where available and performs explicit scans for every supported provider.

## Key Features

- local, in-memory, and named provider-backed locations
- real-time watching where the provider supports notifications
- deterministic on-demand scans with filters, batching, and progress
- per-location processor pipelines with logging and retry behaviors
- runtime pause, resume, restart, status, and processor control
- scheduled scans and optional REST operations

## Architecture

A location handler reads a storage provider and compares observed files with stored file events. It emits `FileEvent` instances into the location's processor pipeline. `IFileMonitoringService` owns the configured handlers and exposes their scan and runtime-control operations to application code, jobs, and HTTP endpoints.

## Use Cases

- process files arriving in an inbound directory
- reconcile a database-backed or cloud-backed location on a schedule
- move, log, or import files after a detected change
- recover missed events after downtime
- expose monitoring status and manual scans to operational tooling

## Basic Usage

This example registers a singleton in-memory file provider, monitors it on demand, writes a sample file, checks the storage result, and returns the events detected by the scan.

```csharp
using System.Text;
using BridgingIT.DevKit.Application.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFileStorage(factory => factory
    .RegisterProvider("inbox", storage => storage
        .UseInMemory("Inbox")
        .WithLifetime(ServiceLifetime.Singleton)));

builder.Services.AddFileMonitoring(monitoring =>
{
    monitoring.UseProvider("inbox", "inbox", options =>
    {
        options.UseOnDemandOnly = true;
        options.FileFilter = "*.csv";
        options.UseProcessor<FileLoggerProcessor>();
    });
});

var app = builder.Build();

app.MapPost("/monitoring/scan", async (
    IFileStorageProviderFactory storageFactory,
    IFileMonitoringService monitoring,
    CancellationToken cancellationToken) =>
{
    var storage = storageFactory.CreateProvider("inbox");
    await using var content = new MemoryStream(
        Encoding.UTF8.GetBytes("orderId,total\n1001,42.50"),
        writable: false);

    var write = await storage.WriteFileAsync(
        "orders.csv",
        content,
        cancellationToken: cancellationToken);

    if (write.IsFailure)
    {
        return Results.Problem(string.Join(
            "; ",
            write.Errors.Select(error => error.Message)));
    }

    var scan = await monitoring.ScanLocationAsync(
        "inbox",
        new FileScanOptions { WaitForProcessing = true },
        token: cancellationToken);

    return Results.Ok(new
    {
        EventCount = scan.Events.Count,
        Events = scan.Events.Select(fileEvent => new
        {
            fileEvent.EventType,
            fileEvent.FilePath
        })
    });
});

app.Run();
```

The first `POST /monitoring/scan` detects `orders.csv`, runs `FileLoggerProcessor`, and returns the detected event and path. Later scans compare the current provider contents with the event history.

## Core concepts

### Locations

A monitored location combines:

- a location name
- a concrete `IFileStorageProvider`
- `LocationOptions`
- one or more file-event processors

The built-in builder supports:

- `UseLocal(...)`
- `UseInMemory(...)`
- `UseProvider(...)`

`UseProvider(...)` resolves an already-registered `IFileStorageProvider` by name through
`IFileStorageProviderFactory`. This is the preferred option when the monitored files live in
application storage that is already exposed through a named provider, such as an Entity Framework
backed operational document store.

```csharp
services.AddFileStorage(factory => factory
    .RegisterProvider("documents", storage => storage
        .UseEntityFramework<AppDbContext>(
            "Documents",
            "Operational document storage")
        .WithLifetime(ServiceLifetime.Singleton)))
    .AddEndpoints(options => options.RequireAuthorization());

services.AddFileMonitoring(monitoring =>
{
    monitoring.UseProvider("documents", "documents", options =>
    {
        options.UseOnDemandOnly = true;
        options.FileFilter = "*.*";
        options.FileBlackListFilter = ["*.tmp", "*.log"];
        options.UseProcessor<FileLoggerProcessor>();
    });
});
```

Provider-backed locations are scan-based unless the resolved provider offers notifications. This
makes them a good fit for scheduled reconciliation jobs and admin-driven reprocessing flows.

### Service

`IFileMonitoringService` is the orchestration entry point. It can:

- start and stop all configured monitoring
- scan a specific location
- pause and resume locations
- restart locations
- inspect queue size and status
- enable and disable processors at runtime

### Events and processors

Detected changes become `FileEvent` instances. These events are then passed through configured `IFileEventProcessor` implementations.

Built-in examples include:

- `FileLoggerProcessor`
- `FileMoverProcessor`

Processors can also be decorated with `IProcessorBehavior` implementations such as logging or retry behaviors.

## Detailed setup

```csharp
using BridgingIT.DevKit.Application.Storage;

services.AddFileMonitoring(monitoring =>
{
    monitoring
        .WithBehavior<LoggingBehavior>()
        .UseLocal("inbound", "C:\\data\\inbound", options =>
        {
            options.FileFilter = "*.csv";
            options.FileBlackListFilter = ["*.tmp", "*.bak"];
            options.RateLimit = RateLimitOptions.MediumSpeed;

            options.UseProcessor<FileLoggerProcessor>();
            options.UseProcessor<FileMoverProcessor>(config =>
                config.WithConfiguration(p =>
                    ((FileMoverProcessor)p).DestinationRoot = "C:\\data\\processed"));
        });
});
```

This registers the monitoring service, configures one location, and adds two processors that will react to detected file events.

## Location options

`LocationOptions` controls per-location behavior.

Key settings include:

- `FileFilter`
- `FileBlackListFilter`
- `UseOnDemandOnly`
- `ScanOnStart`
- `RateLimit`

`UseOnDemandOnly` is especially important because it disables real-time watching and turns the location into a scan-only source.

## On-demand scans

`ScanLocationAsync(...)` lets application code or scheduled jobs trigger a controlled reconciliation pass over a location.

```csharp
var result = await fileMonitoringService.ScanLocationAsync(
    "inbound",
    new FileScanOptions
    {
        WaitForProcessing = true,
        FileFilter = "*.csv",
        FileBlackListFilter = ["*.tmp"],
        BatchSize = 10,
        ProgressIntervalPercentage = 5,
        Timeout = TimeSpan.FromMinutes(2)
    },
    progress,
    cancellationToken);
```

`FileScanOptions` supports:

- `WaitForProcessing`
- `Timeout`
- `DelayPerFile`
- `EventFilter`
- `BatchSize`
- `ProgressIntervalPercentage`
- `FileFilter`
- `FileBlackListFilter`
- `SkipChecksum`
- `MaxFilesToScan`
- `ThrowIfDirectoryNotExists`

This makes on-demand scans useful for operational jobs, recovery after downtime, and deterministic partner-feed processing.

## Runtime control

The monitoring service also exposes operational control:

- `PauseLocationAsync(...)`
- `ResumeLocationAsync(...)`
- `RestartLocationAsync(...)`
- `GetLocationStatusAsync(...)`
- `GetAllLocationStatusAsync(...)`
- `GetQueueSize(...)`
- `WaitForQueueEmptyAsync(...)`
- `GetActiveProcessorsAsync(...)`
- `EnableProcessorAsync(...)`
- `DisableProcessorAsync(...)`

Admin screens and automated maintenance workflows can call that API after startup.

## Processor pipeline

The processor pipeline is where file events turn into business-adjacent work.

```mermaid
flowchart LR
    Scan[Watcher or Scan] --> Event[FileEvent]
    Event --> Handler[Location Handler]
    Handler --> Proc1[Processor 1]
    Proc1 --> Proc2[Processor 2]
    Proc2 --> Result[Processed outcome]
```

Important points:

- processors are configured per location
- processors can be enabled or disabled at runtime
- processors can have behaviors such as retry or logging
- location-level monitoring behaviors observe broader scan operations

This design keeps change detection separate from change handling.

## FileMonitoring and FileStorage

Storage Monitoring is not a separate storage system. It is built on top of the existing file-storage abstraction:

- the location handler owns an `IFileStorageProvider`
- scans and watchers discover changes in that provider-backed location
- processors often use the same storage provider to move, delete, or inspect files

That relationship is why file monitoring belongs in `Application.Storage` rather than in scheduling or messaging.

## Scheduled scans

When scans must run on a schedule instead of continuously, `Application.Storage.Jobs` provides `FileMonitoringLocationScanJob`.

This job:

- reads location-specific settings from job data
- creates `FileScanOptions`
- calls `IFileMonitoringService.ScanLocationAsync(...)`
- logs progress and scan results

That makes it the bridge between `Storage Monitoring` and the `Application.Jobs` feature.

For provider-backed locations, scheduled scans are the normal way to capture file events in
multi-node or database-backed deployments where real-time watchers are not available.

## REST endpoints for provider-backed monitoring

When a monitored location is also exposed through `Presentation.Web.Storage`, the same provider
name can be used to query file events and trigger scans over HTTP.

```csharp
services.AddFileStorage(factory => factory
    .RegisterProvider("documents", storage => storage
        .UseEntityFramework<AppDbContext>(
            "Documents",
            "Operational document storage")
        .WithLifetime(ServiceLifetime.Singleton)))
    .AddEndpoints(options => options.RequireAuthorization());

services.AddFileMonitoring(monitoring =>
{
    monitoring.UseProvider("documents", "documents", options =>
    {
        options.UseOnDemandOnly = true;
        options.UseProcessor<FileLoggerProcessor>();
    });
});
```

This exposes:

| Route | Purpose |
| --- | --- |
| `GET /_bdk/api/storage/files/{provider}/events?path=...&eventType=...&fromDate=...&tillDate=...&take=...` | Query stored file events for the provider-backed monitoring location |
| `POST /_bdk/api/storage/files/{provider}/events/scan?waitForProcessing=true&searchPattern=...&maxFilesToScan=...&skipChecksum=false` | Trigger an on-demand scan and return the detected events |

Important notes:

- The `{provider}` route segment must match the monitored location name you configured with `UseProvider(...)`.
- These HTTP routes resolve the provider through `IFileStorageProviderFactory`, so the REST surface uses the same named provider that application code uses in process.
- The scan route is useful for operations screens, admin tooling, or recovery workflows after downtime.
- The DoFiesta example consumes these routes through the generated Kiota client and exposes them in the Operations > File Events dashboard at `/operations/fileevents`.

## Best practices

- Use real-time watching for low-latency inbox scenarios.
- Use on-demand scans when deterministic batch reconciliation matters more than immediate detection.
- Keep processors focused on one responsibility each.
- Use processor behaviors for retries and logging instead of duplicating that logic in every processor.
- Tune `RateLimitOptions` deliberately for high-volume locations.
- Use scheduled scans for partner drop folders, archive sweeps, and recovery passes after downtime.
- Prefer persistent event-store infrastructure when monitoring history matters across restarts.

## Document Storage diagnostics

`IDocumentStorageDiagnosticsService` captures a payload-free snapshot of every named document client, including default selection, lifetime, provider capabilities, effective stored-size limits, health, transform identifiers, expiration/retention support, and the most recent retention result when available. Diagnostics never include document values, raw continuation tokens, encryption keys, or transform secrets.

When the MCP runtime is present, `DocumentStorageMcpHandler` adds `documents.summary`, `documents.clients`, and `documents.probe`. These operations use the same normalized client identities as dependency injection, health checks, retention, cache keys, continuation-token binding, and the dashboard.

## Related docs

- [FileStorage](./features-storage-files.md)
- [JobScheduling](./features-jobscheduling.md)
- [DocumentStorage](./features-storage-documents.md)
