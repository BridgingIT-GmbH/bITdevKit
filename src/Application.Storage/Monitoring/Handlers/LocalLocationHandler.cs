// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class LocalLocationHandler : LocationHandlerBase
{
    private FileSystemWatcher fileSystemWatcher;
    private readonly Dictionary<string, (FileEvent Event, DateTimeOffset Timestamp)> eventBuffer = [];
    private readonly TimeSpan debounceInterval = TimeSpan.FromMilliseconds(50);

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

    public override async Task StartAsync(CancellationToken token = default)
    {
        await base.StartAsync(token);
        if (!this.options.UseOnDemandOnly && this.provider.SupportsNotifications)
        {
            await this.StartWatchingAsync(token);
        }
        if (!this.options.UseOnDemandOnly)
        {
            await this.ScanAsync(token);
        }
    }

    public override async Task PauseAsync(CancellationToken token = default)
    {
        if (this.fileSystemWatcher != null && !this.isPaused)
        {
            this.fileSystemWatcher.EnableRaisingEvents = false;
            await base.PauseAsync();
        }
    }

    public override async Task ResumeAsync(CancellationToken token = default)
    {
        if (this.fileSystemWatcher != null && this.isPaused)
        {
            this.fileSystemWatcher.EnableRaisingEvents = true;
            await base.ResumeAsync();
        }
    }

    public override async Task StopAsync(CancellationToken token = default)
    {
        if (this.fileSystemWatcher != null)
        {
            this.fileSystemWatcher.EnableRaisingEvents = false;
            this.fileSystemWatcher.Dispose();
            this.fileSystemWatcher = null;
            this.isPaused = false;
        }
        await base.StopAsync(token);
    }

    public override async Task<ScanContext> ScanAsync(bool waitForProcessing = false, TimeSpan timeout = default, CancellationToken token = default)
    {
        var context = new ScanContext { LocationName = this.options.LocationName };
        this.behaviors.ForEach(b => b.OnScanStarted(context), cancellationToken: token);

        var presentFiles = await this.store.GetPresentFilesAsync(this.options.LocationName);
        var currentFiles = new Dictionary<string, FileMetadata>();
        string continuationToken = null;

        do
        {
            var result = await this.provider.ListFilesAsync("/", this.options.FilePattern, true, continuationToken, token);
            foreach (var filePath in result.Value.Files)
            {
                var metadataResult = await this.provider.GetFileMetadataAsync(filePath, token);
                var checksumResult = await this.provider.GetChecksumAsync(filePath, token);
                if (metadataResult.IsSuccess && checksumResult.IsSuccess) // TODO: log failures
                {
                    var metadata = metadataResult.Value;
                    currentFiles[filePath] = metadata;
                    var lastEvent = await this.store.GetFileEventAsync(this.options.LocationName, filePath);
                    var eventType = this.DetermineEventType(lastEvent, metadata, checksumResult.Value);

                    if (eventType.HasValue)
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
                    }
                }
            }
            continuationToken = result.Value.NextContinuationToken;
        } while (continuationToken != null && !token.IsCancellationRequested);

        foreach (var missingFile in presentFiles.Except(currentFiles.Keys))
        {
            var fileEvent = new FileEvent
            {
                LocationName = this.options.LocationName,
                FilePath = missingFile,
                EventType = FileEventType.Deleted,
                DetectionTime = DateTimeOffset.UtcNow
            };
            this.eventQueue.Add(fileEvent, token);
            context.Events.Add(fileEvent);
            this.behaviors.ForEach(b => b.OnFileDetected(context, fileEvent), cancellationToken: token);
        }

        context.EndTime = DateTimeOffset.UtcNow;
        this.behaviors.ForEach(b => b.OnScanCompleted(context, context.EndTime.Value - context.StartTime), cancellationToken: token);

        if (waitForProcessing && timeout != TimeSpan.Zero)
        {
            await this.WaitForQueueEmptyAsync(timeout);
        }
        else if (waitForProcessing)
        {
            await this.WaitForQueueEmptyAsync(TimeSpan.FromMinutes(5)); // Default timeout if none specified
        }

        return context;
    }

    private async Task StartWatchingAsync(CancellationToken token)
    {
        var localProvider = (LocalFileStorageProvider)this.provider;
        this.fileSystemWatcher = new FileSystemWatcher
        {
            Path = localProvider.RootPath,
            Filter = this.options.FilePattern,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = true
        };
        this.fileSystemWatcher.Created += this.OnFileSystemEvent;
        this.fileSystemWatcher.Changed += this.OnFileSystemEvent;
        this.fileSystemWatcher.Deleted += this.OnFileSystemEvent;
        this.fileSystemWatcher.EnableRaisingEvents = true;
        _ = Task.Run(() => this.ProcessBufferedEvents(token));
        this.logger.LogInformation($"Real-time watching started for location: {this.options.LocationName}");
        await Task.CompletedTask;
    }

    private async void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
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
            LastModified = metadataResult.Value?.LastModified,
            Checksum = checksumResult.Value,
            DetectionTime = DateTimeOffset.UtcNow
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
                foreach (var (path, (fileEvent, timestamp)) in this.eventBuffer.ToList())
                {
                    if (DateTimeOffset.UtcNow - timestamp >= this.debounceInterval)
                    {
                        this.eventQueue.Add(fileEvent, token);
                        this.eventBuffer.Remove(path);
                    }
                }
            }
        }
    }

    private FileEventType? DetermineEventType(FileEvent lastEvent, FileMetadata current, string checksum)
    {
        if (lastEvent == null) return FileEventType.Added;
        if (lastEvent.EventType == FileEventType.Deleted) return FileEventType.Added;
        if (lastEvent.Checksum != checksum || lastEvent.LastModified != current.LastModified) return FileEventType.Changed;

        return null;
    }
}