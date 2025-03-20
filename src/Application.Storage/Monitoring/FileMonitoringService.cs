// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage.Monitoring;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements the IFileMonitoringService as a singleton to orchestrate file monitoring across multiple locations.
/// Manages LocationHandler instances, coordinates global behaviors, and provides control over the monitoring process.
/// </summary>
/// <remarks>
/// Initializes a new instance of the FileMonitoringService.
/// </remarks>
/// <param name="logger">The logger instance for logging service operations.</param>
/// <param name="handlers">The collection of LocationHandler instances to manage.</param>
/// <param name="behaviors">The global monitoring behaviors for scan observability.</param>
public class FileMonitoringService(
    ILogger<FileMonitoringService> logger,
    IEnumerable<ILocationHandler> handlers = null,
    IEnumerable<IMonitoringBehavior> behaviors = null) : IFileMonitoringService
{
    private readonly ILogger<FileMonitoringService> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly TypedLogger loggerTyped = new TypedLogger(logger); // Strongly-typed logger for efficiency
    private readonly IEnumerable<ILocationHandler> handlers = handlers ?? [];
    private readonly IEnumerable<IMonitoringBehavior> behaviors = behaviors ?? [];
    private bool isStarted = false;

    /// <summary>
    /// Starts monitoring all configured locations asynchronously.
    /// Initiates real-time watching (if enabled) and event processing for each location.
    /// </summary>
    /// <param name="token">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken token)
    {
        if (this.isStarted)
        {
            this.loggerTyped.LogWarningAlreadyStarted();
            return;
        }

        this.loggerTyped.LogInformationStartingService(this.handlers.Count());
        foreach (var handler in this.handlers)
        {
            await handler.StartAsync(token);
        }

        this.isStarted = true;
        this.loggerTyped.LogInformationServiceStarted();
    }

    /// <summary>
    /// Stops monitoring all locations asynchronously.
    /// Halts real-time watching and event processing, cleaning up resources as necessary.
    /// </summary>
    /// <param name="token">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken token)
    {
        if (!this.isStarted)
        {
            this.loggerTyped.LogWarningNotStarted();
            return;
        }

        this.loggerTyped.LogInformationStoppingService(this.handlers.Count());
        foreach (var handler in this.handlers)
        {
            await handler.StopAsync(token);
        }

        this.isStarted = false;
        this.loggerTyped.LogInformationServiceStopped();
    }

    /// <summary>
    /// Triggers an on-demand scan for a specific location asynchronously.
    /// Compares the current state with historical data to detect changes and enqueue events.
    /// </summary>
    /// <param name="locationName">Specifies the name of the location to be scanned.</param>
    /// <param name="options">Provides additional settings for the scan process.</param>
    /// <param name="progress">Reports the progress of the scan operation.</param>
    /// <param name="token">Allows for cancellation of the scan operation.</param>
    /// <returns>Returns the context of the scan after completion.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified location cannot be found in the handlers.</exception>
    public async Task<ScanContext> ScanLocationAsync(string locationName, ScanOptions options = null, IProgress<ScanProgress> progress = null, CancellationToken token = default)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        this.loggerTyped.LogInformationScanningLocation(locationName);
        var context = await handler.ScanAsync(options, progress, token);
        this.loggerTyped.LogInformationScanCompleted(locationName, context.Events.Count);

        return context;
    }

    /// <summary>
    /// Pauses monitoring for a specific location asynchronously.
    /// Temporarily halts event processing and real-time watching without stopping the service entirely.
    /// </summary>
    /// <param name="locationName">The name of the location to pause (e.g., "Docs").</param>
    /// <returns>A task representing the asynchronous pause operation.</returns>
    public async Task PauseLocationAsync(string locationName)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        this.loggerTyped.LogInformationPausingLocation(locationName);
        await handler.PauseAsync();
    }

    /// <summary>
    /// Resumes monitoring for a specific location asynchronously.
    /// Restarts event processing and real-time watching for a previously paused location.
    /// </summary>
    /// <param name="locationName">The name of the location to resume (e.g., "Docs").</param>
    /// <returns>A task representing the asynchronous resume operation.</returns>
    public async Task ResumeLocationAsync(string locationName)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        this.loggerTyped.LogInformationResumingLocation(locationName);
        await handler.ResumeAsync();
    }

    /// <summary>
    /// Restarts monitoring for a specific location asynchronously.
    /// Stops and then restarts the location's monitoring process, optionally performing an initial scan.
    /// </summary>
    /// <param name="locationName">The name of the location to restart (e.g., "Docs").</param>
    /// <param name="token">The cancellation token to stop the restart if needed.</param>
    /// <returns>A task representing the asynchronous restart operation.</returns>
    public async Task RestartLocationAsync(string locationName, CancellationToken token)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        this.loggerTyped.LogInformationRestartingLocation(locationName);
        await handler.StopAsync(token);
        await handler.StartAsync(token);
    }

    /// <summary>
    /// Retrieves the current status of a specific location asynchronously.
    /// Provides details such as whether it's active, paused, or the queue size.
    /// </summary>
    /// <param name="locationName">The name of the location to query (e.g., "Docs").</param>
    /// <returns>A LocationStatus object with the location's current state.</returns>
    public async Task<LocationStatus> GetLocationStatusAsync(string locationName)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        return await handler.GetStatusAsync();
    }

    /// <summary>
    /// Retrieves the status of all configured locations asynchronously.
    /// Returns a dictionary mapping location names to their current states.
    /// </summary>
    /// <returns>A dictionary of location names and their LocationStatus objects.</returns>
    public async Task<Dictionary<string, LocationStatus>> GetAllLocationStatusAsync()
    {
        var statuses = new Dictionary<string, LocationStatus>();
        foreach (var handler in this.handlers)
        {
            statuses[handler.Options.LocationName] = await handler.GetStatusAsync();
        }
        return statuses;
    }

    /// <summary>
    /// Checks if a specific location is currently active (monitoring and processing events) asynchronously.
    /// </summary>
    /// <param name="locationName">The name of the location to check (e.g., "Docs").</param>
    /// <returns>True if the location is active; false otherwise.</returns>
    public async Task<bool> IsLocationActiveAsync(string locationName)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        var status = await handler.GetStatusAsync();
        return status.IsActive;
    }

    /// <summary>
    /// Checks the overall health of the FileMonitoringService asynchronously.
    /// Verifies accessibility of storage providers and internal state consistency.
    /// </summary>
    /// <returns>True if the service is healthy; false otherwise.</returns>
    public async Task<bool> IsHealthyAsync()
    {
        this.loggerTyped.LogDebugCheckingHealth(this.handlers.Count());
        foreach (var handler in this.handlers)
        {
            var status = await handler.GetStatusAsync();
            if (!status.IsActive && !status.IsPaused)
            {
                this.loggerTyped.LogWarningLocationUnhealthy(handler.Options.LocationName);
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Gets the current size of the event queue for a specific location.
    /// Useful for monitoring backlog and performance.
    /// </summary>
    /// <param name="locationName">The name of the location to query (e.g., "Docs").</param>
    /// <returns>The number of pending events in the queue.</returns>
    public int GetQueueSize(string locationName)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        return handler.GetQueueSize();
    }

    /// <summary>
    /// Checks if the event queue for a specific location is empty asynchronously.
    /// </summary>
    /// <param name="locationName">The name of the location to check (e.g., "Docs").</param>
    /// <returns>True if the queue is empty; false otherwise.</returns>
    public async Task<bool> IsQueueEmptyAsync(string locationName)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        return await handler.IsQueueEmptyAsync();
    }

    /// <summary>
    /// Waits until the event queue for a specific location is empty or a timeout occurs asynchronously.
    /// Useful for ensuring all events are processed before proceeding.
    /// </summary>
    /// <param name="locationName">The name of the location to wait for (e.g., "Docs").</param>
    /// <param name="timeout">The maximum time to wait before timing out.</param>
    /// <returns>A task that completes when the queue is empty or the timeout is reached.</returns>
    public async Task WaitForQueueEmptyAsync(string locationName, TimeSpan timeout)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        await handler.WaitForQueueEmptyAsync(timeout);
    }

    /// <summary>
    /// Retrieves the names of active processors for a specific location asynchronously.
    /// Returns processors that are currently enabled in the processing chain.
    /// </summary>
    /// <param name="locationName">The name of the location to query (e.g., "Docs").</param>
    /// <returns>A list of active processor names.</returns>
    public async Task<IEnumerable<string>> GetActiveProcessorsAsync(string locationName)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        return await handler.GetActiveProcessorsAsync();
    }

    /// <summary>
    /// Enables a specific processor for a location asynchronously.
    /// Allows runtime activation of a processor in the chain.
    /// </summary>
    /// <param name="locationName">The name of the location (e.g., "Docs").</param>
    /// <param name="processorName">The name of the processor to enable.</param>
    /// <returns>A task representing the asynchronous enable operation.</returns>
    public async Task EnableProcessorAsync(string locationName, string processorName)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        this.loggerTyped.LogInformationEnablingProcessor(locationName, processorName);
        await handler.EnableProcessorAsync(processorName);
    }

    /// <summary>
    /// Disables a specific processor for a location asynchronously.
    /// Allows runtime deactivation of a processor in the chain without removing it.
    /// </summary>
    /// <param name="locationName">The name of the location (e.g., "Docs").</param>
    /// <param name="processorName">The name of the processor to disable.</param>
    /// <returns>A task representing the asynchronous disable operation.</returns>
    public async Task DisableProcessorAsync(string locationName, string processorName)
    {
        var handler = this.handlers.FirstOrDefault(h => h.Options.LocationName == locationName);
        if (handler == null)
        {
            this.loggerTyped.LogErrorLocationNotFound(locationName);
            throw new KeyNotFoundException($"Location '{locationName}' not found.");
        }

        this.loggerTyped.LogInformationDisablingProcessor(locationName, processorName);
        await handler.DisableProcessorAsync(processorName);
    }

    /// <summary>
    /// Event raised when a FileEvent is received from a location (either real-time or scan).
    /// Allows external subscribers to react to new events.
    /// </summary>
    public event EventHandler<FileEventReceivedEventArgs> FileEventReceived
    {
        add => throw new NotImplementedException("Event subscription will be implemented with LocationHandler integration.");
        remove => throw new NotImplementedException("Event unsubscription will be implemented with LocationHandler integration.");
    }

    /// <summary>
    /// Event raised when an error occurs during event processing.
    /// Provides details for external error handling or logging.
    /// </summary>
    public event EventHandler<ProcessingErrorEventArgs> ProcessingError
    {
        add => throw new NotImplementedException("Event subscription will be implemented with LocationHandler integration.");
        remove => throw new NotImplementedException("Event unsubscription will be implemented with LocationHandler integration.");
    }

    // Strongly-typed logger class inspired by RepositoryLoggingBehavior.TypedLogger
    private class TypedLogger(ILogger logger)
    {
        private readonly ILogger logger = logger;

        // Warning logs
        public void LogWarningAlreadyStarted() =>
            this.logger.LogWarning("{LogKey} filemonitoring: service already started", Constants.LogKey);

        public void LogWarningNotStarted() =>
            this.logger.LogWarning("{LogKey} filemonitoring: service not started", Constants.LogKey);

        public void LogWarningLocationUnhealthy(string locationName) =>
            this.logger.LogWarning("{LogKey} filemonitoring: location unhealthy (LocationName={LocationName})",
                Constants.LogKey, locationName);

        // Information logs
        public void LogInformationStartingService(int locationCount) =>
            this.logger.LogInformation("{LogKey} filemonitoring: starting service (Locations=#{LocationCount})",
                Constants.LogKey, locationCount);

        public void LogInformationServiceStarted() =>
            this.logger.LogInformation("{LogKey} filemonitoring: service started", Constants.LogKey);

        public void LogInformationStoppingService(int locationCount) =>
            this.logger.LogInformation("{LogKey} filemonitoring: stopping service (Locations=#{LocationCount})",
                Constants.LogKey, locationCount);

        public void LogInformationServiceStopped() =>
            this.logger.LogInformation("{LogKey} filemonitoring: service stopped", Constants.LogKey);

        public void LogInformationScanningLocation(string locationName) =>
            this.logger.LogInformation("{LogKey} filemonitoring: scanning location (LocationName={LocationName})",
                Constants.LogKey, locationName);

        public void LogInformationScanCompleted(string locationName, int changeCount) =>
            this.logger.LogInformation("{LogKey} filemonitoring: scan completed (LocationName={LocationName}, Changes={ChangeCount})",
                Constants.LogKey, locationName, changeCount);

        public void LogInformationPausingLocation(string locationName) =>
            this.logger.LogInformation("{LogKey} filemonitoring: pausing location (LocationName={LocationName})",
                Constants.LogKey, locationName);

        public void LogInformationResumingLocation(string locationName) =>
            this.logger.LogInformation("{LogKey} filemonitoring: resuming location (LocationName={LocationName})",
                Constants.LogKey, locationName);

        public void LogInformationRestartingLocation(string locationName) =>
            this.logger.LogInformation("{LogKey} filemonitoring: restarting location (LocationName={LocationName})",
                Constants.LogKey, locationName);

        public void LogInformationEnablingProcessor(string locationName, string processorName) =>
            this.logger.LogInformation("{LogKey} filemonitoring: enabling processor (LocationName={LocationName}, ProcessorName={ProcessorName})",
                Constants.LogKey, locationName, processorName);

        public void LogInformationDisablingProcessor(string locationName, string processorName) =>
            this.logger.LogInformation("{LogKey} filemonitoring: disabling processor (LocationName={LocationName}, ProcessorName={ProcessorName})",
                Constants.LogKey, locationName, processorName);

        // Debug logs
        public void LogDebugCheckingHealth(int locationCount) =>
            this.logger.LogDebug("{LogKey} filemonitoring: checking health (Locations={LocationCount})",
                Constants.LogKey, locationCount);

        // Error logs
        public void LogErrorLocationNotFound(string locationName) =>
            this.logger.LogError("{LogKey} filemonitoring: location not found (LocationName={LocationName})",
                Constants.LogKey, locationName);
    }
}