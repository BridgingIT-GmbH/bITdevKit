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
            this.logger.LogInformation($"Real-time watching started for in-memory location: {this.options.LocationName}");
        }
        if (!this.options.UseOnDemandOnly)
        {
            await this.ScanAsync(null, token);
        }
    }

    public override async Task PauseAsync(CancellationToken token = default)
    {
        await base.PauseAsync(token);
        if (!this.isPaused)
        {
            this.inMemoryProvider.OnFileEvent -= this.OnInMemoryFileEvent;
            this.isPaused = true;
            this.logger.LogInformation($"Paused real-time watching for in-memory location: {this.options.LocationName}");
        }
    }

    public override async Task ResumeAsync(CancellationToken token = default)
    {
        await base.ResumeAsync(token);
        if (this.isPaused)
        {
            this.inMemoryProvider.OnFileEvent += this.OnInMemoryFileEvent;
            this.isPaused = false;
            this.logger.LogInformation($"Resumed real-time watching for in-memory location: {this.options.LocationName}");
        }
    }

    public override async Task StopAsync(CancellationToken token = default)
    {
        this.inMemoryProvider.OnFileEvent -= this.OnInMemoryFileEvent;
        this.isPaused = false;
        await base.StopAsync(token);
    }

    public override async Task<ScanContext> ScanAsync(bool waitForProcessing = false, TimeSpan timeout = default, IProgress<ScanProgress> progress = null, CancellationToken token = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var context = new ScanContext { LocationName = this.options.LocationName };
        this.behaviors.ForEach(b => b.OnScanStarted(context), cancellationToken: token);

        var presentFiles = await this.store.GetPresentFilesAsync(this.options.LocationName); // based on past events
        var currentFiles = new Dictionary<string, FileMetadata>();
        string continuationToken = null;
        var filesScanned = 0;
        var lastReportedPercentage = 0;
        var initialFiles = (await this.inMemoryProvider.ListFilesAsync("/", this.options.FilePattern, true, null, token)).Value.Files.ToList();
        var estimatedTotalFiles = initialFiles.Count;

        do
        {
            var result = await this.provider.ListFilesAsync("/", this.options.FilePattern, true, continuationToken, token);
            foreach (var filePath in result.Value.Files)
            {
                var metadataResult = await this.provider.GetFileMetadataAsync(filePath, token);
                var checksumResult = await this.provider.GetChecksumAsync(filePath, token);
                if (metadataResult.IsSuccess && checksumResult.IsSuccess)
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

                        filesScanned++;
                        int currentPercentage = (int)((double)filesScanned / estimatedTotalFiles * 100);
                        if (currentPercentage > lastReportedPercentage && currentPercentage % 10 == 0) // Report at 10% intervals
                        {
                            lastReportedPercentage = currentPercentage;
                            progress?.Report(new ScanProgress
                            {
                                FilesScanned = filesScanned,
                                TotalFiles = estimatedTotalFiles,
                                ElapsedTime = DateTimeOffset.UtcNow - startTime
                            });
                        }
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

            filesScanned++;
            int currentPercentage = (int)((double)filesScanned / estimatedTotalFiles * 100);
            if (currentPercentage > lastReportedPercentage && currentPercentage % 10 == 0)
            {
                lastReportedPercentage = currentPercentage;
                progress?.Report(new ScanProgress
                {
                    FilesScanned = filesScanned,
                    TotalFiles = estimatedTotalFiles,
                    ElapsedTime = DateTimeOffset.UtcNow - startTime
                });
            }
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

        int finalPercentage = 100;
        if (finalPercentage > lastReportedPercentage)
        {
            progress?.Report(new ScanProgress
            {
                FilesScanned = filesScanned,
                TotalFiles = filesScanned, // Exact count
                ElapsedTime = context.EndTime.Value - startTime
            });
        }

        return context;
    }

    private void OnInMemoryFileEvent(object sender, FileEventArgs e)
    {
        this.eventQueue.Add(e.FileEvent);
    }

    private FileEventType? DetermineEventType(FileEvent lastEvent, FileMetadata current, string checksum)
    {
        if (lastEvent == null) return FileEventType.Added;
        if (lastEvent.EventType == FileEventType.Deleted) return FileEventType.Added;
        if (lastEvent.Checksum != checksum || lastEvent.LastModified != current.LastModified) return FileEventType.Changed;

        return null;
    }
}