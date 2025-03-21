// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class InMemoryLocationHandler(
    ILogger logger,
    IFileStorageProvider provider,
    IFileEventStore store,
    LocationOptions options,
    IServiceProvider serviceProvider,
    IEnumerable<IMonitoringBehavior> behaviors = null) : LocationHandlerBase(logger, provider, store, options, serviceProvider, behaviors)
{
    private readonly InMemoryFileStorageProvider inMemoryProvider = provider as InMemoryFileStorageProvider ?? throw new ArgumentException("InMemoryLocationHandler requires InMemoryFileStorageProvider.");

    public override async Task StartAsync(CancellationToken token = default)
    {
        await base.StartAsync(token);

        if (!this.options.UseOnDemandOnly && this.provider.SupportsNotifications)
        {
            this.inMemoryProvider.OnFileEvent += this.OnInMemoryFileEvent;
            this.isPaused = false;

            this.logger.LogInformation($"Real-time watching started for location: {this.options.LocationName}");
        }

        if (!this.options.UseOnDemandOnly && this.options.ScanOnStart)
        {
            await this.ScanAsync(null, null, token);
        }
    }

    public override async Task PauseAsync(CancellationToken token = default)
    {
        await base.PauseAsync(token);
        if (!this.isPaused)
        {
            this.inMemoryProvider.OnFileEvent -= this.OnInMemoryFileEvent;
            this.isPaused = true;

            this.logger.LogInformation($"Paused real-time watching for location: {this.options.LocationName}");
        }
    }

    public override async Task ResumeAsync(CancellationToken token = default)
    {
        await base.ResumeAsync(token);
        if (this.isPaused)
        {
            this.inMemoryProvider.OnFileEvent += this.OnInMemoryFileEvent;
            this.isPaused = false;
            this.logger.LogInformation($"Resumed real-time watching for location: {this.options.LocationName}");
        }
    }

    public override async Task StopAsync(CancellationToken token = default)
    {
        this.inMemoryProvider.OnFileEvent -= this.OnInMemoryFileEvent;
        this.isPaused = false;

        await base.StopAsync(token);
    }

    public override async Task<FileScanContext> ScanAsync(
        FileScanOptions options = null,
        IProgress<FileScanProgress> progress = null,
        CancellationToken token = default)
    {
        options ??= FileScanOptions.Default;
        var startTime = DateTimeOffset.UtcNow;
        var context = new FileScanContext { LocationName = this.options.LocationName };
        this.behaviors.ForEach(b => b.OnScanStarted(context), cancellationToken: token);

        var presentFiles = await this.store.GetPresentFilesAsync(this.options.LocationName);
        var currentFiles = new Dictionary<string, FileMetadata>();
        string continuationToken = null;
        var filesScanned = 0;
        var batchCount = 0;

        var initialFiles = (await this.inMemoryProvider.ListFilesAsync("/", this.options.FilePattern, true, null, token)).Value.Files.ToList();
        var estimatedTotalFiles = initialFiles.Count;
        if (options.MaxFilesToScan.HasValue && options.MaxFilesToScan.Value < estimatedTotalFiles)
        {
            estimatedTotalFiles = options.MaxFilesToScan.Value;
        }

        var filesPerPercentage = estimatedTotalFiles / 100.0;
        var nextPercentage = options.ProgressIntervalPercentage;
        var nextReportAtFiles = (int)Math.Ceiling(nextPercentage * filesPerPercentage);

        do
        {
            var result = await this.provider.ListFilesAsync("/", this.options.FilePattern, true, continuationToken, token);
            foreach (var filePath in result.Value.Files.OrderBy(p => p))
            {
                if (!string.IsNullOrEmpty(options.FilePathFilter) && !System.Text.RegularExpressions.Regex.IsMatch(filePath, options.FilePathFilter))
                {
                    continue;
                }

                var metadataResult = await this.provider.GetFileMetadataAsync(filePath, token);
                var checksumResult = options.SkipChecksum
                    ? Result<string>.Success(string.Empty)
                    : await this.provider.GetChecksumAsync(filePath, token);

                if (metadataResult.IsSuccess && checksumResult.IsSuccess)
                {
                    var metadata = metadataResult.Value;
                    currentFiles[filePath] = metadata;
                    var lastEvent = await this.store.GetFileEventAsync(this.options.LocationName, filePath);
                    var eventType = this.DetermineEventType(lastEvent, metadata, checksumResult.Value);

                    if (eventType.HasValue && options.EventFilter.Contains(eventType.Value))
                    {
                        var fileEvent = new FileEvent
                        {
                            LocationName = this.options.LocationName,
                            FilePath = filePath,
                            EventType = eventType.Value,
                            FileSize = metadata.Length,
                            LastModified = metadata.LastModified,
                            Checksum = checksumResult.Value,
                            DetectionTime = DateTimeOffset.UtcNow
                        };
                        this.eventQueue.Add(fileEvent, token);
                        context.Events.Add(fileEvent);
                        this.behaviors.ForEach(b => b.OnFileDetected(context, fileEvent), cancellationToken: token);

                        filesScanned++;
                        batchCount++;

                        while (filesScanned >= nextReportAtFiles && nextPercentage <= 100)
                        {
                            progress?.Report(new FileScanProgress
                            {
                                FilesScanned = filesScanned,
                                TotalFiles = estimatedTotalFiles,
                                ElapsedTime = DateTimeOffset.UtcNow - startTime,
                                PercentageComplete = nextPercentage
                            });
                            nextPercentage += options.ProgressIntervalPercentage;
                            nextReportAtFiles = (int)Math.Ceiling(nextPercentage * filesPerPercentage);
                        }

                        if (batchCount >= options.BatchSize)
                        {
                            if (options.DelayPerFile > TimeSpan.Zero)
                            {
                                await Task.Delay(options.DelayPerFile, token);
                            }
                            batchCount = 0;
                        }

                        if (options.MaxFilesToScan.HasValue && filesScanned >= options.MaxFilesToScan.Value)
                        {
                            break;
                        }
                    }
                }

                if (options.MaxFilesToScan.HasValue && filesScanned >= options.MaxFilesToScan.Value)
                {
                    break;
                }
            }
            continuationToken = result.Value.NextContinuationToken;
        } while (continuationToken != null && !token.IsCancellationRequested);

        if (!options.MaxFilesToScan.HasValue || filesScanned < options.MaxFilesToScan.Value)
        {
            foreach (var missingFile in presentFiles.Except(currentFiles.Keys))
            {
                var fileEvent = new FileEvent
                {
                    LocationName = this.options.LocationName,
                    FilePath = missingFile,
                    EventType = FileEventType.Deleted,
                    DetectionTime = DateTimeOffset.UtcNow
                };

                if (options.EventFilter.Contains(fileEvent.EventType))
                {
                    this.eventQueue.Add(fileEvent, token);
                    context.Events.Add(fileEvent);
                    this.behaviors.ForEach(b => b.OnFileDetected(context, fileEvent), cancellationToken: token);

                    filesScanned++;
                    batchCount++;

                    while (filesScanned >= nextReportAtFiles && nextPercentage <= 100)
                    {
                        progress?.Report(new FileScanProgress
                        {
                            FilesScanned = filesScanned,
                            TotalFiles = estimatedTotalFiles,
                            ElapsedTime = DateTimeOffset.UtcNow - startTime,
                            PercentageComplete = nextPercentage
                        });
                        nextPercentage += options.ProgressIntervalPercentage;
                        nextReportAtFiles = (int)Math.Ceiling(nextPercentage * filesPerPercentage);
                    }

                    if (batchCount >= options.BatchSize)
                    {
                        if (options.DelayPerFile > TimeSpan.Zero)
                        {
                            await Task.Delay(options.DelayPerFile, token);
                        }
                        batchCount = 0;
                    }

                    if (options.MaxFilesToScan.HasValue && filesScanned >= options.MaxFilesToScan.Value)
                    {
                        break;
                    }
                }
            }
        }

        context.EndTime = DateTimeOffset.UtcNow;
        this.behaviors.ForEach(b => b.OnScanCompleted(context, context.EndTime.Value - context.StartTime), cancellationToken: token);

        if (progress != null)
        {
            progress.Report(new FileScanProgress
            {
                FilesScanned = filesScanned,
                TotalFiles = filesScanned,
                ElapsedTime = context.EndTime.Value - startTime,
                PercentageComplete = 100
            });
        }

        if (options.WaitForProcessing && options.Timeout != TimeSpan.Zero)
        {
            await this.WaitForQueueEmptyAsync(options.Timeout);
        }
        else if (options.WaitForProcessing)
        {
            await this.WaitForQueueEmptyAsync(TimeSpan.FromMinutes(5));
        }

        return context;
    }

    private void OnInMemoryFileEvent(object sender, FileEventArgs e)
    {
        this.eventQueue.Add(e.Event);
    }

    private FileEventType? DetermineEventType(FileEvent lastEvent, FileMetadata current, string checksum)
    {
        if (lastEvent == null) return FileEventType.Added;
        if (lastEvent.EventType == FileEventType.Deleted) return FileEventType.Added;
        if (lastEvent.Checksum != checksum || lastEvent.LastModified != current.LastModified) return FileEventType.Changed;

        return FileEventType.Unchanged;
    }
}