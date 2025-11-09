// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;

/// <summary>
/// Builder class for creating and configuring scan options with a fluent API.
/// </summary>
public class FileScanOptionsBuilder
{
    private readonly FileScanOptions options = new();

    /// <summary>
    /// Creates a new instance of the <see cref="FileScanOptionsBuilder"/> class.
    /// </summary>
    /// <returns>A new ScanOptionsBuilder instance.</returns>
    public static FileScanOptionsBuilder Create() => new();

    /// <summary>
    /// Sets whether to wait for processing completion.
    /// </summary>
    /// <param name="waitForProcessing">Whether to wait for processing.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder WithWaitForProcessing(bool waitForProcessing = true)
    {
        this.options.WaitForProcessing = waitForProcessing;
        return this;
    }

    /// <summary>
    /// Sets the timeout duration for scan operations.
    /// </summary>
    /// <param name="timeout">The timeout value to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder WithTimeout(TimeSpan timeout)
    {
        this.options.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the delay to apply between processing individual files.
    /// </summary>
    /// <param name="delayPerFile">The delay value to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder WithDelayPerFile(TimeSpan delayPerFile)
    {
        this.options.DelayPerFile = delayPerFile;
        return this;
    }

    /// <summary>
    /// Sets the collection of file event types to process during scanning.
    /// </summary>
    /// <param name="eventTypes">The event types to include.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder WithEventFilter(params FileEventType[] eventTypes)
    {
        this.options.EventFilter = new HashSet<FileEventType>(eventTypes);
        return this;
    }

    /// <summary>
    /// Adds a file event type to the event filter.
    /// </summary>
    /// <param name="eventType">The event type to include.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder IncludeEventType(FileEventType eventType)
    {
        this.options.EventFilter.Add(eventType);
        return this;
    }

    /// <summary>
    /// Removes a file event type from the event filter.
    /// </summary>
    /// <param name="eventType">The event type to exclude.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder ExcludeEventType(FileEventType eventType)
    {
        this.options.EventFilter.Remove(eventType);
        return this;
    }

    /// <summary>
    /// Sets the number of files to process in a single batch.
    /// </summary>
    /// <param name="batchSize">The batch size to set (minimum 1).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder WithBatchSize(int batchSize)
    {
        this.options.BatchSize = batchSize > 0 ? batchSize : 1;
        return this;
    }

    /// <summary>
    /// Sets the percentage interval at which to report progress.
    /// </summary>
    /// <param name="progressIntervalPercentage">The percentage interval to set (minimum 1).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder WithProgressIntervalPercentage(int progressIntervalPercentage)
    {
        this.options.ProgressIntervalPercentage = progressIntervalPercentage > 0 ? progressIntervalPercentage : 10;
        return this;
    }

    /// <summary>
    /// Sets a glob pattern to filter files by path.
    /// </summary>
    /// <param name="pattern">The filter pattern to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder WithFileFilter(string pattern)
    {
        this.options.FileFilter = pattern;
        return this;
    }

    /// <summary>
    /// Set a blacklist filter for files using glob patterns.
    /// </summary>
    /// <param name="patterns">The provided patterns are used to define which files should be excluded from scanning.</param>
    /// <returns>Returns the updated instance of the builder for method chaining.</returns>
    public FileScanOptionsBuilder WithFileBlackListFilter(string[] patterns)
    {
        this.options.FileBlackListFilter = patterns;
        return this;
    }

    /// <summary>
    /// Sets whether to skip checksum calculation.
    /// </summary>
    /// <param name="skipChecksum">Whether to skip checksum calculation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder WithSkipChecksum(bool skipChecksum = true)
    {
        this.options.SkipChecksum = skipChecksum;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of files to scan.
    /// </summary>
    /// <param name="maxFilesToScan">The maximum number of files to scan, or null for no limit.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FileScanOptionsBuilder WithMaxFilesToScan(int maxFilesToScan)
    {
        this.options.MaxFilesToScan = maxFilesToScan > 0 ? maxFilesToScan : null;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured scan options instance.
    /// </summary>
    /// <returns>The configured ScanOptions instance.</returns>
    public FileScanOptions Build()
    {
        return this.options;
    }
}