// File: BridgingIT.DevKit.Application.FileMonitoring/InMemoryFileEventStore.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// In-memory implementation of IFileEventStore for testing or simple applications.
/// Stores FileEvents in a ConcurrentDictionary, not persistent across restarts.
/// </summary>
public class InMemoryFileEventStore : IFileEventStore
{
    private readonly ConcurrentDictionary<string, List<FileEvent>> events = [];

    public Task<FileEvent> GetFileEventAsync(string filePath)
    {
        return Task.FromResult(this.events.TryGetValue(filePath, out var fileEvents)
            ? fileEvents.OrderByDescending(e => e.DetectionTime).FirstOrDefault()
            : null);
    }

    public Task<IEnumerable<FileEvent>> GetFileEventsAsync(string filePath)
    {
        return Task.FromResult(this.events.TryGetValue(filePath, out var fileEvents)
            ? fileEvents.OrderByDescending(e => e.DetectionTime).AsEnumerable()
            : null);
    }

    public Task<List<FileEvent>> GetFileEventsForLocationAsync(string locationName)
    {
        var result = this.events.Values.SelectMany(e => e).Where(e => e.LocationName == locationName).ToList();
        return Task.FromResult(result);
    }

    public Task<List<string>> GetPresentFilesAsync(string locationName)
    {
        var presentFiles = this.events
            .Where(kv => kv.Value.Any(e => e.LocationName == locationName))
            .Select(kv => new { FilePath = kv.Key, LatestEvent = kv.Value.OrderByDescending(e => e.DetectionTime).First() })
            .Where(x => x.LatestEvent.EventType != FileEventType.Deleted)
            .Select(x => x.FilePath)
            .ToList();
        return Task.FromResult(presentFiles);
    }

    public Task StoreEventAsync(FileEvent fileEvent)
    {
        this.events.AddOrUpdate(
            fileEvent.FilePath,
            _ => [fileEvent],
            (_, list) => { list.Add(fileEvent); return list; });
        return Task.CompletedTask;
    }

    public Task StoreProcessingResultAsync(ProcessingResult result)
    {
        // No-op for in-memory store; could be extended if needed
        return Task.CompletedTask;
    }
}