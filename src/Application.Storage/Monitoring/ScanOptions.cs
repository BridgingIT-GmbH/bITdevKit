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
public class ScanOptions
{
    public bool WaitForProcessing { get; set; } = false;
    public TimeSpan Timeout { get; set; } = TimeSpan.Zero;
    public TimeSpan DelayPerFile { get; set; } = TimeSpan.Zero;
    public HashSet<FileEventType> EventFilter { get; set; } =
    [
        FileEventType.Added,
        FileEventType.Changed,
        FileEventType.Deleted
    ];
    public int BatchSize { get; set; } = 1; // Default: Process 1 file at a time
    public int ProgressIntervalPercentage { get; set; } = 10; // Default: Report every 10%
    public string FilePathFilter { get; set; } = null; // Regex or glob pattern (e.g., "*.log")
    public bool SkipChecksum { get; set; } = false; // Default: Calculate checksums
    public int? MaxFilesToScan { get; set; } = null; // Default: No limit

    public static ScanOptions Default => new();

    public ScanOptions() { }

    public ScanOptions(
        bool waitForProcessing = false,
        TimeSpan? timeout = null,
        TimeSpan? delayPerFile = null,
        IEnumerable<FileEventType> eventFilter = null,
        int batchSize = 1,
        int progressIntervalPercentage = 10,
        string filePathFilter = null,
        bool skipChecksum = false,
        int? maxFilesToScan = null)
    {
        this.WaitForProcessing = waitForProcessing;
        this.Timeout = timeout ?? TimeSpan.Zero;
        this.DelayPerFile = delayPerFile ?? TimeSpan.Zero;
        if (eventFilter != null)
        {
            this.EventFilter = new HashSet<FileEventType>(eventFilter);
        }
        this.BatchSize = batchSize;
        this.ProgressIntervalPercentage = progressIntervalPercentage;
        this.FilePathFilter = filePathFilter;
        this.SkipChecksum = skipChecksum;
        this.MaxFilesToScan = maxFilesToScan;
    }
}

public class ScanOptionsBuilder
{
    private readonly ScanOptions options = new();

    public static ScanOptionsBuilder Create() => new();

    public ScanOptionsBuilder WithWaitForProcessing(bool waitForProcessing = true)
    {
        this.options.WaitForProcessing = waitForProcessing;
        return this;
    }

    public ScanOptionsBuilder WithTimeout(TimeSpan timeout)
    {
        this.options.Timeout = timeout;
        return this;
    }

    public ScanOptionsBuilder WithDelayPerFile(TimeSpan delayPerFile)
    {
        this.options.DelayPerFile = delayPerFile;
        return this;
    }

    public ScanOptionsBuilder WithEventFilter(params FileEventType[] eventTypes)
    {
        this.options.EventFilter = new HashSet<FileEventType>(eventTypes);
        return this;
    }

    public ScanOptionsBuilder IncludeEventType(FileEventType eventType)
    {
        this.options.EventFilter.Add(eventType);
        return this;
    }

    public ScanOptionsBuilder ExcludeEventType(FileEventType eventType)
    {
        this.options.EventFilter.Remove(eventType);
        return this;
    }

    public ScanOptionsBuilder WithBatchSize(int batchSize)
    {
        this.options.BatchSize = batchSize > 0 ? batchSize : 1;
        return this;
    }

    public ScanOptionsBuilder WithProgressIntervalPercentage(int progressIntervalPercentage)
    {
        this.options.ProgressIntervalPercentage = progressIntervalPercentage > 0 ? progressIntervalPercentage : 10;
        return this;
    }

    public ScanOptionsBuilder WithFilePathFilter(string filePathFilter)
    {
        this.options.FilePathFilter = filePathFilter;
        return this;
    }

    public ScanOptionsBuilder WithSkipChecksum(bool skipChecksum = true)
    {
        this.options.SkipChecksum = skipChecksum;
        return this;
    }

    public ScanOptionsBuilder WithMaxFilesToScan(int maxFilesToScan)
    {
        this.options.MaxFilesToScan = maxFilesToScan > 0 ? maxFilesToScan : null;
        return this;
    }

    public ScanOptions Build()
    {
        return this.options;
    }
}