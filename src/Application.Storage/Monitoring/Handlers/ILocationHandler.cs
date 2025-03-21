// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for handling operations on a monitored location, including scanning, processing events, and managing processors.
/// </summary>
public interface ILocationHandler
{
    /// <summary>
    /// Gets the file storage provider associated with this location handler.
    /// </summary>
    IFileStorageProvider Provider { get; }

    /// <summary>
    /// Gets the configuration options for this location handler.
    /// </summary>
    LocationOptions Options { get; }

    /// <summary>
    /// Starts the location handler's monitoring operations.
    /// </summary>
    /// <param name="token">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync(CancellationToken token = default);

    /// <summary>
    /// Stops the location handler's monitoring operations.
    /// </summary>
    /// <param name="token">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync(CancellationToken token = default);

    /// <summary>
    /// Scans the location for file changes and generates events.
    /// </summary>
    /// <param name="options">Options controlling the scan behavior.</param>
    /// <param name="progress">Handler for reporting scan progress.</param>
    /// <param name="token">Cancellation token to cancel the operation.</param>
    /// <returns>A task containing the scan context with results.</returns>
    Task<FileScanContext> ScanAsync(FileScanOptions options = null, IProgress<FileScanProgress> progress = null, CancellationToken token = default);

    /// <summary>
    /// Temporarily pauses the location handler's monitoring operations.
    /// </summary>
    /// <param name="token">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PauseAsync(CancellationToken token = default);

    /// <summary>
    /// Resumes the location handler's previously paused monitoring operations.
    /// </summary>
    /// <param name="token">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResumeAsync(CancellationToken token = default);

    /// <summary>
    /// Gets the current status of the location handler.
    /// </summary>
    /// <returns>A task containing the current location status.</returns>
    Task<LocationStatus> GetStatusAsync();

    /// <summary>
    /// Gets the current size of the event processing queue.
    /// </summary>
    /// <returns>The number of events waiting to be processed.</returns>
    int GetQueueSize();

    /// <summary>
    /// Checks if the event processing queue is empty.
    /// </summary>
    /// <returns>A task containing true if the queue is empty; otherwise, false.</returns>
    Task<bool> IsQueueEmptyAsync();

    /// <summary>
    /// Waits for the event processing queue to become empty or until timeout expires.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the queue to empty.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WaitForQueueEmptyAsync(TimeSpan timeout);

    /// <summary>
    /// Gets the names of all currently active processors.
    /// </summary>
    /// <returns>A task containing a collection of active processor names.</returns>
    Task<IEnumerable<string>> GetActiveProcessorsAsync();

    /// <summary>
    /// Enables a specific processor by name.
    /// </summary>
    /// <param name="processorName">The name of the processor to enable.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnableProcessorAsync(string processorName);

    /// <summary>
    /// Disables a specific processor by name.
    /// </summary>
    /// <param name="processorName">The name of the processor to disable.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisableProcessorAsync(string processorName);

    /// <summary>
    /// Gets all file event processors configured for this location.
    /// </summary>
    /// <returns>A collection of all file event processors.</returns>
    IEnumerable<IFileEventProcessor> GetProcessors();
}