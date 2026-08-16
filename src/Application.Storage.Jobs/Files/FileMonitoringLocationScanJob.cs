// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scheduled job that scans one configured file-monitoring location.
/// </summary>
/// <param name="logger">The logger.</param>
/// <param name="fileMonitoringService">The file-monitoring service.</param>
/// <example>
/// <code>
/// services.AddJobScheduler()
///     .WithJob&lt;FileMonitoringLocationScanJob&gt;("scan-inbound", job =&gt; job
///         .AddTrigger("manual", trigger =&gt; trigger.Manual()));
/// </code>
/// </example>
public partial class FileMonitoringLocationScanJob(
    ILogger<FileMonitoringLocationScanJob> logger,
    IFileMonitoringService fileMonitoringService) : JobBase<FileMonitoringLocationScanJobData>
{
    /// <inheritdoc />
    public override async Task<Result> ExecuteAsync(
        IJobExecutionContext<FileMonitoringLocationScanJobData> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var data = context.Data ?? new FileMonitoringLocationScanJobData();
        var locationName = data.LocationName;
        if (string.IsNullOrEmpty(locationName))
        {
            TypedLogger.LogMissingLocation(logger, Constants.LogKey);
            return Result.Failure($"{nameof(FileMonitoringLocationScanJobData.LocationName)} must be provided in the job data.");
        }

        var scanOptions = CreateScanOptions(data);
        var progressReports = new List<FileScanProgress>();
        var progress = new Progress<FileScanProgress>(report =>
        {
            progressReports.Add(report);
            TypedLogger.LogProgress(logger, Constants.LogKey, locationName, report.FilesScanned, report.TotalFiles, report.PercentageComplete, report.ElapsedTime.TotalMilliseconds);
        });

        TypedLogger.LogStartScan(logger, Constants.LogKey, locationName, scanOptions);
        var scanContext = await fileMonitoringService.ScanLocationAsync(locationName, scanOptions, progress, cancellationToken);
        TypedLogger.LogScanCompleted(logger, Constants.LogKey, locationName, scanContext.Events?.Count ?? 0);
        if (scanContext.Events.SafeAny())
        {
            context.Items["DetectedEvents"] = scanContext.Events.Count;
            context.Items["DetectedAddedEvents"] = scanContext.Events.Count(e => e.EventType == FileEventType.Added);
            context.Items["DetectedChangedEvents"] = scanContext.Events.Count(e => e.EventType == FileEventType.Changed);
            context.Items["DetectedDeletedEvents"] = scanContext.Events.Count(e => e.EventType == FileEventType.Deleted);
            foreach (var evt in scanContext.Events.SafeNull())
            {
                context.Messages.Add($"file-event: {evt.EventType} {evt.FilePath}");
            }
        }
        else
        {
            context.Items["DetectedEvents"] = 0;
            TypedLogger.LogNoChanges(logger, Constants.LogKey, locationName);
        }

        return Result.Success();
    }

    private static FileScanOptions CreateScanOptions(FileMonitoringLocationScanJobData data)
    {
        var scanOptions = new FileScanOptions // Configure scan options with some defaults
        {
            WaitForProcessing = true,
            DelayPerFile = TimeSpan.FromMilliseconds(100),
            Timeout = TimeSpan.FromSeconds(90)
        };

        if (data.WaitForProcessing.HasValue)
        {
            scanOptions.WaitForProcessing = data.WaitForProcessing.Value;
        }

        if (data.DelayPerFile.HasValue)
        {
            scanOptions.DelayPerFile = data.DelayPerFile.Value;
        }

        if (data.BatchSize.HasValue)
        {
            scanOptions.BatchSize = data.BatchSize.Value;
        }

        if (data.ProgressIntervalPercentage.HasValue)
        {
            scanOptions.ProgressIntervalPercentage = data.ProgressIntervalPercentage.Value;
        }

        if (!string.IsNullOrWhiteSpace(data.FileFilter))
        {
            scanOptions.FileFilter = data.FileFilter;
        }

        if (data.FileBlackListFilter is { Length: > 0 })
        {
            scanOptions.FileBlackListFilter = data.FileBlackListFilter
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray();
        }

        if (data.MaxFilesToScan.HasValue)
        {
            scanOptions.MaxFilesToScan = data.MaxFilesToScan.Value;
        }

        if (data.ThrowIfDirectoryNotExists.HasValue)
        {
            scanOptions.ThrowIfDirectoryNotExists = data.ThrowIfDirectoryNotExists.Value;
        }

        if (data.Timeout.HasValue)
        {
            scanOptions.Timeout = data.Timeout.Value;
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
}
