// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Common;

/// <summary>
/// Stores permalink registry entries in process memory for development and tests.
/// </summary>
/// <example>
/// <code>
/// var provider = new InMemoryStoragePermalinkRegistryProvider();
/// </code>
/// </example>
public sealed class InMemoryStoragePermalinkRegistryProvider(TimeProvider timeProvider = null) : IStoragePermalinkRegistryProvider
{
    private readonly object sync = new();
    private readonly Dictionary<StoragePermalinkId, StoredEntry> entries = [];
    private readonly Dictionary<string, StoragePermalinkId> activeLocations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> tombstones = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PrefixTombstone> prefixTombstones = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public string Name => "InMemory";

    /// <inheritdoc />
    public Task<Result<StoragePermalinkEntry>> GetByIdAsync(StoragePermalinkId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            return Task.FromResult(this.entries.TryGetValue(id, out var entry)
                ? Result<StoragePermalinkEntry>.Success(this.ToEntry(entry))
                : Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotFoundError()));
        }
    }

    /// <inheritdoc />
    public Task<Result<StoragePermalinkEntry>> GetByLocationAsync(StorageResourceLocation location, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            var hash = Validate(location).ComputeHash();
            return Task.FromResult(this.activeLocations.TryGetValue(hash, out var id) && this.entries.TryGetValue(id, out var entry)
                ? Result<StoragePermalinkEntry>.Success(this.ToEntry(entry))
                : Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotFoundError("No active permalink exists for the storage location.")));
        }
    }

    /// <inheritdoc />
    public Task<Result<StoragePermalinkEntry>> GetOrCreateAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, DateTimeOffset? occurredAt = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            location = Validate(location);
            var hash = location.ComputeHash();
            if (this.activeLocations.TryGetValue(hash, out var existingId) && this.entries.TryGetValue(existingId, out var existing))
            {
                return Task.FromResult(Result<StoragePermalinkEntry>.Success(this.ToEntry(existing)));
            }

            var timestamp = occurredAt ?? this.timeProvider.GetUtcNow();
            var deletedAt = this.LatestDeletion(location);
            if (occurredAt.HasValue && deletedAt.HasValue && timestamp <= deletedAt.Value)
            {
                return Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkConflictError("The storage change predates a deleted permalink mapping.")));
            }

            var stored = new StoredEntry
            {
                Id = StoragePermalinkId.New(),
                Location = location,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                StorageChangedAt = timestamp,
                ExpiresAt = options?.ExpiresAt,
                Version = Guid.NewGuid()
            };
            this.entries[stored.Id] = stored;
            this.activeLocations[hash] = stored.Id;
            return Task.FromResult(Result<StoragePermalinkEntry>.Success(this.ToEntry(stored)));
        }
    }

    /// <inheritdoc />
    public Task<Result<StoragePermalinkEntry>> MoveAsync(StorageResourceLocation source, StorageResourceLocation target, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            source = Validate(source);
            target = Validate(target);
            if (source.Kind != target.Kind || !string.Equals(source.RegistrationName, target.RegistrationName, StringComparison.Ordinal))
            {
                return Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkValidationError("Permalink-preserving moves require the same storage kind and registration.")));
            }

            var sourceHash = source.ComputeHash();
            var targetHash = target.ComputeHash();
            if (!this.activeLocations.TryGetValue(sourceHash, out var sourceId) || !this.entries.TryGetValue(sourceId, out var sourceEntry))
            {
                var deletedAt = this.LatestDeletion(source);
                if (deletedAt.HasValue && deletedAt.Value >= occurredAt)
                {
                    return Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkConflictError("The move predates the current source location state.")));
                }

                return this.GetOrCreateAsync(target, occurredAt: occurredAt, cancellationToken: cancellationToken);
            }
            if (sourceEntry.StorageChangedAt > occurredAt)
            {
                return Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkConflictError("The move predates the current source mapping.")));
            }

            if (this.activeLocations.TryGetValue(targetHash, out var targetId) && targetId != sourceId)
            {
                if (this.entries[targetId].StorageChangedAt > occurredAt)
                {
                    return Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkConflictError("The move predates the current target mapping.")));
                }

                this.Tombstone(targetId, occurredAt);
            }

            this.activeLocations.Remove(sourceHash);
            this.RecordTombstone(sourceHash, occurredAt);
            sourceEntry.Location = target;
            sourceEntry.UpdatedAt = occurredAt;
            sourceEntry.StorageChangedAt = occurredAt;
            sourceEntry.Version = Guid.NewGuid();
            this.activeLocations[targetHash] = sourceId;
            return Task.FromResult(Result<StoragePermalinkEntry>.Success(this.ToEntry(sourceEntry)));
        }
    }

    /// <inheritdoc />
    public Task<Result<long>> MovePrefixAsync(StorageResourceLocation sourcePrefix, StorageResourceLocation targetPrefix, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            sourcePrefix = Validate(sourcePrefix);
            targetPrefix = Validate(targetPrefix);
            if (sourcePrefix.Kind != StorageResourceKind.File || targetPrefix.Kind != StorageResourceKind.File || sourcePrefix.RegistrationName != targetPrefix.RegistrationName)
            {
                return Task.FromResult(Result<long>.Failure(new StoragePermalinkValidationError("Prefix moves are supported only within one File Storage registration.")));
            }

            var candidates = this.entries.Values.Where(x => x.DeletedAt is null && x.StorageChangedAt <= occurredAt && SamePrefix(x.Location, sourcePrefix)).ToArray();
            var targets = candidates.ToDictionary(
                x => x.Id,
                x => StorageResourceLocation.ForFile(targetPrefix.RegistrationName, Combine(targetPrefix.Path, x.Location.Path[sourcePrefix.Path.Length..].TrimStart('/'))));

            foreach (var entry in candidates)
            {
                var targetHash = targets[entry.Id].ComputeHash();
                if (this.activeLocations.TryGetValue(targetHash, out var collisionId) && collisionId != entry.Id && !targets.ContainsKey(collisionId) && this.entries[collisionId].StorageChangedAt > occurredAt)
                {
                    return Task.FromResult(Result<long>.Failure(new StoragePermalinkConflictError("The prefix move predates a target mapping.")));
                }
            }

            foreach (var entry in candidates)
            {
                var targetHash = targets[entry.Id].ComputeHash();
                if (this.activeLocations.TryGetValue(targetHash, out var collisionId) && collisionId != entry.Id && !targets.ContainsKey(collisionId))
                {
                    this.Tombstone(collisionId, occurredAt);
                }

                this.activeLocations.Remove(entry.Location.ComputeHash());
                this.RecordTombstone(entry.Location.ComputeHash(), occurredAt);
            }

            foreach (var entry in candidates)
            {
                var target = targets[entry.Id];
                entry.Location = target;
                entry.UpdatedAt = occurredAt;
                entry.StorageChangedAt = occurredAt;
                entry.Version = Guid.NewGuid();
                this.activeLocations[target.ComputeHash()] = entry.Id;
            }

            this.RecordPrefixTombstone(sourcePrefix, occurredAt);

            return Task.FromResult(Result<long>.Success(candidates.LongLength));
        }
    }

    /// <inheritdoc />
    public Task<Result> DeleteByLocationAsync(StorageResourceLocation location, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            location = Validate(location);
            var hash = location.ComputeHash();
            this.RecordTombstone(hash, occurredAt);
            if (this.activeLocations.TryGetValue(hash, out var id))
            {
                if (this.entries[id].StorageChangedAt <= occurredAt) this.Tombstone(id, occurredAt);
            }

            return Task.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public Task<Result<long>> DeletePrefixAsync(StorageResourceLocation prefix, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            prefix = Validate(prefix);
            var candidates = this.entries.Values.Where(x => x.DeletedAt is null && x.StorageChangedAt <= occurredAt && SamePrefix(x.Location, prefix)).ToArray();
            foreach (var entry in candidates)
            {
                this.Tombstone(entry.Id, occurredAt);
            }

            this.RecordPrefixTombstone(prefix, occurredAt);

            return Task.FromResult(Result<long>.Success(candidates.LongLength));
        }
    }

    /// <inheritdoc />
    public Task<Result<StoragePermalinkEntry>> UpdateExpirationAsync(StoragePermalinkId id, StoragePermalinkExpirationUpdate update, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            if (!this.entries.TryGetValue(id, out var entry) || entry.DeletedAt is not null)
            {
                return Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotFoundError()));
            }

            if (!Matches(entry, update?.IfMatchETag))
            {
                return Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkConflictError("The permalink changed after it was read.")));
            }

            entry.ExpiresAt = update?.ExpiresAt;
            entry.UpdatedAt = this.timeProvider.GetUtcNow();
            entry.Version = Guid.NewGuid();
            return Task.FromResult(Result<StoragePermalinkEntry>.Success(this.ToEntry(entry)));
        }
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(StoragePermalinkId id, StoragePermalinkDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            if (!this.entries.TryGetValue(id, out var entry) || entry.DeletedAt is not null)
            {
                return Task.FromResult(Result.Success());
            }

            if (!Matches(entry, options?.IfMatchETag))
            {
                return Task.FromResult(Result.Failure(new StoragePermalinkConflictError("The permalink changed after it was read.")));
            }

            this.Tombstone(id, this.timeProvider.GetUtcNow());
            return Task.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public Task<Result<StoragePermalinkPage>> ListPageAsync(StoragePermalinkQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        query ??= new();
        if (query.Take is <= 0 or > 500)
        {
            return Task.FromResult(Result<StoragePermalinkPage>.Failure(new StoragePermalinkValidationError("Permalink page size must be between 1 and 500.")));
        }

        lock (this.sync)
        {
            var skip = DecodeOffset(query.ContinuationToken);
            if (skip < 0)
            {
                return Task.FromResult(Result<StoragePermalinkPage>.Failure(new StoragePermalinkValidationError("The permalink continuation token is invalid.")));
            }

            var now = this.timeProvider.GetUtcNow();
            var items = this.entries.Values.Select(x => this.ToEntry(x, now)).Where(x => Matches(x, query)).OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToArray();
            var pageItems = items.Skip(skip).Take(query.Take).ToArray();
            var next = skip + pageItems.Length < items.Length ? Base64UrlHelper.Encode(BitConverter.GetBytes(skip + pageItems.Length)) : null;
            return Task.FromResult(Result<StoragePermalinkPage>.Success(new() { Items = pageItems, ContinuationToken = next }));
        }
    }

    private void Tombstone(StoragePermalinkId id, DateTimeOffset deletedAt)
    {
        if (!this.entries.TryGetValue(id, out var entry) || entry.DeletedAt is not null)
        {
            return;
        }

        var hash = entry.Location.ComputeHash();
        this.activeLocations.Remove(hash);
        this.RecordTombstone(hash, deletedAt);
        entry.DeletedAt = deletedAt;
        entry.UpdatedAt = deletedAt;
        entry.StorageChangedAt = deletedAt;
        entry.Version = Guid.NewGuid();
    }

    private StoragePermalinkEntry ToEntry(StoredEntry entry, DateTimeOffset? now = null) => new()
    {
        Id = entry.Id,
        Location = entry.Location,
        CreatedAt = entry.CreatedAt,
        UpdatedAt = entry.UpdatedAt,
        ExpiresAt = entry.ExpiresAt,
        DeletedAt = entry.DeletedAt,
        ETag = entry.Version.ToString("N"),
        Status = entry.DeletedAt is not null
            ? StoragePermalinkStatus.Deleted
            : entry.ExpiresAt is not null && entry.ExpiresAt <= (now ?? this.timeProvider.GetUtcNow())
                ? StoragePermalinkStatus.Expired
                : StoragePermalinkStatus.Active
    };

    private static bool Matches(StoredEntry entry, string etag) => string.IsNullOrWhiteSpace(etag) || string.Equals(entry.Version.ToString("N"), etag, StringComparison.Ordinal);
    private static bool SamePrefix(StorageResourceLocation location, StorageResourceLocation prefix) => location.Kind == prefix.Kind && location.RegistrationName == prefix.RegistrationName && (location.Path == prefix.Path || location.Path.StartsWith(prefix.Path.TrimEnd('/') + "/", StringComparison.Ordinal));
    private static string Combine(string left, string right) => string.IsNullOrEmpty(right) ? left : $"{left.TrimEnd('/')}/{right.TrimStart('/')}";
    private static StorageResourceLocation Validate(StorageResourceLocation location) => location ?? throw new ArgumentNullException(nameof(location));

    private DateTimeOffset? LatestDeletion(StorageResourceLocation location)
    {
        DateTimeOffset? latest = this.tombstones.TryGetValue(location.ComputeHash(), out var exact) ? exact : null;
        if (location.Kind != StorageResourceKind.File) return latest;
        foreach (var tombstone in this.prefixTombstones.Values.Where(x => SamePrefix(location, x.Location)))
        {
            if (!latest.HasValue || tombstone.OccurredAt > latest.Value) latest = tombstone.OccurredAt;
        }

        return latest;
    }

    private void RecordTombstone(string hash, DateTimeOffset occurredAt)
    {
        if (!this.tombstones.TryGetValue(hash, out var current) || occurredAt > current) this.tombstones[hash] = occurredAt;
    }

    private void RecordPrefixTombstone(StorageResourceLocation location, DateTimeOffset occurredAt)
    {
        var hash = location.ComputeHash();
        if (!this.prefixTombstones.TryGetValue(hash, out var current) || occurredAt > current.OccurredAt)
        {
            this.prefixTombstones[hash] = new(location, occurredAt);
        }
    }

    private static bool Matches(StoragePermalinkEntry entry, StoragePermalinkQuery query) =>
        (!query.Id.HasValue || entry.Id == query.Id.Value) &&
        (!query.Kind.HasValue || entry.Location.Kind == query.Kind.Value) &&
        (string.IsNullOrWhiteSpace(query.RegistrationName) || string.Equals(entry.Location.RegistrationName, query.RegistrationName.Trim(), StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(query.LocationContains) || entry.Location.ToCanonicalString().Contains(query.LocationContains, StringComparison.OrdinalIgnoreCase)) &&
        (query.Status.HasValue ? entry.Status == query.Status.Value : entry.Status != StoragePermalinkStatus.Deleted);

    private static int DecodeOffset(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 0;
        try
        {
            var bytes = Base64UrlHelper.Decode(token);
            return bytes.Length == sizeof(int) ? BitConverter.ToInt32(bytes) : -1;
        }
        catch (FormatException)
        {
            return -1;
        }
    }

    private sealed class StoredEntry
    {
        public StoragePermalinkId Id { get; init; }
        public StorageResourceLocation Location { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset StorageChangedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public Guid Version { get; set; }
    }

    private sealed record PrefixTombstone(StorageResourceLocation Location, DateTimeOffset OccurredAt);
}
