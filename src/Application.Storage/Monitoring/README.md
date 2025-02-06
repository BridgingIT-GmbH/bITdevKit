# File Monitoring System - Overview

> The File Monitoring System provides comprehensive file change detection and processing capabilities across various storage types. It combines real-time monitoring with on-demand scanning to ensure no changes are missed, while maintaining efficient resource usage and clear operational boundaries. The system is designed for reliability, extensibility, and clear operational visibility through native .NET monitoring capabilities.

[TOC]

## Core Capabilities

### Change Detection
The system employs two complementary approaches to file change detection. Real-time watchers provide immediate notification of changes through filesystem events, requiring no active polling or state comparison. This is complemented by on-demand scanning capability that systematically compares filesystem state with previously recorded events, ensuring comprehensive change detection even after system downtime.

Change detection can use either timestamp-based comparison (default) or checksum-based verification, configurable per location. This flexibility allows for balancing performance against accuracy requirements for different monitoring scenarios.

### Event Processing
All detected changes flow through a common processing pipeline, ensuring consistent handling regardless of their source. The system uses an in-memory queue with rate-limited processing to maintain stable operation under varying loads. Events are processed sequentially through configurable processor chains, with optional retry policies available per processor.

The processing pipeline maintains clear event correlation throughout the flow, with comprehensive status tracking and error handling. Each processor can enhance the event with additional information, which is persisted with the event for later reference.

### Operational Features
The system integrates with standard .NET monitoring capabilities, providing metrics through System.Diagnostics.Metrics and health checks through the standard health check system. This enables seamless integration with existing monitoring infrastructure while maintaining clear boundaries around core functionality.

Rich event publication enables external systems to react to various system occurrences, from file detection through processing completion. The monitoring service provides comprehensive control and insight into system operation through clear interfaces.

### Use Cases
The File Monitoring System can be applied in various scenarios where reliable and efficient file change detection and processing are critical. Here are some potential use cases:
1.	Backup and Sync Solutions:
•	Automatically detect and synchronize changes in files across different storage locations, such as local folders, network shares, and cloud storage.
•	Ensure that backups are up-to-date by detecting and processing file changes in real-time or through scheduled scans.
2.	Content Management Systems (CMS):
•	Monitor content directories for changes, such as new uploads, modifications, or deletions, and trigger appropriate actions like indexing, metadata extraction, or content publishing.
•	Maintain an up-to-date repository of content by detecting changes and processing them through a defined pipeline.
3.	Data Ingestion Pipelines:
•	Detect new data files in specified directories and trigger data ingestion processes to load the data into databases or data warehouses.
•	Ensure data consistency and completeness by using checksum-based change detection to verify file integrity.
4.	Security and Compliance Monitoring:
•	Monitor sensitive directories for unauthorized changes or access, and trigger alerts or automated responses to potential security breaches.
•	Maintain audit logs of file changes and processing results for compliance and regulatory requirements.
6.	Media Processing and Transcoding:
•	Detect new media files (e.g., videos, images) and trigger processing pipelines for tasks like transcoding, thumbnail generation, or metadata extraction.
•	Handle large volumes of media files efficiently with rate-limited processing and retry policies.
7.	Log File Monitoring and Analysis:
•	Monitor log directories for new or updated log files and trigger log analysis or aggregation processes.
•	Ensure that log data is processed and available for monitoring and alerting systems in real-time.
8.	IoT Data Collection:
•	Monitor directories where IoT devices store data files and trigger data processing pipelines to analyze and store the data.
•	Ensure timely processing of IoT data to support real-time analytics and decision-making.
9.	Document Management Systems:
•	Monitor document repositories for changes and trigger workflows for document approval, versioning, or archiving.
•	Ensure that document changes are tracked and processed according to business rules.
10.	E-commerce Platforms:
•	Monitor product image directories for changes and trigger image processing tasks like resizing, watermarking, or optimization.
•	Ensure that product images are always up-to-date and optimized for web delivery.

These use cases demonstrate the versatility and applicability of the File Monitoring System V2 in various domains, providing reliable file change detection and processing capabilities to support a wide range of business and technical requirements.

## Usage Examples

### Basic Configuration
Setting up file monitoring with default settings:

```csharp
builder.Services.AddFileMonitoring(monitoring =>
{
    monitoring.UseShare("Documents", "\\\\server\\docs", options =>
    {
        // Basic settings
        options.FilePattern = "*.pdf";
        options.IncludeSubdirectories = true;
        
        // Configure simple processor
        options.UseProcessor<FileReadProcessor>()
            .WithRetry(retry => retry
                .MaxRetries(3)
                .DelayMs(1000));
    });
});
```

### Advanced Configuration
Comprehensive monitoring setup with custom processing:

```csharp
builder.Services.AddFileMonitoring(monitoring =>
{
    monitoring.UseShare("Archives", "\\\\server\\archive", options =>
    {
        // Basic configuration
        options.FilePattern = "*.*";
        options.IncludeSubdirectories = true;
        
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
                .DelayMs(1000)
                .ExponentialBackoff());
                
        options.UseProcessor<MetadataProcessor>()
            .WithRetry(retry => retry
                .MaxRetries(2)
                .DelayMs(500));
                
        options.UseProcessor<FileHashProcessor>()
            .WithRetry(retry => retry
                .MaxRetries(3)
                .DelayMs(1000));
    });
});
```

### Operational Control
Example of operational monitoring and control:

```csharp
public class FileMonitoringController : ControllerBase
{
    private readonly MonitoringService _monitoringService;
    private readonly ILogger<FileMonitoringController> _logger;
    
    // Constructor injection
    public FileMonitoringController(
        MonitoringService monitoringService,
        ILogger<FileMonitoringController> logger)
    {
        _monitoringService = monitoringService;
        _logger = logger;
    }
    
    // Monitor file events
    public void ConfigureEventHandlers()
    {
        _monitoringService.FileEventReceived += (s, e) =>
        {
            _logger.LogInformation(
                "File event received for {Path}: {Type}", 
                e.FilePath, 
                e.EventType);
        };
        
        _monitoringService.BeforeProcessing += (s, e) =>
        {
            _logger.LogInformation(
                "Starting processing for {Path}", 
                e.FileEvent.FilePath);
        };
        
        _monitoringService.ProcessingError += (s, e) =>
        {
            _logger.LogError(
                e.Exception,
                "Error processing {Path}", 
                e.FileEvent.FilePath);
        };
    }
    
    // Trigger manual scan
    public async Task<IActionResult> ScanLocation(string locationName)
    {
        await foreach(var evt in _monitoringService.ScanLocationAsync(
            locationName, 
            CancellationToken.None))
        {
            _logger.LogInformation(
                "Scan detected {Type} for {Path}",
                evt.EventType,
                evt.FilePath);
        }
        return Ok();
    }
    
    // Location control
    public async Task<IActionResult> PauseLocation(string locationName)
    {
        await _monitoringService.PauseLocationAsync(locationName);
        return Ok();
    }
    
    public async Task<IActionResult> GetLocationStatus(string locationName)
    {
        var status = await _monitoringService.GetLocationStatusAsync(locationName);
        return Ok(status);
    }
}
```

### Health Check Configuration
Integrating with .NET health checks:

```csharp
public class FileMonitoringHealthCheck : IHealthCheck
{
    private readonly MonitoringService _monitoringService;
    
    public FileMonitoringHealthCheck(MonitoringService monitoringService)
    {
        _monitoringService = monitoringService;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        var isHealthy = await _monitoringService.IsHealthyAsync();
        
        if (!isHealthy)
        {
            return HealthCheckResult.Unhealthy(
                "File monitoring system is not healthy");
        }
        
        return HealthCheckResult.Healthy(
            "File monitoring system is healthy");
    }
}

// Registration
builder.Services.AddHealthChecks()
    .AddCheck<FileMonitoringHealthCheck>("file_monitoring");
```

### Metrics Configuration
Example of metrics setup:

```csharp
public class FileMonitoringMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _eventsProcessed;
    private readonly Counter<long> _processingErrors;
    private readonly ObservableGauge<int> _queueSize;
    
    public FileMonitoringMetrics(MonitoringService monitoringService)
    {
        _meter = new Meter("FileMonitoring");
        
        _eventsProcessed = _meter.CreateCounter<long>(
            "file_monitoring.events.processed",
            description: "Number of file events processed");
            
        _processingErrors = _meter.CreateCounter<long>(
            "file_monitoring.processing.errors",
            description: "Number of processing errors");
            
        _queueSize = _meter.CreateObservableGauge<int>(
            "file_monitoring.queue.size",
            () => monitoringService.GetQueueSize("default"));
            
        // Configure event handlers
        monitoringService.AfterProcessing += (s, e) => 
            _eventsProcessed.Add(1);
            
        monitoringService.ProcessingError += (s, e) => 
            _processingErrors.Add(1);
    }
}
```

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
- Status tracking
- Queue inspection

### Performance
Performance considerations are built into the design:
- Rate-limited processing
- Efficient change detection
- Pagination for large directories
- Streaming operations
- Resource control

Through these capabilities and considerations, the system provides robust file monitoring while maintaining clear operational characteristics and integration points.