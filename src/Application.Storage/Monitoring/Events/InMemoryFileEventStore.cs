// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// In-memory implementation of IFileEventStore for testing or simple applications.
/// Stores FileEvents in a ConcurrentDictionary with ConcurrentBag, not persistent across restarts.
/// Thread-safe for concurrent access and modifications.
/// </summary>
public class InMemoryFileEventStore : IFileEventStore
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<FileEvent>> events = [];

    /// <summary>
    /// Gets the most recent FileEvent for a specific file path (not location-scoped).
    /// Use GetFileEventAsync(string locationName, string filePath) for location-specific queries.
    /// </summary>
    public Task<FileEvent> GetFileEventAsync(string filePath, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.events.TryGetValue(filePath, out var fileEvents)
            ? fileEvents
                .WhereIf(e => e.DetectedDate >= fromDate, fromDate != null)
                .WhereIf(e => e.DetectedDate <= tillDate, tillDate != null)
                .OrderByDescending(e => e.DetectedDate).FirstOrDefault()
            : null);
    }

    /// <summary>
    /// Gets the most recent FileEvent for a specific file path within a location.
    /// </summary>
    public Task<FileEvent> GetFileEventAsync(string locationName, string filePath, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.events.TryGetValue(filePath, out var fileEvents)
            ? fileEvents.Where(e => e.LocationName == locationName)
                        .WhereIf(e => e.DetectedDate >= fromDate, fromDate != null)
                        .WhereIf(e => e.DetectedDate <= tillDate, tillDate != null)
                        .OrderByDescending(e => e.DetectedDate)
                        .FirstOrDefault()
            : null);
    }

    /// <summary>
    /// Gets all FileEvents for a specific file path across all locations.
    /// </summary>
    public Task<IEnumerable<FileEvent>> GetFileEventsAsync(string filePath, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.events.TryGetValue(filePath, out var fileEvents)
            ? fileEvents
                .WhereIf(e => e.DetectedDate >= fromDate, fromDate != null)
                .WhereIf(e => e.DetectedDate <= tillDate, tillDate != null)
                .OrderByDescending(e => e.DetectedDate).AsEnumerable()
            : []);
    }

    /// <summary>
    /// Gets all FileEvents for a specific location.
    /// </summary>
    public Task<List<FileEvent>> GetFileEventsForLocationAsync(string locationName, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default)
    {
        var result = this.events.Values.SelectMany(e => e)
            .Where(e => e.LocationName == locationName)
            .WhereIf(e => e.DetectedDate >= fromDate, fromDate != null)
            .WhereIf(e => e.DetectedDate <= tillDate, tillDate != null)
            .OrderByDescending(e => e.DetectedDate).ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// Gets the list of file paths currently present (not deleted) in a specific location.
    /// </summary>
    public Task<List<string>> GetPresentFilesAsync(string locationName, CancellationToken cancellationToken = default)
    {
        var presentFiles = this.events
            .Select(kv => new
            {
                FilePath = kv.Key,
                LatestEvent = kv.Value
                    .Where(e => e.LocationName == locationName)
                    .OrderByDescending(e => e.DetectedDate).FirstOrDefault()
            })
            .Where(x => x.LatestEvent != null && x.LatestEvent.EventType != FileEventType.Deleted)
            .Select(x => x.FilePath).ToList();

        return Task.FromResult(presentFiles);
    }

    /// <summary>
    /// Stores a FileEvent in a thread-safe manner.
    /// </summary>
    public Task StoreEventAsync(FileEvent fileEvent, CancellationToken cancellationToken = default)
    {
        this.events.AddOrUpdate(
            fileEvent.FilePath,
            _ => [fileEvent],
            (_, bag) =>
            {
                bag.Add(fileEvent);
                return bag;
            });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stores a ProcessingResult (no-op for in-memory store; could be extended).
    /// </summary>
    public Task StoreProcessingResultAsync(FileProcessingResult result, CancellationToken cancellationToken = default)
    {
        // No-op for in-memory store; could add concurrent storage if needed
        return Task.CompletedTask;
    }
}