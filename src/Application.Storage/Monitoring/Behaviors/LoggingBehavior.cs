// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides logging behavior.
/// </summary>
/// <param name="logger">The logger that receives diagnostic events.</param>
public class LoggingBehavior(ILogger<LoggingBehavior> logger) : IMonitoringBehavior
{
    private readonly ILogger<LoggingBehavior> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Executes the on scan started operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void OnScanStarted(FileScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.logger.LogInformation("[{LogKey}] filemonitoring: scan started for location {LocationName} at {StartTime}",
            Constants.LogKey,
            context.LocationName,
            context.StartTime);
    }

    /// <summary>
    /// Executes the on file detected operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="fileEvent">The file event used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void OnFileDetected(FileScanContext context, FileEvent fileEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.logger.LogInformation("[{LogKey}] filemonitoring: file detected in location {LocationName} {FilePath} (EventType={EventType}, Size={FileSize}, Checksum={Checksum})",
            Constants.LogKey,
            context.LocationName,
            fileEvent.FilePath,
            fileEvent.EventType,
            fileEvent.FileSize,
            fileEvent.Checksum);
    }

    /// <summary>
    /// Executes the on scan completed operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="duration">The duration used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public void OnScanCompleted(FileScanContext context, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.logger.LogInformation("[{LogKey}] filemonitoring: scan completed for location {LocationName} (Changes=#{ChangeCount}) took -> {TimeElapsed:0.0000} ms",
            Constants.LogKey,
            context.LocationName,
            context.Events.Count,
            duration.TotalMilliseconds);
    }
}
