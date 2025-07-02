﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages file change detection and event processing for a specific storage location.
/// Integrates real-time watching, on-demand scanning, and a processor chain with rate limiting.
/// </summary>
[DebuggerDisplay("Name={options.Name}")]
public abstract class LocationHandlerBase : ILocationHandler
{
    protected readonly ILogger logger;
    private readonly TypedLogger loggerTyped;
    protected readonly IFileStorageProvider provider;
    protected readonly IFileEventStore store;
    protected readonly LocationOptions options;
    protected readonly IServiceProvider serviceProvider;
    protected readonly IEnumerable<IMonitoringBehavior> behaviors;
    protected readonly BlockingCollection<FileEvent> eventQueue;
    protected readonly List<IFileEventProcessor> processors;
    protected readonly RateLimiter rateLimiter;

    protected CancellationTokenSource cts;
    protected Task processingTask;
    protected bool isPaused;

    /// <summary>
    /// Initializes a new instance of the LocationHandler.
    /// </summary>
    /// <param name="logger">The logger instance for logging handler operations.</param>
    /// <param name="provider">The file storage provider for accessing the location.</param>
    /// <param name="store">The event store for persisting FileEvents.</param>
    /// <param name="options">The configuration options for the location.</param>
    /// <param name="serviceProvider">The service provider for resolving processor and behavior instances via Factory.</param>
    /// <param name="behaviors">The global monitoring behaviors for scan observability.</param>
    protected LocationHandlerBase(
        ILogger logger,
        IFileStorageProvider provider,
        IFileEventStore store,
        LocationOptions options,
        IServiceProvider serviceProvider,
        IEnumerable<IMonitoringBehavior> behaviors = null)
    {
        this.logger = logger ?? NullLoggerFactory.Instance.CreateLogger("LocationHandler");
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

        this.StartAsync(); // autostart
    }

    /// <summary>
    /// Gets the configuration options for this location.
    /// </summary>
    public LocationOptions Options => this.options;

    /// <summary>
    /// Gets the file storage provider associated with the current instance. It returns an object implementing the
    /// IFileStorageProvider interface.
    /// </summary>
    public IFileStorageProvider Provider => this.provider;

    /// <summary>
    /// Starts monitoring the location asynchronously.
    /// Initiates real-time watching (if supported) and event processing.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    public virtual Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (this.processingTask == null)
        {
            //this.loggerTyped.LogInformationStartingHandler(this.options.LocationName, this.provider.Description);
            this.cts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.cts.Token);

            this.processingTask = Task.Run(() => this.ProcessEventsAsync(linkedCts.Token), linkedCts.Token);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops monitoring the location asynchronously.
    /// Halts real-time watching and event processing, cleaning up resources.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public virtual Task StopAsync(CancellationToken cancellationToken = default)
    {
        this.loggerTyped.LogInformationStoppingHandler(this.options.LocationName);
        //this.cts.Cancel();

        if (this.processingTask != null)
        {
            //await this.processingTask;
        }

        this.eventQueue.CompleteAdding();
        this.processingTask = null;
        this.logger.LogInformation($"Handler stopped for location: {this.options.LocationName}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs an on-demand scan of the location asynchronously.
    /// Detects changes by comparing current state with stored events and enqueues them.
    /// </summary>
    /// <param name="options">Specifies the configuration settings for the scan operation.</param>
    /// <param name="progress">Provides updates on the progress of the scan as it executes.</param>
    /// <param name="cancellationToken">Allows for the cancellation of the scan operation if needed.</param>
    /// <returns>Returns a task that, when completed, provides the context of the scan.</returns>
    public abstract Task<FileScanContext> ScanAsync(FileScanOptions options = null, IProgress<FileScanProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses event processing for the location asynchronously.
    /// Temporarily halts the processing pipeline without stopping real-time watching.
    /// </summary>
    /// <returns>A task representing the asynchronous pause operation.</returns>
    public virtual Task PauseAsync(CancellationToken cancellationToken = default)
    {
        this.loggerTyped.LogInformationPausingHandler(this.options.LocationName);
        this.isPaused = true;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resumes event processing for the location asynchronously.
    /// Restarts the processing pipeline if previously paused.
    /// </summary>
    /// <returns>A task representing the asynchronous resume operation.</returns>
    public virtual Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        this.loggerTyped.LogInformationResumingHandler(this.options.LocationName);
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
            LocationName = this.options.LocationName,
            IsActive = this.processingTask?.IsCompleted == false,
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
    public Task<bool> IsQueueEmptyAsync() =>
        Task.FromResult(this.eventQueue.Count == 0);

    /// <summary>
    /// Waits until the event queue is empty or a timeout occurs asynchronously.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>A task that completes when the queue is empty or the timeout is reached.</returns>
    public async Task WaitForQueueEmptyAsync(TimeSpan timeout)
    {
        var startTime = DateTimeOffset.UtcNow;
        while (!this.eventQueue.IsCompleted && this.eventQueue.Count > 0)
        {
            if (DateTimeOffset.UtcNow - startTime >= timeout && timeout != TimeSpan.Zero)
            {
                throw new TimeoutException($"Queue did not empty within {timeout.TotalSeconds} seconds.");
            }
            await Task.Delay(100); // Poll every 100ms
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
            this.loggerTyped.LogWarningProcessorNotFound(this.options.LocationName, processorName);
            return Task.CompletedTask;
        }

        processor.IsEnabled = true;
        this.loggerTyped.LogInformationProcessorEnabled(this.options.LocationName, processorName);

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
            this.loggerTyped.LogWarningProcessorNotFound(this.options.LocationName, processorName);
            return Task.CompletedTask;
        }

        processor.IsEnabled = false;
        this.loggerTyped.LogInformationProcessorDisabled(this.options.LocationName, processorName);

        return Task.CompletedTask;
    }

    public IEnumerable<IFileEventProcessor> GetProcessors()
    {
        return this.processors.AsEnumerable();
    }

    private List<IFileEventProcessor> BuildProcessorChain(LocationOptions options)
    {
        return this.BuildProcessorChainWithConfig(options);
    }

    protected List<IFileEventProcessor> BuildProcessorChainWithConfig(LocationOptions options)
    {
        var chain = new List<IFileEventProcessor>();
        foreach (var processorConfig in options.ProcessorConfigs)
        {
            var processor = Factory.Create(processorConfig.ProcessorType, this.serviceProvider) as IFileEventProcessor;
            if (processor != null)
            {
                processor.IsEnabled = true;
                processorConfig.Configure?.Invoke(processor);
                foreach (var behaviorType in options.LocationProcessorBehaviors.Concat(processorConfig.BehaviorTypes))
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

    protected async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !this.eventQueue.IsCompleted)
        {
            if (this.isPaused || !this.eventQueue.TryTake(out var fileEvent, 100, cancellationToken))
            {
                continue;
            }

            await this.rateLimiter.WaitForTokenAsync(cancellationToken);
            var context = new FileProcessingContext(fileEvent);
            context.SetItem("StorageProvider", this.provider);

            // Store the event once, before processing
            await this.store.StoreEventAsync(fileEvent, cancellationToken);

            // Process with all enabled processors
            foreach (var processor in this.processors.Where(p => p.IsEnabled))
            {
                var result = await this.ExecuteProcessorAsync(processor, context, cancellationToken);
                await this.store.StoreProcessingResultAsync(new FileProcessingResult
                {
                    FileEventId = fileEvent.Id,
                    ProcessorName = processor.ProcessorName,
                    Success = result.IsSuccess,
                    Message = result.IsSuccess ? "Processed successfully" : result.Errors.FirstOrDefault()?.Message
                }, cancellationToken);
            }

            if (!this.processors.Any(p => p.IsEnabled))
            {
                this.logger.LogDebug($"{{LogKey}} filemonitoring: No enabled processors for event: {fileEvent.FilePath} in location: {this.options.LocationName}", Constants.LogKey);
            }
        }
    }

    /// <summary>
    /// Determines whether a file should be processed based on specified options and its file path.
    /// </summary>
    /// <param name="options">Contains criteria for filtering files, including path filters and blacklist patterns.</param>
    /// <param name="filePath">Represents the path of the file being evaluated for processing.</param>
    /// <returns>Returns true if the file meets the criteria for processing; otherwise, false.</returns>
    protected bool ShouldProcessFile(FileScanOptions options, string filePath)
    {
        // Skip files that don't match the optional path filter (scan options)
        if (!string.IsNullOrEmpty(options.FileFilter) && !filePath.MatchAny([options.FileFilter]))
        {
            return false;
        }

        // Skip files that match the blacklist patterns (scan options)
        if (options.FileBlackListFilter.SafeAny() && filePath.MatchAny(options.FileBlackListFilter))
        {
            return false;
        }

        // Skip files that match the blacklist patterns (location options)
        if (this.options.FileBlackListFilter.SafeAny() && filePath.MatchAny(this.options.FileBlackListFilter))
        {
            return false;
        }

        return true;
    }

    private async Task<Result> ExecuteProcessorAsync(IFileEventProcessor processor, FileProcessingContext context, CancellationToken token)
    {
        try
        {
            await processor.ProcessAsync(context, token);

            return Result.Success();
        }
        catch (Exception ex)
        {
            this.loggerTyped.LogErrorProcessingFailed(this.options.LocationName, processor.ProcessorName, ex.Message);
            return Result.Failure().WithError(new ExceptionError(ex));
        }
    }

    // Behavior decorator to wrap processors with behaviors
    private class BehaviorDecorator(IFileEventProcessor inner, IProcessorBehavior behavior) : IFileEventProcessor
    {
        public string ProcessorName => inner.ProcessorName;

        public bool IsEnabled { get => inner.IsEnabled; set => inner.IsEnabled = value; }

        public IEnumerable<IProcessorBehavior> Behaviors => inner.Behaviors.Concat([behavior]);

        public async Task ProcessAsync(FileProcessingContext context, CancellationToken token)
        {
            await behavior.BeforeProcessAsync(context, token);
            var result = await this.ExecuteInnerAsync(context, token);
            await behavior.AfterProcessAsync(context, result, token);
        }

        private async Task<Result<bool>> ExecuteInnerAsync(FileProcessingContext context, CancellationToken token)
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
        public void LogInformationStartingHandler(string storageProviderLocationName, string storageProviderDescription) =>
            logger.LogInformation("{LogKey} filemonitoring: starting location handler (locationName={StorageProviderLocationName}, description={StorageProviderDescription})", Constants.LogKey, storageProviderLocationName, storageProviderDescription);

        public void LogInformationRealTimeStarted(string storageProviderLocationName) =>
            logger.LogInformation("{LogKey} filemonitoring: real-time watching started (locationName={StorageProviderLocationName})", Constants.LogKey, storageProviderLocationName);

        public void LogInformationStoppingHandler(string storageProviderLocationName) =>
            logger.LogInformation("{LogKey} filemonitoring: stopping location handler (locationName={StorageProviderLocationName})", Constants.LogKey, storageProviderLocationName);

        public void LogInformationHandlerStopped(string storageProviderLocationName) =>
            logger.LogInformation("{LogKey} filemonitoring: location handler stopped (locationName={StorageProviderLocationName})", Constants.LogKey, storageProviderLocationName);

        public void LogInformationScanningLocation(string storageProviderLocationName) =>
            logger.LogInformation("{LogKey} filemonitoring: scanning location (locationName={StorageProviderLocationName})", Constants.LogKey, storageProviderLocationName);

        public void LogInformationScanCompleted(string storageProviderLocationName, int changeCount) =>
            logger.LogInformation("{LogKey} filemonitoring: scan completed (locationName={StorageProviderLocationName}, Changes={ChangeCount})",
                Constants.LogKey, storageProviderLocationName, changeCount);

        public void LogInformationPausingHandler(string storageProviderLocationName) =>
            logger.LogInformation("{LogKey} filemonitoring: pausing handler (locationName={StorageProviderLocationName})", Constants.LogKey, storageProviderLocationName);

        public void LogInformationResumingHandler(string storageProviderLocationName) =>
            logger.LogInformation("{LogKey} filemonitoring: resuming handler (locationName={StorageProviderLocationName})", Constants.LogKey, storageProviderLocationName);

        public void LogWarningProcessorNotFound(string storageProviderLocationName, string fileMonitoringProcessorName) =>
            logger.LogWarning("{LogKey} filemonitoring: processor not found (locationName={StorageProviderLocationName}, processor={FileMonitoringProcessorName})",
                Constants.LogKey, storageProviderLocationName, fileMonitoringProcessorName);

        public void LogInformationProcessorEnabled(string storageProviderLocationName, string fileMonitoringProcessorName) =>
            logger.LogInformation("{LogKey} filemonitoring: processor enabled (locationName={StorageProviderLocationName}, processor={FileMonitoringProcessorName})",
                Constants.LogKey, storageProviderLocationName, fileMonitoringProcessorName);

        public void LogInformationProcessorDisabled(string storageProviderLocationName, string fileMonitoringProcessorName) =>
            logger.LogInformation("{LogKey} filemonitoring: processor disabled (locationName={StorageProviderLocationName}, processor={FileMonitoringProcessorName})",
                Constants.LogKey, storageProviderLocationName, fileMonitoringProcessorName);

        public void LogErrorProcessingFailed(string storageProviderLocationName, string fileMonitoringProcessorName, string errorMessage) =>
            logger.LogError("{LogKey} filemonitoring: processing failed (locationName={StorageProviderLocationName}, processor={FileMonitoringProcessorName}, Error={ErrorMessage})",
                Constants.LogKey, storageProviderLocationName, fileMonitoringProcessorName, errorMessage);
    }
}
