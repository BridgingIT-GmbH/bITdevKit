// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.Logging;

internal class FileStorageLocationHandler(
    ILogger logger,
    IFileStorageProvider provider,
    IFileEventStore store,
    LocationOptions options,
    IServiceProvider serviceProvider,
    IEnumerable<IMonitoringBehavior> behaviors = null) : LocationHandlerBase(logger, provider, store, options, serviceProvider, behaviors)
{
    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);

        if (!this.options.UseOnDemandOnly && this.options.ScanOnStart)
        {
            await this.ScanAsync(null, null, cancellationToken);
        }
    }

    public override async Task<FileScanContext> ScanAsync(
        FileScanOptions options = null,
        IProgress<FileScanProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= FileScanOptions.Default;
        var startTime = DateTimeOffset.UtcNow;
        var context = new FileScanContext { LocationName = this.options.LocationName };
        this.behaviors.ForEach(b => b.OnScanStarted(context), cancellationToken: cancellationToken);

        var presentFiles = await this.store.GetPresentFilesAsync(this.options.LocationName, cancellationToken);
        var currentFiles = new Dictionary<string, FileMetadata>();
        string continuationToken = null;
        var filesScanned = 0;
        var batchCount = 0;

        do
        {
            var result = await this.provider.ListFilesAsync("/", this.options.FileFilter, true, continuationToken, cancellationToken);
            foreach (var filePath in result.Value.Files.Order().Where(f => this.ShouldProcessFile(options, f)))
            {
                var metadataResult = await this.provider.GetFileMetadataAsync(filePath, cancellationToken);
                var checksumResult = options.SkipChecksum
                    ? Result<string>.Success(string.Empty)
                    : await this.provider.GetChecksumAsync(filePath, cancellationToken);

                if (metadataResult.IsSuccess && checksumResult.IsSuccess)
                {
                    var metadata = metadataResult.Value;
                    currentFiles[filePath] = metadata;
                    var lastEvent = await this.store.GetFileEventAsync(this.options.LocationName, filePath, cancellationToken: cancellationToken);
                    var eventType = DetermineEventType(lastEvent, metadata, checksumResult.Value);

                    if (eventType.HasValue && options.EventFilter.Contains(eventType.Value))
                    {
                        var fileEvent = new FileEvent
                        {
                            ScanId = context.ScanId,
                            LocationName = this.options.LocationName,
                            FilePath = filePath,
                            EventType = eventType.Value,
                            FileSize = metadata.Length,
                            LastModifiedDate = metadata.LastModified,
                            Checksum = checksumResult.Value,
                            DetectedDate = DateTimeOffset.UtcNow
                        };

                        this.eventQueue.Add(fileEvent, cancellationToken);
                        context.Events.Add(fileEvent);
                        this.behaviors.ForEach(b => b.OnFileDetected(context, fileEvent), cancellationToken: cancellationToken);

                        filesScanned++;
                        batchCount++;

                        progress?.Report(new FileScanProgress
                        {
                            FilesScanned = filesScanned,
                            TotalFiles = filesScanned,
                            ElapsedTime = DateTimeOffset.UtcNow - startTime,
                            PercentageComplete = 0
                        });

                        if (batchCount >= options.BatchSize)
                        {
                            if (options.DelayPerFile > TimeSpan.Zero)
                            {
                                await Task.Delay(options.DelayPerFile, cancellationToken);
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
        } while (continuationToken != null && !cancellationToken.IsCancellationRequested);

        if (!options.MaxFilesToScan.HasValue || filesScanned < options.MaxFilesToScan.Value)
        {
            foreach (var missingFilePath in presentFiles.Except(currentFiles.Keys))
            {
                if (!this.ShouldProcessFile(options, missingFilePath))
                {
                    continue;
                }

                var fileEvent = new FileEvent
                {
                    ScanId = context.ScanId,
                    LocationName = this.options.LocationName,
                    FilePath = missingFilePath,
                    EventType = FileEventType.Deleted,
                    DetectedDate = DateTimeOffset.UtcNow
                };

                if (options.EventFilter.Contains(fileEvent.EventType))
                {
                    this.eventQueue.Add(fileEvent, cancellationToken);
                    context.Events.Add(fileEvent);
                    this.behaviors.ForEach(b => b.OnFileDetected(context, fileEvent), cancellationToken: cancellationToken);
                    filesScanned++;
                    batchCount++;

                    if (batchCount >= options.BatchSize)
                    {
                        if (options.DelayPerFile > TimeSpan.Zero)
                        {
                            await Task.Delay(options.DelayPerFile, cancellationToken);
                        }

                        batchCount = 0;
                    }
                }
            }
        }

        context.EndTime = DateTimeOffset.UtcNow;
        this.behaviors.ForEach(b => b.OnScanCompleted(context, context.EndTime.Value - context.StartTime), cancellationToken: cancellationToken);

        progress?.Report(new FileScanProgress
        {
            FilesScanned = filesScanned,
            TotalFiles = filesScanned,
            ElapsedTime = context.EndTime.Value - startTime,
            PercentageComplete = 100
        });

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

    private static FileEventType? DetermineEventType(FileEvent lastEvent, FileMetadata current, string checksum)
    {
        if (lastEvent == null)
        {
            return FileEventType.Added;
        }

        if (lastEvent.EventType == FileEventType.Deleted)
        {
            return FileEventType.Added;
        }

        if (lastEvent.Checksum != checksum || lastEvent.LastModifiedDate != current.LastModified)
        {
            return FileEventType.Changed;
        }

        return FileEventType.Unchanged;
    }
}
