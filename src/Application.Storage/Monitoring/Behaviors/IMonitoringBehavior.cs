// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;

/// <summary>
/// Defines the contract for global behaviors that observe scan operations in the FileMonitoring system.
/// Provides hooks for monitoring scan lifecycle events across all locations.
/// </summary>
public interface IMonitoringBehavior
{
    /// <summary>
    /// Called when a scan operation starts for a location.
    /// Allows behaviors to log or track the beginning of the scan process.
    /// </summary>
    /// <param name="context">The scan context containing details like LocationName and StartTime.</param>
    void OnScanStarted(FileScanContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a file change is detected during a scan.
    /// Provides the detected FileEvent for observability (e.g., logging each change).
    /// </summary>
    /// <param name="context">The scan context containing the scan details.</param>
    /// <param name="fileEvent">The FileEvent representing the detected change.</param>
    void OnFileDetected(FileScanContext context, FileEvent fileEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a scan operation completes for a location.
    /// Allows behaviors to summarize the scan (e.g., log duration or total changes).
    /// </summary>
    /// <param name="context">The scan context containing the results and timing.</param>
    /// <param name="duration">The duration of the scan operation.</param>
    void OnScanCompleted(FileScanContext context, TimeSpan duration, CancellationToken cancellationToken = default);
}