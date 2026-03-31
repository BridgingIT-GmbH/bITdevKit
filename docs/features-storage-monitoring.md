# Storage Monitoring Feature Documentation

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

## Core Concepts

### Locations

A monitored location combines:

- a location name
- a concrete `IFileStorageProvider`
- `LocationOptions`
- one or more file-event processors

The built-in builder supports:

- `UseLocal(...)`
- `UseInMemory(...)`

Infrastructure packages can add further provider-backed locations.

### Service

`IFileMonitoringService` is the orchestration entry point. It can:

- start and stop all configured monitoring
- scan a specific location
- pause and resume locations
- restart locations
- inspect queue size and status
- enable and disable processors at runtime

### Events And Processors

Detected changes become `FileEvent` instances. These events are then passed through configured `IFileEventProcessor` implementations.

Built-in examples include:

- `FileLoggerProcessor`
- `FileMoverProcessor`

Processors can also be decorated with `IProcessorBehavior` implementations such as logging or retry behaviors.

## Basic Setup

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

## Location Options

`LocationOptions` controls per-location behavior.

Key settings include:

- `FileFilter`
- `FileBlackListFilter`
- `UseOnDemandOnly`
- `ScanOnStart`
- `RateLimit`

`UseOnDemandOnly` is especially important because it disables real-time watching and turns the location into a scan-only source.

## On-Demand Scans

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

## Runtime Control

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

That API makes the feature suitable for admin screens and automated maintenance workflows, not just startup-time wiring.

## Processor Pipeline

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

## FileMonitoring And FileStorage

Storage Monitoring is not a separate storage system. It is built on top of the existing file-storage abstraction:

- the location handler owns an `IFileStorageProvider`
- scans and watchers discover changes in that provider-backed location
- processors often use the same storage provider to move, delete, or inspect files

That relationship is why file monitoring belongs in `Application.Storage` rather than in scheduling or messaging.

## Scheduled Scans

When scans must run on a schedule instead of continuously, `Application.Storage.Jobs` provides `FileMonitoringLocationScanJob`.

This job:

- reads location-specific settings from job data
- creates `FileScanOptions`
- calls `IFileMonitoringService.ScanLocationAsync(...)`
- logs progress and scan results

That makes it the bridge between `Storage Monitoring` and `JobScheduling`.

## Best Practices

- Use real-time watching for low-latency inbox scenarios.
- Use on-demand scans when deterministic batch reconciliation matters more than immediate detection.
- Keep processors focused on one responsibility each.
- Use processor behaviors for retries and logging instead of duplicating that logic in every processor.
- Tune `RateLimitOptions` deliberately for high-volume locations.
- Use scheduled scans for partner drop folders, archive sweeps, and recovery passes after downtime.
- Prefer persistent event-store infrastructure when monitoring history matters across restarts.

## Related Docs

- [FileStorage](./features-storage-files.md)
- [JobScheduling](./features-jobscheduling.md)
- [DocumentStorage](./features-storage-documents.md)
