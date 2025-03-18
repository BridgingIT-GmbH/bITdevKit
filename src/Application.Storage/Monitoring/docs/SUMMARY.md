# File Monitoring System - Design and Usage Summary

> The File Monitoring System is a robust, extensible solution built in .NET to detect and process file changes across various storage locations. It combines real-time monitoring with on-demand scanning, ensures consistent event handling through a rate-limited pipeline, and provides rich observability via behaviors and .NET monitoring tools. Locations can be configured for real-time detection (default) or restricted to on-demand scanning only using the `UseOnDemandOnly` option. This summary explains the system’s design and demonstrates its usage with practical C# examples.

[TOC]

## Overview
The File Monitoring System acts as a vigilant overseer for file systems, tracking changes (e.g., creations, modifications, deletions) in specified locations like local folders or network shares. It’s designed for reliability, efficiency, and flexibility, making it suitable for scenarios like backups, content management, or security auditing. Built with modern .NET features, it integrates seamlessly with standard metrics and health checks while offering extensible behaviors for custom needs.

### Key Features
- **Dual Change Detection**: Real-time watchers (optional) and on-demand scanners ensure comprehensive coverage.
- **Processing Pipeline**: Sequential, rate-limited event handling with configurable processors.
- **Extensible Behaviors**: Plug-ins for logging, metrics, and custom actions.
- **Operational Integration**: Native .NET monitoring, health checks, and event publication.

## Core Concepts

### 1. Change Detection
The system employs two complementary methods to detect file changes, configurable per location:
- **Real-Time Watchers**: Leverage .NET’s `FileSystemWatcher` for instant notifications. Enabled by default, but can be disabled with `UseOnDemandOnly`. Ideal for immediate reactions but may miss events during downtime.
- **On-Demand Scanners**: Actively compare current file states with historical records. Ensures nothing is overlooked, even after restarts, and is the sole method for locations with `UseOnDemandOnly`.

Detection strategies per location:
- **Timestamp-Based** (default): Compares modification times (fast).
- **Checksum-Based**: Verifies file contents via hashes (accurate).

### 2. Event Processing
Changes are funneled through a unified pipeline:
- **In-Memory Queue**: Stores events temporarily, avoiding disk I/O.
- **Rate Limiting**: Controls processing speed (e.g., 100 events/second) for stability.
- **Processor Chains**: Customizable handlers (e.g., read, move, log) with optional retries.

### 3. Behaviors
Behaviors are plug-ins that observe and react to monitoring activities:
- **Built-In**: Logging (tracks events), ScanStatistics (collects metrics).
- **Custom**: Implement `IMonitoringBehavior` for tailored functionality.

### 4. Monitoring and Control
- **Metrics**: Uses `System.Diagnostics.Metrics` for performance tracking.
- **Health Checks**: Integrates with .NET’s health check system.
- **Events**: Publishes notifications for external integration (real-time events only for locations without `UseOnDemandOnly`).
- **Control**: Pause, resume, or restart locations at runtime; configure on-demand-only mode during setup.

## Architecture
The system is modular, with clear boundaries:
- **Monitoring Service**: Central coordinator managing locations and processing, respecting `UseOnDemandOnly` settings.
- **Storage Providers**: Abstract file system interactions (e.g., local, network).
- **Change Detection**: Watchers (optional per location) and scanners feed events to the pipeline.
- **Processing Pipeline**: Queues and processes events sequentially.
- **Behaviors**: Provide observability and extensibility.
- **Storage**: Persists event history (not metrics).

### Simplified Flow
1. **Detection**: Watcher catches a change (if enabled) or scanner finds a difference.
2. **Queueing**: Event is enqueued with blacklist filtering (e.g., skip `.tmp` files).
3. **Processing**: Processors handle the event (e.g., log it, read it).
4. **Observation**: Behaviors log, track, or react to the event.

## Usage Examples in C#

Below are practical C# examples demonstrating how to configure and use the File Monitoring System in a .NET application, including the new `UseOnDemandOnly` option.

### 1. Basic Configuration
Set up monitoring for a network share with logging:

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add file monitoring service
        builder.Services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .WithBehavior<LoggingBehavior>() // Add logging
                .UseShare("Documents", "\\\\server\\docs", options =>
                {
                    options.FilePattern = "*.pdf"; // Monitor PDFs only
                    options.IncludeSubdirectories = true;

                    // Add a simple processor with retries
                    options.UseProcessor<FileReadProcessor>()
                        .WithRetry(retry => retry
                            .MaxRetries(3)
                            .DelayMs(1000));
                });
        });

        var app = builder.Build();
        app.Run();
    }
}

// Example processor to read file contents
public class FileReadProcessor : IFileEventProcessor
{
    public string ProcessorName => "FileReader";

    public Task<(bool CanHandle, string ReasonIfNot)> CanHandleAsync(ProcessingContext context, CancellationToken token)
    {
        return Task.FromResult((true, string.Empty));
    }

    public async Task<(bool Success, string Message)> ProcessAsync(ProcessingContext context, CancellationToken token)
    {
        try
        {
            var storageProvider = context.GetItem<IFileStorageProvider>("StorageProvider");
            using var stream = await storageProvider.ReadFileAsync(context.FileEvent.FilePath, token);
            // Process stream (e.g., read contents)
            return (true, "File read successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to read file: {ex.Message}");
        }
    }
}
```

**Explanation**: This monitors `\\server\docs` for PDF changes in real-time (default), logs events via `LoggingBehavior`, and uses `FileReadProcessor` to read each changed file, retrying up to 3 times if it fails.

### 2. Advanced Configuration with On-Demand Only
Monitor an archive with multiple behaviors, checksum detection, and on-demand scanning only:

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddFileMonitoring(monitoring =>
        {
            monitoring
                .WithBehavior<LoggingBehavior>()
                .WithBehavior<ScanStatisticsBehavior>() // Tracks scan stats
                .UseShare("Archives", "\\\\server\\archive", options =>
                {
                    options.FilePattern = "*.*";
                    options.IncludeSubdirectories = true;
                    options.UseOnDemandOnly(); // Disable real-time watching
                    options.Strategy = ChangeDetectionStrategy.ChecksumBased; // Verify contents
                    options.RateLimit.WithEventsPerSecond(100).WithMaxBurstSize(1000);
                    options.Blacklist.ExcludePatterns.Add("*.tmp"); // Skip temp files

                    options.UseProcessor<FileArchiveProcessor>()
                        .WithRetry(retry => retry.MaxRetries(2).DelayMs(500));
                });
        });

        var app = builder.Build();
        app.Run();
    }
}

// Example processor to archive files
public class FileArchiveProcessor : IFileEventProcessor
{
    public string ProcessorName => "FileArchiver";

    public Task<(bool CanHandle, string ReasonIfNot)> CanHandleAsync(ProcessingContext context, CancellationToken token)
    {
        return Task.FromResult((true, string.Empty));
    }

    public async Task<(bool Success, string Message)> ProcessAsync(ProcessingContext context, CancellationToken token)
    {
        try
        {
            // Simulate archiving logic (e.g., copy to backup)
            Console.WriteLine($"Archiving {context.FileEvent.FilePath}");
            return (true, "File archived");
        }
        catch (Exception ex)
        {
            return (false, $"Archiving failed: {ex.Message}");
        }
    }
}
```

**Explanation**: This configures `\\server\archive` for all files, disables real-time watching with `UseOnDemandOnly`, uses checksums for accuracy, limits processing to 100 events/second, skips `.tmp` files, and archives changes with stats tracking. Changes are detected only via manual scans.

### 3. On-Demand Scanning
Manually trigger a scan and log results:

```csharp
public class ScanExample
{
    private readonly MonitoringService _monitoringService;
    private readonly ILogger<ScanExample> _logger;

    public ScanExample(MonitoringService monitoringService, ILogger<ScanExample> logger)
    {
        _monitoringService = monitoringService;
        _logger = logger;
    }

    public async Task RunScanAsync()
    {
        try
        {
            // Trigger scan on "Archives" location (on-demand only)
            var context = await _monitoringService.ScanLocationAsync("Archives", CancellationToken.None);

            _logger.LogInformation(
                "Scan completed. Found {TotalChanges} changes in {ProcessedItems} items",
                context.DetectedChanges,
                context.ProcessedItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
        }
    }
}
```

**Explanation**: This uses dependency injection to access `MonitoringService` and runs a scan on the "Archives" location (configured as on-demand only), logging the number of changes and items processed.

### 4. Custom Behavior
Create a behavior to send alerts:

```csharp
public class AlertBehavior : IMonitoringBehavior
{
    private readonly ILogger<AlertBehavior> _logger;

    public AlertBehavior(ILogger<AlertBehavior> logger)
    {
        _logger = logger;
    }

    public void OnScanStarted(ScanContext context)
    {
        _logger.LogInformation("Scan started for {Location}", context.LocationName);
    }

    public void OnFileDetected(ScanContext context, FileEvent fileEvent)
    {
        // Send alert (e.g., via email or API)
        _logger.LogWarning("Change detected: {Path} ({EventType})",
            fileEvent.FilePath, fileEvent.EventType);
    }

    public void OnScanCompleted(ScanContext context, TimeSpan duration)
    {
        _logger.LogInformation("Scan completed in {Duration}s", duration.TotalSeconds);
    }
}

// Register in Program.cs
builder.Services.AddFileMonitoring(monitoring =>
{
    monitoring
        .WithBehavior<AlertBehavior>()
        .UseShare("Sensitive", "\\\\server\\secure", options =>
        {
            options.FilePattern = "*.docx";
        });
});
```

**Explanation**: `AlertBehavior` logs warnings for each change, useful for security monitoring. It’s added alongside other behaviors, with real-time watching enabled by default for "Sensitive".

### 5. Runtime Control
Pause and resume a location:

```csharp
public class ControlExample
{
    private readonly MonitoringService _monitoringService;

    public ControlExample(MonitoringService monitoringService)
    {
        _monitoringService = monitoringService;
    }

    public async Task ManageLocationAsync()
    {
        // Pause monitoring
        await _monitoringService.PauseLocationAsync("Documents");
        Console.WriteLine("Documents paused");

        // Check status
        var status = await _monitoringService.GetLocationStatusAsync("Documents");
        Console.WriteLine($"Status: {status.State}");

        // Resume monitoring
        await _monitoringService.ResumeLocationAsync("Documents");
        Console.WriteLine("Documents resumed");
    }
}
```

**Explanation**: This demonstrates runtime control—pausing and resuming the “Documents” location (real-time enabled), with status checks. For on-demand-only locations, pausing isn’t typically needed since no watcher is active.

## Design Highlights
- **Reliability**: Dual detection ensures no missed changes; retries handle failures; `UseOnDemandOnly` provides explicit control.
- **Efficiency**: Rate limiting and streaming keep resource use in check.
- **Extensibility**: Behaviors and processors allow customization.
- **Observability**: Integrates with .NET metrics and health checks.

## Use Cases
- **Backup**: Sync new files to a backup drive (real-time or on-demand).
- **Content Management**: Update a CMS when media files change.
- **Data Pipeline**: Process new data files as they appear (on-demand for batch processing).
- **Security**: Log changes in sensitive directories (real-time or audited scans).

## Implementation Notes
- **Configuration**: Fluent API (`UseShare`, `WithBehavior`) simplifies setup; `UseOnDemandOnly` disables real-time watching.
- **Processors**: Implement `IFileEventProcessor` for custom handling.
- **Behaviors**: Use `IMonitoringBehavior` for observability.
- **Boundaries**: Sequential processing, in-memory queues, and event-only storage ensure predictability.

## Conclusion
The File Monitoring System is a versatile .NET tool for file change detection and processing. With its dual detection methods—configurable via `UseOnDemandOnly` for on-demand-only workflows—extensible architecture, and practical C# usage, it offers a reliable foundation for various applications. The examples above show how to configure it for real-time or on-demand scanning, extend it with behaviors, and control it at runtime—making it a powerful ally for file-centric workflows.