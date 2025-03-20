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
    public bool WaitForProcessing { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.Zero;

    public TimeSpan DelayPerFile { get; set; } = TimeSpan.Zero;

    public HashSet<FileEventType> EventFilter { get; set; } =
    [
        FileEventType.Added,
        FileEventType.Changed,
        FileEventType.Deleted
    ]; // Default: Added, Changed, Deleted

    public static ScanOptions Default => new();

    public ScanOptions() { }

    public ScanOptions(bool waitForProcessing = false, TimeSpan? timeout = null, TimeSpan? delayPerFile = null, IEnumerable<FileEventType> eventFilter = null)
    {
        this.WaitForProcessing = waitForProcessing;
        this.Timeout = timeout ?? TimeSpan.Zero;
        this.DelayPerFile = delayPerFile ?? TimeSpan.Zero;
        if (eventFilter != null)
        {
            this.EventFilter = new HashSet<FileEventType>(eventFilter);
        }
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

    public ScanOptions Build()
    {
        return this.options;
    }
}