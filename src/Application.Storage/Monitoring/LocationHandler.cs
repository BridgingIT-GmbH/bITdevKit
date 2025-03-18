// File: BridgingIT.DevKit.Application.FileMonitoring/LocationHandler.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages file change detection and event processing for a specific storage location.
/// Integrates real-time watching, on-demand scanning, and a processor chain with rate limiting.
/// </summary>
[DebuggerDisplay("Name={options.Name}")]
public class LocationHandler
{
    private readonly ILogger<LocationHandler> logger;
    private readonly TypedLogger loggerTyped;
    private readonly IFileStorageProvider provider;
    private readonly IFileEventStore store;
    private readonly LocationOptions options;
    private readonly IServiceProvider serviceProvider; // For Factory use
    private readonly IEnumerable<IMonitoringBehavior> behaviors;
    private readonly List<IFileEventProcessor> processors;
    private readonly RateLimiter rateLimiter;
    private readonly BlockingCollection<FileEvent> eventQueue;
    private CancellationTokenSource cts;
    private Task processingTask;
    private bool isPaused;
    private FileSystemWatcher fileSystemWatcher;

    /// <summary>
    /// Initializes a new instance of the LocationHandler.
    /// </summary>
    /// <param name="logger">The logger instance for logging handler operations.</param>
    /// <param name="provider">The file storage provider for accessing the location.</param>
    /// <param name="store">The event store for persisting FileEvents.</param>
    /// <param name="options">The configuration options for the location.</param>
    /// <param name="serviceProvider">The service provider for resolving processor and behavior instances via Factory.</param>
    /// <param name="behaviors">The global monitoring behaviors for scan observability.</param>
    public LocationHandler(
        ILogger<LocationHandler> logger,
        IFileStorageProvider provider,
        IFileEventStore store,
        LocationOptions options,
        IServiceProvider serviceProvider,
        IEnumerable<IMonitoringBehavior> behaviors = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.loggerTyped = new TypedLogger(logger);
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.behaviors = behaviors ?? [];
        this.processors = this.BuildProcessorChain(options);
        this.rateLimiter = new RateLimiter(options.RateLimit.EventsPerSecond, options.RateLimit.MaxBurstSize);
        this.eventQueue = [];
        this.cts = new CancellationTokenSource();
        this.isPaused = false;
    }

    /// <summary>
    /// Gets the configuration options for this location.
    /// </summary>
    public LocationOptions Options => this.options;

    /// <summary>
    /// Starts monitoring the location asynchronously.
    /// Initiates real-time watching (if supported) and event processing.
    /// </summary>
    /// <param name="token">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken token)
    {
        this.loggerTyped.LogInformationStartingHandler(this.options.Name);
        if (!this.options.UseOnDemandOnly && this.provider.SupportsNotifications)
        {
            this.SetupFileSystemWatcher();
            this.loggerTyped.LogInformationRealTimeStarted(this.options.Name);
        }

        this.cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, this.cts.Token);
        this.processingTask = Task.Run(() => this.ProcessEventsAsync(linkedCts.Token), linkedCts.Token);

        if (!this.options.UseOnDemandOnly)
        {
            await this.ScanAsync(token); // Initial scan to sync state
        }
    }

    /// <summary>
    /// Stops monitoring the location asynchronously.
    /// Halts real-time watching and event processing, cleaning up resources.
    /// </summary>
    /// <param name="token">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken token)
    {
        this.loggerTyped.LogInformationStoppingHandler(this.options.Name);
        this.cts.Cancel();

        if (this.fileSystemWatcher != null)
        {
            this.fileSystemWatcher.EnableRaisingEvents = false;
            this.fileSystemWatcher.Dispose();
            this.fileSystemWatcher = null;
        }

        if (this.processingTask != null)
        {
            await this.processingTask;
        }

        this.eventQueue.CompleteAdding();
        this.loggerTyped.LogInformationHandlerStopped(this.options.Name);
    }

    /// <summary>
    /// Performs an on-demand scan of the location asynchronously.
    /// Detects changes by comparing current state with stored events and enqueues them.
    /// </summary>
    /// <param name="token">The cancellation token to stop the scan if needed.</param>
    /// <returns>A ScanContext object with the scan results.</returns>
    public async Task<ScanContext> ScanAsync(CancellationToken token)
    {
        var context = new ScanContext { LocationName = this.options.Name };
        this.loggerTyped.LogInformationScanningLocation(this.options.Name);
        this.behaviors.ForEach(b => b.OnScanStarted(context), cancellationToken: token);

        var presentFiles = await this.store.GetPresentFilesAsync(this.options.Name);
        var currentFiles = new Dictionary<string, FileMetadata>();
        string continuationToken = null;

        do
        {
            var result = await this.provider.ListFilesAsync("/", this.options.FilePattern, true, continuationToken, token);
            foreach (var filePath in result.Value.Files)
            {
                var metadataResult = await this.provider.GetFileMetadataAsync(filePath, token);
                var checksumResult = await this.provider.GetChecksumAsync(filePath, token);
                if (metadataResult.IsSuccess && checksumResult.IsSuccess)
                {
                    var metadata = metadataResult.Value;
                    currentFiles[filePath] = metadata;
                    var lastEvent = await this.store.GetFileEventAsync(filePath);
                    var eventType = this.DetermineEventType(lastEvent, metadata, checksumResult.Value);

                    if (eventType.HasValue)
                    {
                        var fileEvent = new FileEvent
                        {
                            LocationName = this.options.Name,
                            FilePath = filePath,
                            EventType = eventType.Value,
                            FileSize = metadata.Length,
                            LastModified = metadata.LastModified,
                            Checksum = checksumResult.Value
                        };
                        this.eventQueue.Add(fileEvent, token);
                        context.DetectedChanges.Add(fileEvent);
                        this.behaviors.ForEach(b => b.OnFileDetected(context, fileEvent), cancellationToken: token);
                    }
                }
            }
            continuationToken = result.Value.NextContinuationToken;
        } while (continuationToken != null && !token.IsCancellationRequested);

        // Detect deletions
        foreach (var missingFile in presentFiles.Except(currentFiles.Keys))
        {
            var fileEvent = new FileEvent
            {
                LocationName = this.options.Name,
                FilePath = missingFile,
                EventType = FileEventType.Deleted
            };
            this.eventQueue.Add(fileEvent, token);
            context.DetectedChanges.Add(fileEvent);
            this.behaviors.ForEach(b => b.OnFileDetected(context, fileEvent), cancellationToken: token);
        }

        context.EndTime = DateTimeOffset.UtcNow;
        this.behaviors.ForEach(b => b.OnScanCompleted(context, context.EndTime.Value - context.StartTime), cancellationToken: token);
        this.loggerTyped.LogInformationScanCompleted(this.options.Name, context.DetectedChanges.Count);
        return context;
    }

    /// <summary>
    /// Pauses event processing for the location asynchronously.
    /// Temporarily halts the processing pipeline without stopping real-time watching.
    /// </summary>
    /// <returns>A task representing the asynchronous pause operation.</returns>
    public Task PauseAsync()
    {
        this.loggerTyped.LogInformationPausingHandler(this.options.Name);
        this.isPaused = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resumes event processing for the location asynchronously.
    /// Restarts the processing pipeline if previously paused.
    /// </summary>
    /// <returns>A task representing the asynchronous resume operation.</returns>
    public Task ResumeAsync()
    {
        this.loggerTyped.LogInformationResumingHandler(this.options.Name);
        this.isPaused = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves the current status of the location asynchronously.
    /// Includes active state, pause state, and queue size.
    /// </summary>
    /// <returns>A LocationStatus object with the current state.</returns>
    public Task<LocationStatus> GetStatusAsync()
    {
        var status = new LocationStatus
        {
            LocationName = this.options.Name,
            IsActive = this.processingTask != null && !this.processingTask.IsCompleted,
            IsPaused = this.isPaused,
            QueueSize = this.eventQueue.Count,
            LastScanTime = DateTimeOffset.MinValue // Update with actual scan time if tracked
        };
        return Task.FromResult(status);
    }

    /// <summary>
    /// Gets the current size of the event queue.
    /// </summary>
    /// <returns>The number of pending events.</returns>
    public int GetQueueSize() => this.eventQueue.Count;

    /// <summary>
    /// Checks if the event queue is empty asynchronously.
    /// </summary>
    /// <returns>True if the queue is empty; false otherwise.</returns>
    public Task<bool> IsQueueEmptyAsync() => Task.FromResult(this.eventQueue.Count == 0);

    /// <summary>
    /// Waits until the event queue is empty or a timeout occurs asynchronously.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>A task that completes when the queue is empty or the timeout is reached.</returns>
    public async Task WaitForQueueEmptyAsync(TimeSpan timeout)
    {
        var start = DateTimeOffset.UtcNow;
        while (this.eventQueue.Count > 0 && DateTimeOffset.UtcNow - start < timeout)
        {
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Retrieves the names of active processors asynchronously.
    /// </summary>
    /// <returns>A list of enabled processor names.</returns>
    public Task<IEnumerable<string>> GetActiveProcessorsAsync() =>
        Task.FromResult(this.processors.Where(p => p.IsEnabled).Select(p => p.ProcessorName));

    /// <summary>
    /// Enables a specific processor in the chain asynchronously.
    /// </summary>
    /// <param name="processorName">The name of the processor to enable.</param>
    /// <returns>A task representing the asynchronous enable operation.</returns>
    public Task EnableProcessorAsync(string processorName)
    {
        var processor = this.processors.FirstOrDefault(p => p.ProcessorName == processorName);
        if (processor == null)
        {
            this.loggerTyped.LogWarningProcessorNotFound(this.options.Name, processorName);
            return Task.CompletedTask;
        }
        processor.IsEnabled = true;
        this.loggerTyped.LogInformationProcessorEnabled(this.options.Name, processorName);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disables a specific processor in the chain asynchronously.
    /// </summary>
    /// <param name="processorName">The name of the processor to disable.</param>
    /// <returns>A task representing the asynchronous disable operation.</returns>
    public Task DisableProcessorAsync(string processorName)
    {
        var processor = this.processors.FirstOrDefault(p => p.ProcessorName == processorName);
        if (processor == null)
        {
            this.loggerTyped.LogWarningProcessorNotFound(this.options.Name, processorName);
            return Task.CompletedTask;
        }
        processor.IsEnabled = false;
        this.loggerTyped.LogInformationProcessorDisabled(this.options.Name, processorName);
        return Task.CompletedTask;
    }

    private void SetupFileSystemWatcher()
    {
        if (this.provider is LocalFileStorageProvider localProvider)
        {
            this.fileSystemWatcher = new FileSystemWatcher
            {
                Path = localProvider.RootPath,
                Filter = this.options.FilePattern,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = true
            };

            this.fileSystemWatcher.Created += async (s, e) => await this.OnFileCreated(e);
            this.fileSystemWatcher.Changed += async (s, e) => await this.OnFileChanged(e);
            this.fileSystemWatcher.Deleted += async (s, e) => await this.OnFileDeleted(e);
            this.fileSystemWatcher.EnableRaisingEvents = true;
        }
    }

    private async Task OnFileCreated(FileSystemEventArgs e)
    {
        if (this.provider is LocalFileStorageProvider localProvider)
        {
            var relativePath = Path.GetRelativePath(localProvider.RootPath, e.FullPath);
            var metadata = await this.provider.GetFileMetadataAsync(relativePath, CancellationToken.None);
            var checksum = await this.provider.GetChecksumAsync(relativePath, CancellationToken.None);
            var fileEvent = new FileEvent
            {
                LocationName = this.options.Name,
                FilePath = relativePath,
                EventType = FileEventType.Added,
                FileSize = metadata.Value.Length,
                LastModified = metadata.Value.LastModified,
                Checksum = checksum.Value,
                DetectionTime = DateTimeOffset.UtcNow
            };
            this.eventQueue.Add(fileEvent);
        }
    }

    private async Task OnFileChanged(FileSystemEventArgs e)
    {
        if (this.provider is LocalFileStorageProvider localProvider)
        {
            var relativePath = Path.GetRelativePath(localProvider.RootPath, e.FullPath);
            var metadata = await this.provider.GetFileMetadataAsync(relativePath, CancellationToken.None);
            var checksum = await this.provider.GetChecksumAsync(relativePath, CancellationToken.None);
            var fileEvent = new FileEvent
            {
                LocationName = this.options.Name,
                FilePath = relativePath,
                EventType = FileEventType.Changed,
                FileSize = metadata.Value.Length,
                LastModified = metadata.Value.LastModified,
                Checksum = checksum.Value,
                DetectionTime = DateTimeOffset.UtcNow
            };
            this.eventQueue.Add(fileEvent);
        }
    }

    private Task OnFileDeleted(FileSystemEventArgs e)
    {
        if (this.provider is LocalFileStorageProvider localProvider)
        {
            var relativePath = Path.GetRelativePath(localProvider.RootPath, e.FullPath);
            var fileEvent = new FileEvent
            {
                LocationName = this.options.Name,
                FilePath = relativePath,
                EventType = FileEventType.Deleted,
                DetectionTime = DateTimeOffset.UtcNow
            };
            this.eventQueue.Add(fileEvent);
        }

        return Task.CompletedTask;
    }

    private List<IFileEventProcessor> BuildProcessorChain(LocationOptions options)
    {
        return this.BuildProcessorChainWithConfig(options);
    }

    internal List<IFileEventProcessor> BuildProcessorChainWithConfig(LocationOptions options)
    {
        var chain = new List<IFileEventProcessor>();
        foreach (var processorConfig in options.ProcessorConfigs)
        {
            var processor = Factory.Create(processorConfig.ProcessorType, this.serviceProvider) as IFileEventProcessor;
            if (processor != null)
            {
                processor.IsEnabled = true;
                processorConfig.Configure?.Invoke(processor); // Apply configuration

                foreach (var behaviorType in options.LocationProcessorBehaviors)
                {
                    if (Factory.Create(behaviorType, this.serviceProvider) is IProcessorBehavior behavior)
                    {
                        processor = new BehaviorDecorator(processor, behavior);
                    }
                }
                foreach (var behaviorType in processorConfig.BehaviorTypes)
                {
                    if (Factory.Create(behaviorType, this.serviceProvider) is IProcessorBehavior behavior)
                    {
                        processor = new BehaviorDecorator(processor, behavior);
                    }
                }
                chain.Add(processor);
            }
        }
        return chain;
    }

    private async Task ProcessEventsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !this.eventQueue.IsCompleted)
        {
            if (this.isPaused || this.cts.IsCancellationRequested || !this.eventQueue.TryTake(out var fileEvent, 100, token))
            {
                continue;
            }

            await this.rateLimiter.WaitForTokenAsync(token);
            var context = new ProcessingContext { FileEvent = fileEvent };
            context.SetItem("StorageProvider", this.provider); // Add provider to context

            foreach (var processor in this.processors.Where(p => p.IsEnabled))
            {
                var result = await this.ExecuteProcessorAsync(processor, context, token);
                await this.store.StoreEventAsync(fileEvent);
                await this.store.StoreProcessingResultAsync(new ProcessingResult
                {
                    FileEventId = fileEvent.Id,
                    ProcessorName = processor.ProcessorName,
                    Success = result.IsSuccess,
                    Message = result.IsSuccess ? "Processed successfully" : result.Errors.FirstOrDefault()?.Message
                });
            }
        }
    }

    private async Task<Result<bool>> ExecuteProcessorAsync(IFileEventProcessor processor, ProcessingContext context, CancellationToken token)
    {
        try
        {
            await processor.ProcessAsync(context, token);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            this.loggerTyped.LogErrorProcessingFailed(this.options.Name, processor.ProcessorName, ex.Message);
            return Result<bool>.Failure().WithError(new ExceptionError(ex));
        }
    }

    private FileEventType? DetermineEventType(FileEvent lastEvent, FileMetadata current, string checksum)
    {
        if (lastEvent == null) return FileEventType.Added;
        if (lastEvent.EventType == FileEventType.Deleted) return FileEventType.Added;
        if (lastEvent.Checksum != checksum || lastEvent.LastModified != current.LastModified) return FileEventType.Changed;

        return null;
    }

    // Behavior decorator to wrap processors with behaviors
    private class BehaviorDecorator(IFileEventProcessor inner, IProcessorBehavior behavior) : IFileEventProcessor
    {
        public string ProcessorName => inner.ProcessorName;

        public bool IsEnabled { get => inner.IsEnabled; set => inner.IsEnabled = value; }

        public IEnumerable<IProcessorBehavior> Behaviors => inner.Behaviors.Concat([behavior]);

        public async Task ProcessAsync(ProcessingContext context, CancellationToken token)
        {
            await behavior.BeforeProcessAsync(context, token);
            var result = await this.ExecuteInnerAsync(context, token);
            await behavior.AfterProcessAsync(context, result, token);
        }

        private async Task<Result<bool>> ExecuteInnerAsync(ProcessingContext context, CancellationToken token)
        {
            try
            {
                await inner.ProcessAsync(context, token);
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure().WithError(new ExceptionError(ex));
            }
        }
    }

    private class TypedLogger(ILogger logger)
    {
        public void LogInformationStartingHandler(string locationName) =>
            logger.LogInformation("{LogKey} filemonitoring: starting handler (LocationName={LocationName})", Constants.LogKey, locationName);

        public void LogInformationRealTimeStarted(string locationName) =>
            logger.LogInformation("{LogKey} filemonitoring: real-time watching started (LocationName={LocationName})", Constants.LogKey, locationName);

        public void LogInformationStoppingHandler(string locationName) =>
            logger.LogInformation("{LogKey} filemonitoring: stopping handler (LocationName={LocationName})", Constants.LogKey, locationName);

        public void LogInformationHandlerStopped(string locationName) =>
            logger.LogInformation("{LogKey} filemonitoring: handler stopped (LocationName={LocationName})", Constants.LogKey, locationName);

        public void LogInformationScanningLocation(string locationName) =>
            logger.LogInformation("{LogKey} filemonitoring: scanning location (LocationName={LocationName})", Constants.LogKey, locationName);

        public void LogInformationScanCompleted(string locationName, int changeCount) =>
            logger.LogInformation("{LogKey} filemonitoring: scan completed (LocationName={LocationName}, Changes={ChangeCount})",
                Constants.LogKey, locationName, changeCount);

        public void LogInformationPausingHandler(string locationName) =>
            logger.LogInformation("{LogKey} filemonitoring: pausing handler (LocationName={LocationName})", Constants.LogKey, locationName);

        public void LogInformationResumingHandler(string locationName) =>
            logger.LogInformation("{LogKey} filemonitoring: resuming handler (LocationName={LocationName})", Constants.LogKey, locationName);

        public void LogWarningProcessorNotFound(string locationName, string processorName) =>
            logger.LogWarning("{LogKey} filemonitoring: processor not found (LocationName={LocationName}, ProcessorName={ProcessorName})",
                Constants.LogKey, locationName, processorName);

        public void LogInformationProcessorEnabled(string locationName, string processorName) =>
            logger.LogInformation("{LogKey} filemonitoring: processor enabled (LocationName={LocationName}, ProcessorName={ProcessorName})",
                Constants.LogKey, locationName, processorName);

        public void LogInformationProcessorDisabled(string locationName, string processorName) =>
            logger.LogInformation("{LogKey} filemonitoring: processor disabled (LocationName={LocationName}, ProcessorName={ProcessorName})",
                Constants.LogKey, locationName, processorName);

        public void LogErrorProcessingFailed(string locationName, string processorName, string errorMessage) =>
            logger.LogError("{LogKey} filemonitoring: processing failed (LocationName={LocationName}, ProcessorName={ProcessorName}, Error={ErrorMessage})",
                Constants.LogKey, locationName, processorName, errorMessage);
    }
}