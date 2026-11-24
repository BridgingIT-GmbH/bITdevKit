// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using System.Text.Json;
using System.Text;

/// <summary>Persists monitoring events, processor outcomes, and location-scoped file presence atomically.</summary>
/// <param name="database">The selected native database and collection namespace.</param>
/// <example><code>services.AddFileMonitoring(_ => { }).WithLiteDbStore();</code></example>
public sealed class LiteDbFileEventStore(LiteDbFeatureDatabase database) : IFileEventStore
{
    private readonly LiteDbKeyedCollection<Guid, LiteDbFileEventRecord> events = database.Collection<Guid, LiteDbFileEventRecord>("events");
    private readonly LiteDbKeyedCollection<Guid, LiteDbFileProcessingRecord> results = database.Collection<Guid, LiteDbFileProcessingRecord>("results");
    private readonly LiteDbKeyedCollection<string, LiteDbFilePresenceRecord> presence = database.Collection<string, LiteDbFilePresenceRecord>("presence");
    private readonly LiteDbKeyedCollection<string, LiteDbFileEventState> state = database.Collection<string, LiteDbFileEventState>("state");

    /// <inheritdoc />
    public Task<FileEvent> GetFileEventAsync(string filePath, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default) =>
        this.LatestAsync(null, filePath, fromDate, tillDate, cancellationToken);

    /// <inheritdoc />
    public Task<FileEvent> GetFileEventAsync(string locationName, string filePath, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default) =>
        this.LatestAsync(locationName, filePath, fromDate, tillDate, cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<FileEvent>> GetFileEventsAsync(string filePath, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default) =>
        await this.SelectAsync(null, filePath, fromDate, tillDate, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<List<FileEvent>> GetFileEventsForLocationAsync(string locationName, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default) =>
        this.SelectAsync(locationName, null, fromDate, tillDate, cancellationToken);

    /// <inheritdoc />
    public Task<List<string>> GetPresentFilesAsync(string locationName, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
        Task.FromResult(database.Query<LiteDbFilePresenceRecord>("presence", BsonExpression.Create("$.LocationKey = @0 AND $.Present = true", HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(locationName))),
            database.Options.MaxMaterializedRows, "$.Path", failOnOverflow: true).Select(row => row.Path).Order(StringComparer.Ordinal).ToList()), cancellationToken);

    /// <inheritdoc />
    public Task StoreEventAsync(FileEvent fileEvent, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        ArgumentNullException.ThrowIfNull(fileEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileEvent.LocationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileEvent.FilePath);
        if (fileEvent.Id == Guid.Empty) { throw new ArgumentException("A monitoring event identity is required.", nameof(fileEvent)); }

        var hash = HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(database.Options.Serializer.SerializeToString(fileEvent)));
        if (this.events.TryGetValue(fileEvent.Id, out var existing))
        {
            if (existing.Hash != hash) { throw new LiteDbProviderException(new ConflictError("The monitoring event identity already has different content.")); }

            return Task.CompletedTask;
        }

        var control = this.state.GetOrAdd("current", _ => new());
        var row = new LiteDbFileEventRecord { Event = fileEvent, Hash = hash, Sequence = checked(++control.Sequence) };
        this.events.Add(fileEvent.Id, row);
        var key = JsonSerializer.Serialize(new[] { fileEvent.LocationName, fileEvent.FilePath });
        if (!this.presence.TryGetValue(key, out var latest) || StringComparer.Ordinal.Compare(row.SortKey, latest.SortKey) > 0)
        {
            this.presence[key] = new() { Location = fileEvent.LocationName, Path = fileEvent.FilePath, SortKey = row.SortKey, Present = fileEvent.EventType != FileEventType.Deleted };
        }

        return Task.CompletedTask;
    }, cancellationToken);

    /// <inheritdoc />
    public Task StoreProcessingResultAsync(FileProcessingResult result, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Id == Guid.Empty || !this.events.ContainsKey(result.FileEventId)) { throw new LiteDbProviderException(new NotFoundError("The processing result must reference a stored monitoring event.")); }

        var hash = HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(database.Options.Serializer.SerializeToString(result)));
        if (this.results.TryGetValue(result.Id, out var existing))
        {
            if (existing.Hash != hash) { throw new LiteDbProviderException(new ConflictError("The processing result identity already has different content.")); }
        }
        else { this.results.Add(result.Id, new() { Result = result, Hash = hash }); }

        return Task.CompletedTask;
    }, cancellationToken);

    /// <summary>Reads persisted processor outcomes for one monitoring event.</summary>
    /// <param name="eventId">The event identity.</param>
    /// <param name="cancellationToken">Cancels native selection.</param>
    /// <returns>Complete detached outcomes in processing-time order.</returns>
    public Task<IReadOnlyList<FileProcessingResult>> GetProcessingResultsAsync(Guid eventId, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
        Task.FromResult<IReadOnlyList<FileProcessingResult>>(database.Query<LiteDbFileProcessingRecord>("results", BsonExpression.Create("$.EventId = @0", eventId),
            database.Options.MaxMaterializedRows, failOnOverflow: true).Select(row => row.Result).ToArray()), cancellationToken);

    private Task<FileEvent> LatestAsync(string location, string path, DateTimeOffset? from, DateTimeOffset? until, CancellationToken cancellationToken) => database.ExecuteAsync(() =>
        Task.FromResult(database.Query<LiteDbFileEventRecord>("events", Predicate(location, path, from, until), 1, "$.SortKey", descending: true).FirstOrDefault()?.Event), cancellationToken);

    private Task<List<FileEvent>> SelectAsync(string location, string path, DateTimeOffset? from, DateTimeOffset? until, CancellationToken cancellationToken) => database.ExecuteAsync(() =>
        Task.FromResult(database.Query<LiteDbFileEventRecord>("events", Predicate(location, path, from, until), database.Options.MaxMaterializedRows,
            "$.SortKey", descending: true, failOnOverflow: true).Select(row => row.Event).ToList()), cancellationToken);

    private static BsonExpression Predicate(string location, string path, DateTimeOffset? from, DateTimeOffset? until)
    {
        var clauses = new List<string>();
        var values = new List<BsonValue>();
        if (location is not null) { Add("LocationKey", "=", HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(location))); }

        if (path is not null) { Add("PathKey", "=", HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(path))); }

        if (from.HasValue) { Add("DetectedTicks", ">=", from.Value.UtcTicks); }

        if (until.HasValue) { Add("DetectedTicks", "<=", until.Value.UtcTicks); }

        return clauses.Count == 0 ? null : BsonExpression.Create(string.Join(" AND ", clauses), values.ToArray());

        void Add(string field, string comparison, BsonValue value)
        {
            values.Add(value);
            clauses.Add($"$.{field} {comparison} @{values.Count - 1}");
        }
    }
}
