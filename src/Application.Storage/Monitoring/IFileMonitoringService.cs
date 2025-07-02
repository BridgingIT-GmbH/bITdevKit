﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for the FileMonitoringService, which orchestrates file monitoring across multiple locations.
/// Provides methods for starting, stopping, scanning, and managing the monitoring process, along with events for external integration.
/// </summary>
public interface IFileMonitoringService
{
    /// <summary>
    /// Starts monitoring all configured locations asynchronously.
    /// Initiates real-time watching (if enabled) and event processing for each location.
    /// </summary>
    /// <param name="token">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    Task StartAsync(CancellationToken token = default);

    /// <summary>
    /// Stops monitoring all locations asynchronously.
    /// Halts real-time watching and event processing, cleaning up resources as necessary.
    /// </summary>
    /// <param name="token">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    Task StopAsync(CancellationToken token = default);

    /// <summary>
    /// Triggers an on-demand scan for a specific location asynchronously.
    /// Compares the current state with historical data to detect changes and enqueue events.
    /// </summary>
    /// <param name="locationName">Specifies the name of the location to be scanned.</param>
    /// <param name="options">Provides additional settings that modify the scan behavior.</param>
    /// <param name="progress">Reports the progress of the scan operation.</param>
    /// <param name="token">Allows for cancellation of the scan operation if needed.</param>
    /// <returns>Returns a task that resolves to the scan context for the specified location.</returns>
    Task<FileScanContext> ScanLocationAsync(string locationName, FileScanOptions options = null, IProgress<FileScanProgress> progress = null, CancellationToken token = default);

    /// <summary>
    /// Pauses monitoring for a specific location asynchronously.
    /// Temporarily halts event processing and real-time watching without stopping the service entirely.
    /// </summary>
    /// <param name="locationName">The name of the location to pause (e.g., "Docs").</param>
    /// <returns>A task representing the asynchronous pause operation.</returns>
    Task PauseLocationAsync(string locationName);

    /// <summary>
    /// Resumes monitoring for a specific location asynchronously.
    /// Restarts event processing and real-time watching for a previously paused location.
    /// </summary>
    /// <param name="locationName">The name of the location to resume (e.g., "Docs").</param>
    /// <returns>A task representing the asynchronous resume operation.</returns>
    Task ResumeLocationAsync(string locationName);

    /// <summary>
    /// Restarts monitoring for a specific location asynchronously.
    /// Stops and then restarts the location's monitoring process, optionally performing an initial scan.
    /// </summary>
    /// <param name="locationName">The name of the location to restart (e.g., "Docs").</param>
    /// <param name="token">The cancellation token to stop the restart if needed.</param>
    /// <returns>A task representing the asynchronous restart operation.</returns>
    Task RestartLocationAsync(string locationName, CancellationToken token = default);

    /// <summary>
    /// Retrieves the current status of a specific location asynchronously.
    /// Provides details such as whether it's active, paused, or the queue size.
    /// </summary>
    /// <param name="locationName">The name of the location to query (e.g., "Docs").</param>
    /// <returns>A LocationStatus object with the location's current state.</returns>
    Task<LocationStatus> GetLocationStatusAsync(string locationName);

    /// <summary>
    /// Retrieves the status of all configured locations asynchronously.
    /// Returns a dictionary mapping location names to their current states.
    /// </summary>
    /// <returns>A dictionary of location names and their LocationStatus objects.</returns>
    Task<Dictionary<string, LocationStatus>> GetAllLocationStatusAsync();

    /// <summary>
    /// Retrieves an instance that handles location-related operations based on the specified name.
    /// The handler provides methods to manage and interact with the location like the StorageProvider.
    /// </summary>
    /// <param name="locationName">The name of the location for which the handler is being requested.</param>
    /// <returns>An object that provides methods to manage and interact with the specified location.</returns>
    ILocationHandler GetLocation(string locationName);

    /// <summary>
    /// Checks if a specific location is currently active (monitoring and processing events) asynchronously.
    /// </summary>
    /// <param name="locationName">The name of the location to check (e.g., "Docs").</param>
    /// <returns>True if the location is active; false otherwise.</returns>
    Task<bool> IsLocationActiveAsync(string locationName);

    /// <summary>
    /// Checks the overall health of the FileMonitoringService asynchronously.
    /// Verifies accessibility of storage providers and internal state consistency.
    /// </summary>
    /// <returns>True if the service is healthy; false otherwise.</returns>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// Gets the current size of the event queue for a specific location.
    /// Useful for monitoring backlog and performance.
    /// </summary>
    /// <param name="locationName">The name of the location to query (e.g., "Docs").</param>
    /// <returns>The number of pending events in the queue.</returns>
    int GetQueueSize(string locationName);

    /// <summary>
    /// Checks if the event queue for a specific location is empty asynchronously.
    /// </summary>
    /// <param name="locationName">The name of the location to check (e.g., "Docs").</param>
    /// <returns>True if the queue is empty; false otherwise.</returns>
    Task<bool> IsQueueEmptyAsync(string locationName);

    /// <summary>
    /// Waits until the event queue for a specific location is empty or a timeout occurs asynchronously.
    /// Useful for ensuring all events are processed before proceeding.
    /// </summary>
    /// <param name="locationName">The name of the location to wait for (e.g., "Docs").</param>
    /// <param name="timeout">The maximum time to wait before timing out.</param>
    /// <returns>A task that completes when the queue is empty or the timeout is reached.</returns>
    Task WaitForQueueEmptyAsync(string locationName, TimeSpan timeout);

    /// <summary>
    /// Retrieves the names of active processors for a specific location asynchronously.
    /// Returns processors that are currently enabled in the processing chain.
    /// </summary>
    /// <param name="locationName">The name of the location to query (e.g., "Docs").</param>
    /// <returns>A list of active processor names.</returns>
    Task<IEnumerable<string>> GetActiveProcessorsAsync(string locationName);

    /// <summary>
    /// Enables a specific processor for a location asynchronously.
    /// Allows runtime activation of a processor in the chain.
    /// </summary>
    /// <param name="locationName">The name of the location (e.g., "Docs").</param>
    /// <param name="processorName">The name of the processor to enable.</param>
    /// <returns>A task representing the asynchronous enable operation.</returns>
    Task EnableProcessorAsync(string locationName, string processorName);

    /// <summary>
    /// Disables a specific processor for a location asynchronously.
    /// Allows runtime deactivation of a processor in the chain without removing it.
    /// </summary>
    /// <param name="locationName">The name of the location (e.g., "Docs").</param>
    /// <param name="processorName">The name of the processor to disable.</param>
    /// <returns>A task representing the asynchronous disable operation.</returns>
    Task DisableProcessorAsync(string locationName, string processorName);

    /// <summary>
    /// Event raised when a FileEvent is received from a location (either real-time or scan).
    /// Allows external subscribers to react to new events.
    /// </summary>
    event EventHandler<FileEventReceivedEventArgs> FileEventReceived;

    /// <summary>
    /// Event raised when an error occurs during event processing.
    /// Provides details for external error handling or logging.
    /// </summary>
    event EventHandler<ProcessingErrorEventArgs> ProcessingError;
}

/// <summary>
/// Arguments for the FileEventReceived event, carrying the detected FileEvent.
/// </summary>
public class FileEventReceivedEventArgs(FileEvent fileEvent) : EventArgs
{
    public FileEvent FileEvent { get; } = fileEvent;
}

/// <summary>
/// Arguments for the ProcessingError event, carrying details of the processing failure.
/// </summary>
public class ProcessingErrorEventArgs(FileEvent fileEvent, string processorName, Exception exception) : EventArgs
{
    public FileEvent FileEvent { get; } = fileEvent;

    public string ProcessorName { get; } = processorName;

    public Exception Exception { get; } = exception;
}

/// <summary>
/// Represents the status of a monitored location.
/// </summary>
public class LocationStatus
{
    public string LocationName { get; set; }

    public bool IsActive { get; set; }

    public bool IsPaused { get; set; }

    public int QueueSize { get; set; }

    public DateTimeOffset LastScanTime { get; set; }
}
