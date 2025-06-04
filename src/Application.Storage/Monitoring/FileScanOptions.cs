// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;
using System;
using System.Collections.Generic;

/// <summary>
/// Represents options for scanning, including processing behavior and timing settings.
/// </summary>
public class FileScanOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to wait for processing completion.
    /// </summary>
    public bool WaitForProcessing { get; set; } = false;

    /// <summary>
    /// Gets or sets the timeout duration for scan operations.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the delay to apply between processing individual files.
    /// </summary>
    public TimeSpan DelayPerFile { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the collection of file event types to process during scanning.
    /// </summary>
    public HashSet<FileEventType> EventFilter { get; set; } =
    [
        FileEventType.Added,
        FileEventType.Changed,
        FileEventType.Deleted
    ];

    /// <summary>
    /// Gets or sets the number of files to process in a single batch.
    /// </summary>
    public int BatchSize { get; set; } = 1; // Default: Process 1 file at a time

    /// <summary>
    /// Gets or sets the percentage interval at which to report progress.
    /// </summary>
    public int ProgressIntervalPercentage { get; set; } = 10; // Default: Report every 10%

    /// <summary>
    /// Gets or sets a regex or glob pattern to filter files by path.
    /// </summary>
    public string FileFilter { get; set; } = null; // Regex or glob pattern (e.g., "*.log")

    /// <summary>
    /// Gets or sets a blacklist filter for files using glob patterns.
    /// </summary>
    public string[] FileBlackListFilter { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to skip checksum calculation.
    /// </summary>
    public bool SkipChecksum { get; set; } = false; // Default: Calculate checksums

    /// <summary>
    /// Gets or sets the maximum number of files to scan, or null for no limit.
    /// </summary>
    public int? MaxFilesToScan { get; set; } = null; // Default: No limit

    public bool ThrowIfDirectoryNotExists { get; set; } = false;

    /// <summary>
    /// Gets the default scan options configuration.
    /// </summary>
    public static FileScanOptions Default => new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FileScanOptions"/> class with default values.
    /// </summary>
    public FileScanOptions() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileScanOptions"/> class with specified values.
    /// </summary>
    /// <param name="waitForProcessing">Whether to wait for processing completion.</param>
    /// <param name="timeout">The timeout duration for scan operations.</param>
    /// <param name="delayPerFile">The delay to apply between processing individual files.</param>
    /// <param name="eventFilter">The collection of file event types to process.</param>
    /// <param name="batchSize">The number of files to process in a single batch.</param>
    /// <param name="progressIntervalPercentage">The percentage interval at which to report progress.</param>
    /// <param name="filePathFilter">A regex or glob pattern to filter files by path.</param>
    /// <param name="skipChecksum">Whether to skip checksum calculation.</param>
    /// <param name="maxFilesToScan">The maximum number of files to scan, or null for no limit.</param>
    public FileScanOptions(
        bool waitForProcessing = false,
        TimeSpan? timeout = null,
        TimeSpan? delayPerFile = null,
        IEnumerable<FileEventType> eventFilter = null,
        int batchSize = 1,
        int progressIntervalPercentage = 10,
        string filePathFilter = null,
        bool skipChecksum = false,
        int? maxFilesToScan = null,
        bool thrownIfDirectoryNotExists = false)
    {
        this.WaitForProcessing = waitForProcessing;
        this.Timeout = timeout ?? TimeSpan.Zero;
        this.DelayPerFile = delayPerFile ?? TimeSpan.Zero;
        if (eventFilter != null)
        {
            this.EventFilter = [.. eventFilter];
        }
        this.BatchSize = batchSize;
        this.ProgressIntervalPercentage = progressIntervalPercentage;
        this.FileFilter = filePathFilter;
        this.SkipChecksum = skipChecksum;
        this.MaxFilesToScan = maxFilesToScan;
        this.ThrowIfDirectoryNotExists = thrownIfDirectoryNotExists;
    }
}