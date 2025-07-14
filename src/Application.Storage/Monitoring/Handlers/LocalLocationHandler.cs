// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class LocalLocationHandler : LocationHandlerBase
{
    private FileSystemWatcher fileSystemWatcher;
    private readonly Dictionary<string, (FileEvent Event, DateTimeOffset Timestamp)> eventBuffer = [];
    private readonly TimeSpan debounceInterval = TimeSpan.FromMilliseconds(200);

    public LocalLocationHandler(
        ILogger logger,
        IFileStorageProvider provider,
        IFileEventStore store,
        LocationOptions options,
        IServiceProvider serviceProvider,
        IEnumerable<IMonitoringBehavior> behaviors = null)
        : base(logger, provider, store, options, serviceProvider, behaviors)
    {
        if (provider is not LocalFileStorageProvider)
        {
            throw new ArgumentException("LocalLocationHandler requires LocalFileStorageProvider.");
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);

        if (!this.options.UseOnDemandOnly && this.provider.SupportsNotifications)
        {
            await this.StartWatchingAsync(cancellationToken);
        }

        if (!this.options.UseOnDemandOnly && this.options.ScanOnStart)
        {
            await this.ScanAsync(null, null, cancellationToken);
        }
    }

    public override async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        if (this.fileSystemWatcher != null && !this.isPaused)
        {
            this.fileSystemWatcher.EnableRaisingEvents = false;
            await base.PauseAsync(cancellationToken);
        }
    }

    public override async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (this.fileSystemWatcher != null && this.isPaused)
        {
            this.fileSystemWatcher.EnableRaisingEvents = true;
            await base.ResumeAsync(cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (this.fileSystemWatcher != null)
        {
            this.fileSystemWatcher.EnableRaisingEvents = false;
            this.fileSystemWatcher.Dispose();
            this.fileSystemWatcher = null;
            this.isPaused = false;
        }
        await base.StopAsync(cancellationToken);
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

        var estimatedTotalFiles = 0;
        if (this.provider is LocalFileStorageProvider localProvider)
        {
            // TODO: maybe just use the provider capabilities here and not rely on the file system directly (perf?)
            var directoryExists = Directory.Exists(localProvider.RootPath);
            if (options.ThrowIfDirectoryNotExists && !directoryExists)
            {
                throw new DirectoryNotFoundException($"The {this.options.LocationName} directory {localProvider.RootPath} does not exist.");
            }

            estimatedTotalFiles = directoryExists
                ? Directory.GetFiles(localProvider.RootPath, this.options.FileFilter, SearchOption.AllDirectories).Length
                : 0;
        }
        if (options.MaxFilesToScan < estimatedTotalFiles)
        {
            estimatedTotalFiles = options.MaxFilesToScan.Value;
        }

        var filesPerPercentage = estimatedTotalFiles / 100.0;
        var nextPercentage = options.ProgressIntervalPercentage;
        var nextReportAtFiles = (int)Math.Ceiling(nextPercentage * filesPerPercentage);

        do
        {
            var result = await this.provider.ListFilesAsync("/", this.options.FileFilter, true, continuationToken, cancellationToken);
            foreach (var filePath in result.Value.Files.Order()
                .Where(f => this.ShouldProcessFile(options, f)))
            {
                var metadataResult = await this.provider.GetFileMetadataAsync(filePath, cancellationToken);
                var checksumResult = options.SkipChecksum
                    ? Result<string>.Success(string.Empty)
                    : await this.provider.GetChecksumAsync(filePath, cancellationToken);

                if (metadataResult.IsSuccess && checksumResult.IsSuccess) // TODO: log failures
                {
                    var metadata = metadataResult.Value;
                    currentFiles[filePath] = metadata;
                    var lastEvent = await this.store.GetFileEventAsync(this.options.LocationName, filePath, cancellationToken: cancellationToken);
                    var eventType = this.DetermineEventType(lastEvent, metadata, checksumResult.Value);

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
                            DetectedDate = DateTimeOffset.UtcNow,
                            Checksum = checksumResult.Value
                            //Properties = ["provider" : this.provider]
                        };

                        this.eventQueue.Add(fileEvent, cancellationToken);
                        context.Events.Add(fileEvent);
                        this.behaviors.ForEach(b => b.OnFileDetected(context, fileEvent), cancellationToken: cancellationToken);

                        filesScanned++;
                        batchCount++;

                        while (filesScanned >= nextReportAtFiles && nextPercentage <= 100)
                        {
                            progress?.Report(new FileScanProgress
                            {
                                FilesScanned = filesScanned,
                                TotalFiles = estimatedTotalFiles,
                                ElapsedTime = DateTimeOffset.UtcNow - startTime,
                                PercentageComplete = nextPercentage // Use nextPercentage directly
                            });
                            nextPercentage += options.ProgressIntervalPercentage;
                            nextReportAtFiles = (int)Math.Ceiling(nextPercentage * filesPerPercentage);
                        }

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
        }

        context.EndTime = DateTimeOffset.UtcNow;
        this.behaviors.ForEach(b => b.OnScanCompleted(context, context.EndTime.Value - context.StartTime), cancellationToken: cancellationToken);

        progress?.Report(new FileScanProgress
        {
            FilesScanned = filesScanned,
            TotalFiles = filesScanned,
            ElapsedTime = context.EndTime.Value - startTime,
            PercentageComplete = 100 // Final report always at 100%
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

    private async Task StartWatchingAsync(CancellationToken cancellationToken)
    {
        var localProvider = (LocalFileStorageProvider)this.provider;
        this.fileSystemWatcher = new FileSystemWatcher
        {
            Path = localProvider.RootPath,
            Filter = "*.*", //this.options.FileFilter,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = true
        };
        this.fileSystemWatcher.Created += this.OnFileSystemEvent;
        this.fileSystemWatcher.Changed += this.OnFileSystemEvent;
        this.fileSystemWatcher.Deleted += this.OnFileSystemEvent;
        this.fileSystemWatcher.EnableRaisingEvents = true;
        _ = Task.Run(() => this.ProcessBufferedEvents(cancellationToken), cancellationToken);

        this.logger.LogInformation($"Real-time watching started for location: {this.options.LocationName}");

        await Task.CompletedTask;
    }

    private async void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (!this.options.FileFilter.IsNullOrEmpty() && !e.FullPath.Match(this.options.FileFilter))
        {
            return;
        }

        if (this.options.FileBlackListFilter.SafeAny() && e.FullPath.MatchAny(this.options.FileBlackListFilter))
        {
            return;
        }

        var localProvider = (LocalFileStorageProvider)this.provider;
        var relativePath = Path.GetRelativePath(localProvider.RootPath, e.FullPath);
        var metadataResult = e.ChangeType != WatcherChangeTypes.Deleted ? await this.provider.GetFileMetadataAsync(relativePath, CancellationToken.None) : Result<FileMetadata>.Success();
        var checksumResult = e.ChangeType != WatcherChangeTypes.Deleted ? await this.provider.GetChecksumAsync(relativePath, CancellationToken.None) : Result<string>.Success(string.Empty);

        if (!metadataResult || !checksumResult)
        {
            this.logger.LogWarning($"Failed to get metadata or checksum for file: {relativePath}");
            return;
        }

        var fileEvent = new FileEvent
        {
            LocationName = this.options.LocationName,
            FilePath = relativePath,
            EventType = e.ChangeType switch
            {
                WatcherChangeTypes.Created => FileEventType.Added,
                WatcherChangeTypes.Changed => FileEventType.Changed,
                WatcherChangeTypes.Deleted => FileEventType.Deleted,
                _ => FileEventType.Changed
            },
            FileSize = metadataResult.Value?.Length,
            LastModifiedDate = metadataResult.Value?.LastModified,
            Checksum = checksumResult.Value,
            DetectedDate = DateTimeOffset.UtcNow
        };

        lock (this.eventBuffer)
        {
            if (this.eventBuffer.TryGetValue(relativePath, out var buffered))
            {
                // Debounce Added + Changed or Changed + Changed within the interval, keeping the first event
                if (((buffered.Event.EventType == FileEventType.Added && fileEvent.EventType == FileEventType.Changed) ||
                     (buffered.Event.EventType == FileEventType.Changed && fileEvent.EventType == FileEventType.Changed)) &&
                    (DateTimeOffset.UtcNow - buffered.Timestamp) < this.debounceInterval)
                {
                    // Update the buffered event's timestamp but keep the first event
                    this.eventBuffer[relativePath] = (buffered.Event, DateTimeOffset.UtcNow);
                    return;
                }
                // Otherwise, enqueue the buffered event and update with the new one
                this.eventQueue.Add(buffered.Event);
            }
            this.eventBuffer[relativePath] = (fileEvent, DateTimeOffset.UtcNow);
        }
    }

    private async Task ProcessBufferedEvents(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(this.debounceInterval, token);

            lock (this.eventBuffer)
            {
                foreach (var (filePath, (fileEvent, timestamp)) in this.eventBuffer.ToList())
                {
                    if (!this.options.FileFilter.IsNullOrEmpty() && !filePath.Match(this.options.FileFilter))
                    {
                        continue;
                    }

                    if (this.options.FileBlackListFilter.SafeAny() && filePath.MatchAny(this.options.FileBlackListFilter))
                    {
                        continue;
                    }

                    if (DateTimeOffset.UtcNow - timestamp >= this.debounceInterval)
                    {
                        this.eventQueue.Add(fileEvent, token);
                        this.eventBuffer.Remove(filePath);
                    }
                }
            }
        }
    }

    private FileEventType? DetermineEventType(FileEvent lastEvent, FileMetadata current, string checksum)
    {
        if (lastEvent == null) return FileEventType.Added;
        if (lastEvent.EventType == FileEventType.Deleted) return FileEventType.Added;
        if (lastEvent.Checksum != checksum || lastEvent.LastModifiedDate != current.LastModified) return FileEventType.Changed;

        return FileEventType.Unchanged;
    }
}