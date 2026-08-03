// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the typed payload for <see cref="FileMonitoringLocationScanJob"/>.
/// </summary>
/// <example>
/// <code>
/// var data = new FileMonitoringLocationScanJobData
/// {
///     LocationName = "inbound",
///     FileFilter = "*.*"
/// };
/// </code>
/// </example>
public sealed class FileMonitoringLocationScanJobData
{
    /// <summary>
    /// Gets or sets the configured file-monitoring location name.
    /// </summary>
    public string LocationName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the scan should wait for event processing.
    /// </summary>
    public bool? WaitForProcessing { get; set; }

    /// <summary>
    /// Gets or sets the delay applied between scanned files.
    /// </summary>
    public TimeSpan? DelayPerFile { get; set; }

    /// <summary>
    /// Gets or sets the batch size used by the scanner.
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// Gets or sets the progress reporting interval percentage.
    /// </summary>
    public int? ProgressIntervalPercentage { get; set; }

    /// <summary>
    /// Gets or sets the file include filter.
    /// </summary>
    public string FileFilter { get; set; }

    /// <summary>
    /// Gets or sets file blacklist filters.
    /// </summary>
    public string[] FileBlackListFilter { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of files to scan.
    /// </summary>
    public int? MaxFilesToScan { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a missing directory should fail the scan.
    /// </summary>
    public bool? ThrowIfDirectoryNotExists { get; set; }

    /// <summary>
    /// Gets or sets the scan timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}
