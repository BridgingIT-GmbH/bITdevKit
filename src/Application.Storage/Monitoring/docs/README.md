# File Monitoring System - Overview

> The File Monitoring System provides comprehensive file change detection and processing capabilities across various storage types. It combines real-time monitoring with on-demand scanning to ensure no changes are missed, while maintaining efficient resource usage and clear operational boundaries. The system is designed for reliability, extensibility, and clear operational visibility through a flexible behavior system and native .NET monitoring capabilities.

[TOC]

## Core Capabilities

### Change Detection
The system employs two complementary approaches to file change detection, configurable per location:

- Real-time watchers provide immediate notification of changes through filesystem events, requiring no active polling or state comparison. This can be disabled using the UseOnDemandOnly option to restrict a location to on-demand scanning only.
- On-demand scanning capability systematically compares filesystem state with previously recorded events, ensuring comprehensive change detection even after system downtime.

Change detection can use either timestamp-based comparison (default) or checksum-based verification, configurable per location. This flexibility allows for balancing performance against accuracy requirements for different monitoring scenarios.

### Event Processing and Behaviors
All detected changes flow through a common processing pipeline, ensuring consistent handling regardless of their source. The system uses an in-memory queue with rate-limited processing to maintain stable operation under varying loads. Events are processed sequentially through configurable processor chains, with optional retry policies available per processor.

The behavior system allows for extensible monitoring capabilities:
- Plug-in architecture for monitoring operations
- Multiple behaviors can observe scan operations
- Rich context sharing between behaviors
- Built-in support for logging, metrics, and custom analytics

### Operational Features
The system integrates with standard .NET monitoring capabilities, providing:
- Built-in behaviors for logging and statistics
- Metrics through System.Diagnostics.Metrics
- Health checks through the standard health check system
- Rich event publication for external system integration

## Configuration

### Basic Configuration
Setting up file monitoring with default behaviors:

```csharp
builder.Services.AddFileMonitoring(monitoring =>
{
    monitoring
        .WithBehavior<LoggingBehavior>()
        .UseShare("Documents", "\\\\server\\docs", options =>
        {
            // Basic settings
            options.FilePattern = "*.pdf";
            options.IncludeSubdirectories = true;
            options.UseOnDemandOnly(); // Disables real-time watching
            // Configure simple processor
            options.UseProcessor<FileReadProcessor>()
                .WithRetry(retry => retry
                    .MaxRetries(3)
                    .DelayMs(1000));
        });
});
```

### Advanced Configuration
Example with multiple behaviors and advanced options:

```csharp
builder.Services.AddFileMonitoring(monitoring =>
{
    monitoring
        .WithBehavior<LoggingBehavior>()
        .WithBehavior<ScanStatisticsBehavior>()
        .UseShare("Archives", "\\\\server\\archive", options =>
        {
            // Basic configuration
            options.FilePattern = "*.*";
            options.IncludeSubdirectories = true;
            options.UseOnDemandOnly(); // Disables real-time watching
            // Use checksum-based detection
            options.Strategy = ChangeDetectionStrategy.ChecksumBased;

            // Configure rate limits
            options.RateLimit
                .WithEventsPerSecond(100)
                .WithMaxBurstSize(1000);

            // Configure blacklist
            options.Blacklist.IgnoreSystemFiles = true;
            options.Blacklist.IgnoreHiddenFiles = true;
            options.Blacklist.ExcludePatterns.Add("*.tmp");

            // Configure processor chain
            options.UseProcessor<FileReadProcessor>()
                .WithRetry(retry => retry
                    .MaxRetries(3)
                    .DelayMs(1000));
        });
});
```

The UseOnDemandOnly option can be added to disable real-time watchers for a location, restricting it to on-demand scanning only. By default, real-time watching is enabled unless explicitly disabled.

## Using the On-Demand Scanner

The scanner can be accessed directly through the MonitoringService:

```csharp
public class Example
{
    private readonly MonitoringService monitoringService;
    private readonly ILogger<Example> logger;

    // Simple scan with results
    public async Task<ScanContext> SimpleScanAsync()
    {
        try
        {
            var context = await monitoringService.ScanLocationAsync("Documents", CancellationToken.None);

            logger.LogInformation("Scan completed. Found {TotalChanges} changes across {ProcessedItems} items",
                context.DetectedChanges,
                context.ProcessedItems);

            return context
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during scan operation");
            throw;
        }
    }

    // Scan with timeout and results
    public async Task<ScanContext> TimedScanAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            var context = await monitoringService.ScanLocationAsync("Documents", cts.Token);

            return context
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Scan operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during scan operation");
            throw;
        }
    }
}
```

## Behaviors

The behavior system provides extensible monitoring capabilities through clear interfaces and rich context sharing.

### Built-in Behaviors

#### LoggingBehavior
Provides structured logging for scan operations:
- Correlation ID tracking across scan operations
- Detailed progress logging
- Error and state tracking
- Performance metrics logging

#### ScanStatisticsBehavior
Collects and analyzes scan performance metrics:
- Real-time processing rates
- Change type distribution
- Timing analysis with percentiles
- Error rate tracking
- Comprehensive completion reports

### Custom Behaviors
Implement the IMonitoringBehavior interface to create custom behaviors:

```csharp
public interface IMonitoringBehavior
{
    void OnScanStarted(ScanContext context);
    void OnFileDetected(ScanContext context, FileEvent fileEvent);
    void OnScanCompleted(ScanContext context, TimeSpan duration);
}
```

### Scan Context
Each scan operation provides rich contextual information to behaviors:
- Unique scan identifier
- Progress tracking
- State management
- Error collection
- Performance metrics
- Extensible properties for behavior communication

## Use Cases

The File Monitoring System can be applied in various scenarios where reliable and efficient file change detection and processing are critical:

1. Backup and Sync Solutions
   - Detect and synchronize changes across storage locations
   - Ensure backups are up-to-date through real-time monitoring
   - Verify file integrity using checksum-based detection

2. Content Management Systems
   - Monitor content directories for changes
   - Trigger content processing workflows
   - Maintain content repository consistency

3. Data Processing Pipelines
   - Detect new data files for processing
   - Ensure data consistency through verification
   - Trigger automated processing workflows

4. Security and Compliance
   - Monitor sensitive directories for changes
   - Track file access and modifications
   - Maintain audit trails through behavior system

## Operational Considerations

### Resource Usage
The system is designed for efficient resource usage:
- In-memory queue with rate limiting
- Streaming-based file operations
- No event batching or accumulation
- Clear resource cleanup

### Error Handling
Comprehensive error handling ensures reliable operation:
- Clear error boundaries
- Optional retry policies
- Full error logging
- Status preservation
- Event correlation

### Monitoring
Integration with standard monitoring:
- Native .NET metrics
- Standard health checks
- Rich event publication
- Status tracking through behaviors
- Detailed operation logging

### Performance
Performance considerations are built into the design:
- Rate-limited processing
- Efficient change detection
- Streaming operations
- Resource control
- Performance tracking through behaviors

Through these capabilities and considerations, the system provides robust file monitoring while maintaining clear operational characteristics and integration points.