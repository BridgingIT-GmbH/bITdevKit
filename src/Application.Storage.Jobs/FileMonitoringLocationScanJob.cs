// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage.JobScheduling;

using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

/// <summary>
/// Represents file monitoring location scan job.
/// </summary>
/// <param name="loggerFactory">The factory used to create loggers.</param>
/// <param name="scopeFactory">The scope factory used by the operation.</param>
[DisallowConcurrentExecution]
[Obsolete("This job is obsolete and will be removed in future versions. Use Application.Storage.Jobs/Files/FileMonitoringLocationScanJob instead.")]
public partial class FileMonitoringLocationScanJob( // obsolete job, replaced by Files/FileMonitoringLocationScanJob based on ned Jobs instead of JobScheduling
                                                    //  retained for backward compatibility, but will be removed in future versions as JobScheduling is deprecated
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory) : JobBase(loggerFactory), IRetryJobScheduling
{
    RetryJobSchedulingOptions IRetryJobScheduling.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    /// <inheritdoc/>
    public override async Task Process(Quartz.IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var fileMonitoringService = scope.ServiceProvider.GetRequiredService<IFileMonitoringService>();

        // Retrieve the location name from the job data map
        this.Data.TryGetValue(DataKeys.LocationName, out var locationName);
        if (string.IsNullOrEmpty(locationName))
        {
            TypedLogger.LogMissingLocation(this.Logger, Constants.LogKey);
            throw new ArgumentException($"{DataKeys.LocationName} must be provided in the job data.");
        }

        // Log the start of the scan
        var scanOptions = this.CreateScanOptions();
        var progressReports = new List<FileScanProgress>();
        var progress = new Progress<FileScanProgress>(report =>
        {
            progressReports.Add(report);
            TypedLogger.LogProgress(this.Logger, Constants.LogKey, locationName, report.FilesScanned, report.TotalFiles, report.PercentageComplete, report.ElapsedTime.TotalMilliseconds);
        });

        TypedLogger.LogStartScan(this.Logger, Constants.LogKey, locationName, scanOptions);
        var scanContext = await fileMonitoringService.ScanLocationAsync(locationName, scanOptions, progress, cancellationToken);
        TypedLogger.LogScanCompleted(this.Logger, Constants.LogKey, locationName, scanContext.Events?.Count ?? 0);
        if (scanContext.Events.SafeAny())
        {
            this.Data.AddOrUpdate("Detected events", scanContext.Events.Count.ToString());
            this.Data.AddOrUpdate("Detected added events", scanContext.Events.Where(e => e.EventType == FileEventType.Added).Count().ToString());
            this.Data.AddOrUpdate("Detected changed events", scanContext.Events.Where(e => e.EventType == FileEventType.Changed).Count().ToString());
            this.Data.AddOrUpdate("Detected deleted events", scanContext.Events.Where(e => e.EventType == FileEventType.Deleted).Count().ToString());
            foreach (var evt in scanContext.Events.SafeNull())
            {
                //TypedLogger.LogEventProcessed(this.Logger, Constants.LogKey, locationName, evt.EventType.ToString(), evt.FilePath, evt.FileSize, evt.DetectedDate);
                this.Data.AddOrUpdate($"Detected event for {evt.FilePath}", evt.EventType.ToString());
            }
        }
        else
        {
            this.Data.AddOrUpdate("Detected events", "0");
            TypedLogger.LogNoChanges(this.Logger, Constants.LogKey, locationName);
        }
    }

    private FileScanOptions CreateScanOptions()
    {
        var scanOptions = new FileScanOptions // Configure scan options with some defaults
        {
            WaitForProcessing = true,
            //SkipChecksum = true,
            DelayPerFile = TimeSpan.FromMilliseconds(100),
            Timeout = TimeSpan.FromSeconds(90)
        };

        // get all options from the job data, only set if they are set
        if (this.Data.TryGetValue(DataKeys.WaitForProcessing, out var waitForProcessing) && bool.TryParse(waitForProcessing, out var waitForProcessingValue))
        {
            scanOptions.WaitForProcessing = waitForProcessingValue;
        }

        if (this.Data.TryGetValue(DataKeys.DelayPerFile, out var delayPerFile) && TimeSpan.TryParse(delayPerFile, out var delayPerFileValue))
        {
            scanOptions.DelayPerFile = delayPerFileValue;
        }

        if (this.Data.TryGetValue(DataKeys.BatchSize, out var batchSize) && int.TryParse(batchSize, out var batchSizeValue))
        {
            scanOptions.BatchSize = batchSizeValue;
        }

        if (this.Data.TryGetValue(DataKeys.ProgressIntervalPercentage, out var progressIntervalPercentage) && int.TryParse(progressIntervalPercentage, out var progressIntervalPercentageValue))
        {
            scanOptions.ProgressIntervalPercentage = progressIntervalPercentageValue;
        }

        if (this.Data.TryGetValue(DataKeys.FileFilter, out var fileFilter))
        {
            scanOptions.FileFilter = fileFilter;
        }

        if (this.Data.TryGetValue(DataKeys.FileBlackListFilter, out var fileBlacklistFilter))
        {
            scanOptions.FileBlackListFilter = fileBlacklistFilter?.Split(";")?.Select(f => f.Trim())?.ToArray();
        }

        if (this.Data.TryGetValue(DataKeys.MaxFilesToScan, out var maxFilesToScan) && int.TryParse(maxFilesToScan, out var maxFilesToScanValue))
        {
            scanOptions.MaxFilesToScan = maxFilesToScanValue;
        }

        if (this.Data.TryGetValue(DataKeys.ThrowIfDirectoryNotExists, out var throwIfDirectoryNotExists) && bool.TryParse(throwIfDirectoryNotExists, out var throwIfDirectoryNotExistsValue))
        {
            scanOptions.ThrowIfDirectoryNotExists = throwIfDirectoryNotExistsValue;
        }

        if (this.Data.TryGetValue(DataKeys.Timeout, out var timeout) && TimeSpan.TryParse(timeout, out var timeoutValue))
        {
            scanOptions.Timeout = timeoutValue;
        }

        return scanOptions;
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the start scan operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="locationName">The location name used by the operation.</param>
        /// <param name="options">The options controlling the operation.</param>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] job: scan started (location={LocationName}) {@Options}")]
        public static partial void LogStartScan(ILogger logger, string logKey, string locationName, FileScanOptions options);

        /// <summary>
        /// Writes a log entry for the scan completed operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="locationName">The location name used by the operation.</param>
        /// <param name="eventCount">The event count used by the operation.</param>
        [LoggerMessage(1, LogLevel.Information, "[{LogKey}] job: scan completed (location={LocationName}, eventCount={EventCount})")]
        public static partial void LogScanCompleted(ILogger logger, string logKey, string locationName, int eventCount);

        /// <summary>
        /// Writes a log entry for the no changes operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="locationName">The location name used by the operation.</param>
        [LoggerMessage(2, LogLevel.Information, "[{LogKey}] job: no changes (location={LocationName})")]
        public static partial void LogNoChanges(ILogger logger, string logKey, string locationName);

        /// <summary>
        /// Writes a log entry for the event processed operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="locationName">The location name used by the operation.</param>
        /// <param name="eventType">The event type used by the operation.</param>
        /// <param name="filePath">The file path used by the operation.</param>
        /// <param name="fileSize">The file size used by the operation.</param>
        /// <param name="detectedDate">The detected date used by the operation.</param>
        [LoggerMessage(3, LogLevel.Information, "[{LogKey}] job: event processed (location={LocationName}, eventType={EventType}, filePath={FilePath}, size={FileSize}, detected={DetectedDate})")]
        public static partial void LogEventProcessed(ILogger logger, string logKey, string locationName, string eventType, string filePath, long? fileSize, DateTimeOffset detectedDate);

        /// <summary>
        /// Writes a log entry for the progress operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="locationName">The location name used by the operation.</param>
        /// <param name="filesScanned">The files scanned used by the operation.</param>
        /// <param name="totalFiles">The total files used by the operation.</param>
        /// <param name="percentageComplete">The percentage complete used by the operation.</param>
        /// <param name="timeElapsed">The time elapsed used by the operation.</param>
        [LoggerMessage(4, LogLevel.Information, "[{LogKey}] job: progress (location={LocationName}, filesScanned={FilesScanned}, totalFiles={TotalFiles}, percentageComplete={PercentageComplete:F2}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProgress(ILogger logger, string logKey, string locationName, long filesScanned, long totalFiles, double percentageComplete, double timeElapsed);

        /// <summary>
        /// Writes a log entry for the missing location operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        [LoggerMessage(5, LogLevel.Error, "[{LogKey}] job: missing location")]
        public static partial void LogMissingLocation(ILogger logger, string logKey);
    }

    /// <summary>
    /// Represents data keys.
    /// </summary>
    public struct DataKeys
    {
        /// <summary>
        /// Defines the location name value.
        /// </summary>
        public const string LocationName = "LocationName";
        /// <summary>
        /// Defines the wait for processing value.
        /// </summary>
        public const string WaitForProcessing = "WaitForProcessing";
        /// <summary>
        /// Defines the delay per file value.
        /// </summary>
        public const string DelayPerFile = "DelayPerFile";
        /// <summary>
        /// Defines the batch size value.
        /// </summary>
        public const string BatchSize = "BatchSize";
        /// <summary>
        /// Defines the progress interval percentage value.
        /// </summary>
        public const string ProgressIntervalPercentage = "ProgressIntervalPercentage";
        /// <summary>
        /// Defines the file filter value.
        /// </summary>
        public const string FileFilter = "FileFilter";
        /// <summary>
        /// Defines the file black list filter value.
        /// </summary>
        public const string FileBlackListFilter = "FileBlackListFilter";
        /// <summary>
        /// Defines the max files to scan value.
        /// </summary>
        public const string MaxFilesToScan = "MaxFilesToScan";
        /// <summary>
        /// Defines the throw if directory not exists value.
        /// </summary>
        public const string ThrowIfDirectoryNotExists = "ThrowIfDirectoryNotExists";
        /// <summary>
        /// Defines the timeout value.
        /// </summary>
        public const string Timeout = "Timeout";
    }
}
