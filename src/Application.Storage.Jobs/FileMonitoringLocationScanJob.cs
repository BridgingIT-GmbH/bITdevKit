// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

[DisallowConcurrentExecution]
public partial class FileMonitoringLocationScanJob(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory) : JobBase(loggerFactory), IRetryJobScheduling
{
    RetryJobSchedulingOptions IRetryJobScheduling.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
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
            progressReports.Add(report); TypedLogger.LogProgress(this.Logger, Constants.LogKey, locationName, report.FilesScanned, report.TotalFiles, report.PercentageComplete, report.ElapsedTime.TotalMilliseconds);
        });

        TypedLogger.LogStartScan(this.Logger, Constants.LogKey, locationName, scanOptions);
        var scanContext = await fileMonitoringService.ScanLocationAsync(locationName, scanOptions, progress, cancellationToken);
        TypedLogger.LogScanCompleted(this.Logger, Constants.LogKey, locationName, scanContext.Events?.Count ?? 0);
        if (scanContext.Events.SafeAny())
        {
            this.Data.AddOrUpdate("Detected events", scanContext.Events.Count.ToString());
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
        // Configure scan options with some defaults
        var scanOptions = new FileScanOptions
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

        if (this.Data.TryGetValue(DataKeys.Timeout, out var timeout) && TimeSpan.TryParse(timeout, out var timeoutValue))
        {
            scanOptions.Timeout = timeoutValue;
        }

        return scanOptions;
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} job: scan started (location={LocationName}) {@Options}")]
        public static partial void LogStartScan(ILogger logger, string logKey, string locationName, FileScanOptions options);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} job: scan completed (location={LocationName}, eventCount={EventCount})")]
        public static partial void LogScanCompleted(ILogger logger, string logKey, string locationName, int eventCount);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} job: no changes (location={LocationName})")]
        public static partial void LogNoChanges(ILogger logger, string logKey, string locationName);

        [LoggerMessage(3, LogLevel.Information, "{LogKey} job: event processed (location={LocationName}, eventType={EventType}, filePath={FilePath}, size={FileSize}, detected={DetectedDate})")]
        public static partial void LogEventProcessed(ILogger logger, string logKey, string locationName, string eventType, string filePath, long? fileSize, DateTimeOffset detectedDate);

        [LoggerMessage(4, LogLevel.Information, "{LogKey} job: progress (location={LocationName}, filesScanned={FilesScanned}, totalFiles={TotalFiles}, percentageComplete={PercentageComplete:F2}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProgress(ILogger logger, string logKey, string locationName, long filesScanned, long totalFiles, double percentageComplete, double timeElapsed);

        [LoggerMessage(5, LogLevel.Error, "{LogKey} job: missing location")]
        public static partial void LogMissingLocation(ILogger logger, string logKey);
    }

    public struct DataKeys
    {
        public const string LocationName = "LocationName";
        public const string WaitForProcessing = "WaitForProcessing";
        public const string DelayPerFile = "DelayPerFile";
        public const string BatchSize = "BatchSize";
        public const string ProgressIntervalPercentage = "ProgressIntervalPercentage";
        public const string FileFilter = "FileFilter";
        public const string FileBlackListFilter = "FileBlackListFilter";
        public const string MaxFilesToScan = "MaxFilesToScan";
        public const string Timeout = "Timeout";
    }
}