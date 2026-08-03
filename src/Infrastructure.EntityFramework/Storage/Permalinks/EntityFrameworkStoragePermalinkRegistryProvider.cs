// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using System.Text;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Persists Storage Permalink Registry entries through a provider-owned Entity Framework context scope.
/// </summary>
/// <typeparam name="TContext">
/// The registered context implementing <see cref="IStoragePermalinkRegistryContext" />.
/// </typeparam>
/// <example>
/// <code>
/// services.AddStoragePermalinks().UseEntityFramework&lt;AppDbContext&gt;();
/// </code>
/// </example>
public sealed class EntityFrameworkStoragePermalinkRegistryProvider<TContext>(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider = null) : IStoragePermalinkRegistryProvider
    where TContext : DbContext, IStoragePermalinkRegistryContext
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public string Name => "EntityFramework";

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> GetByIdAsync(StoragePermalinkId id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var row = await Context(scope).StoragePermalinks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value && !x.IsSynchronizationTombstone, cancellationToken);
            return row is null ? NotFound() : Result<StoragePermalinkEntry>.Success(this.Map(row));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Failure<StoragePermalinkEntry>("Could not read the permalink.", ex); }
    }

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> GetByLocationAsync(StorageResourceLocation location, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var row = await FindActiveAsync(Context(scope), location, true, cancellationToken);
            return row is null ? NotFound("No active permalink exists for the storage location.") : Result<StoragePermalinkEntry>.Success(this.Map(row));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Failure<StoragePermalinkEntry>("Could not read the permalink location.", ex); }
    }

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> GetOrCreateAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, DateTimeOffset? occurredAt = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = Context(scope);
            var existing = await FindActiveAsync(context, location, false, cancellationToken);
            if (existing is not null) return Result<StoragePermalinkEntry>.Success(this.Map(existing));

            var timestamp = occurredAt ?? this.timeProvider.GetUtcNow();
            var latestDeletion = await LatestDeletionAsync(context, location, cancellationToken);
            if (occurredAt.HasValue && latestDeletion.HasValue && timestamp <= latestDeletion.Value)
            {
                return Result<StoragePermalinkEntry>.Failure(new StoragePermalinkConflictError("The storage change predates a deleted permalink mapping."));
            }

            var row = Create(location, options, timestamp);
            context.StoragePermalinks.Add(row);
            await context.SaveChangesAsync(cancellationToken);
            return Result<StoragePermalinkEntry>.Success(this.Map(row));
        }
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateException ex)
        {
            var existing = await this.GetByLocationAsync(location, cancellationToken);
            return existing.IsSuccess ? existing : Failure<StoragePermalinkEntry>("Could not create the permalink because its location changed concurrently.", ex);
        }
        catch (Exception ex) { return Failure<StoragePermalinkEntry>("Could not create the permalink.", ex); }
    }

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> MoveAsync(StorageResourceLocation source, StorageResourceLocation target, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        if (source.Kind != target.Kind || source.RegistrationName != target.RegistrationName)
            return Result<StoragePermalinkEntry>.Failure(new StoragePermalinkValidationError("Permalink-preserving moves require the same storage kind and registration."));
        if (source == target) return await this.GetOrCreateAsync(target, occurredAt: occurredAt, cancellationToken: cancellationToken);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = Context(scope);
            var sourceRow = await FindActiveAsync(context, source, false, cancellationToken);
            if (sourceRow is null)
            {
                var deletedAt = await LatestDeletionAsync(context, source, cancellationToken);
                return deletedAt.HasValue && deletedAt.Value >= occurredAt
                    ? Result<StoragePermalinkEntry>.Failure(new StoragePermalinkConflictError("The move predates the current source location state."))
                    : await this.GetOrCreateAsync(target, occurredAt: occurredAt, cancellationToken: cancellationToken);
            }
            if (sourceRow.StorageChangedAt > occurredAt) return Result<StoragePermalinkEntry>.Failure(new StoragePermalinkConflictError("The move predates the current source mapping."));

            var targetRow = await FindActiveAsync(context, target, false, cancellationToken);
            if (targetRow is not null && targetRow.StorageChangedAt > occurredAt) return Result<StoragePermalinkEntry>.Failure(new StoragePermalinkConflictError("The move predates the current target mapping."));
            await using var transaction = await BeginTransactionAsync(context, cancellationToken);
            if (targetRow is not null && targetRow.Id != sourceRow.Id) Tombstone(targetRow, occurredAt);
            if (targetRow is not null) await context.SaveChangesAsync(cancellationToken);
            context.StoragePermalinks.Add(CreateTombstone(source, occurredAt));
            ApplyLocation(sourceRow, target, occurredAt);
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result<StoragePermalinkEntry>.Success(this.Map(sourceRow));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Failure<StoragePermalinkEntry>("Could not move the permalink mapping.", ex); }
    }

    /// <inheritdoc />
    public async Task<Result<long>> MovePrefixAsync(StorageResourceLocation sourcePrefix, StorageResourceLocation targetPrefix, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        if (sourcePrefix.Kind != StorageResourceKind.File || targetPrefix.Kind != StorageResourceKind.File || sourcePrefix.RegistrationName != targetPrefix.RegistrationName)
            return Result<long>.Failure(new StoragePermalinkValidationError("Prefix moves are supported only within one File Storage registration."));
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = Context(scope);
            await using var transaction = await BeginTransactionAsync(context, cancellationToken);
            var rows = await context.StoragePermalinks.Where(x => !x.IsSynchronizationTombstone && x.StorageKind == (int)StorageResourceKind.File && x.RegistrationName == sourcePrefix.RegistrationName && x.DeletedAt == null).ToListAsync(cancellationToken);
            var candidates = rows.Where(x => IsWithinPrefix(x.Path, sourcePrefix.Path) && x.StorageChangedAt <= occurredAt).ToArray();
            var candidateIds = candidates.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var byHash = rows.ToDictionary(x => x.LocationHash, StringComparer.Ordinal);
            foreach (var row in candidates)
            {
                var relative = row.Path[sourcePrefix.Path.Length..].TrimStart('/');
                var target = StorageResourceLocation.ForFile(targetPrefix.RegistrationName, Combine(targetPrefix.Path, relative));
                if (byHash.TryGetValue(target.ComputeHash(), out var collision) && collision.Id != row.Id && !candidateIds.Contains(collision.Id))
                {
                    if (collision.StorageChangedAt > occurredAt) return Result<long>.Failure(new StoragePermalinkConflictError("The prefix move predates a target mapping."));
                    Tombstone(collision, occurredAt);
                }
            }
            if (rows.Any(x => x.DeletedAt is not null)) await context.SaveChangesAsync(cancellationToken);
            foreach (var row in candidates)
            {
                var relative = row.Path[sourcePrefix.Path.Length..].TrimStart('/');
                var target = StorageResourceLocation.ForFile(targetPrefix.RegistrationName, Combine(targetPrefix.Path, relative));
                context.StoragePermalinks.Add(CreateTombstone(new() { Kind = StorageResourceKind.File, RegistrationName = row.RegistrationName, Scope = row.Scope, Path = row.Path }, occurredAt));
                ApplyLocation(row, target, occurredAt);
            }
            context.StoragePermalinks.Add(CreateTombstone(sourcePrefix, occurredAt, isPrefix: true));
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result<long>.Success(candidates.LongLength);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Failure<long>("Could not move the permalink prefix.", ex); }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteByLocationAsync(StorageResourceLocation location, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = Context(scope);
            var row = await FindActiveAsync(context, location, false, cancellationToken);
            if (row is not null && row.StorageChangedAt > occurredAt) return Result.Success();
            if (row is not null)
            {
                Tombstone(row, occurredAt);
            }
            else
            {
                context.Entry(CreateTombstone(location, occurredAt)).State = EntityState.Added;
            }
            var changes = await context.SaveChangesAsync(cancellationToken);
            if (changes == 0) return Result.Failure(new StoragePermalinkProviderError("The permalink deletion synchronization state was not persisted."));
            return Result.Success();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result.Failure(new StoragePermalinkProviderError("Could not delete the permalink location.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<long>> DeletePrefixAsync(StorageResourceLocation prefix, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = Context(scope);
            var rows = await context.StoragePermalinks.Where(x => !x.IsSynchronizationTombstone && x.StorageKind == (int)prefix.Kind && x.RegistrationName == prefix.RegistrationName && x.DeletedAt == null).ToListAsync(cancellationToken);
            var candidates = rows.Where(x => IsWithinPrefix(x.Path, prefix.Path) && x.StorageChangedAt <= occurredAt).ToArray();
            foreach (var row in candidates) Tombstone(row, occurredAt);
            context.StoragePermalinks.Add(CreateTombstone(prefix, occurredAt, isPrefix: true));
            await context.SaveChangesAsync(cancellationToken);
            return Result<long>.Success(candidates.LongLength);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Failure<long>("Could not delete the permalink prefix.", ex); }
    }

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> UpdateExpirationAsync(StoragePermalinkId id, StoragePermalinkExpirationUpdate update, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = Context(scope);
            var row = await context.StoragePermalinks.SingleOrDefaultAsync(x => !x.IsSynchronizationTombstone && x.Id == id.Value && x.DeletedAt == null, cancellationToken);
            if (row is null) return NotFound();
            if (!Matches(row, update?.IfMatchETag)) return Conflict<StoragePermalinkEntry>();
            row.ExpiresAt = update?.ExpiresAt;
            row.UpdatedAt = this.timeProvider.GetUtcNow();
            row.ConcurrencyVersion = Guid.NewGuid();
            await context.SaveChangesAsync(cancellationToken);
            return Result<StoragePermalinkEntry>.Success(this.Map(row));
        }
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateConcurrencyException) { return Conflict<StoragePermalinkEntry>(); }
        catch (Exception ex) { return Failure<StoragePermalinkEntry>("Could not update permalink expiration.", ex); }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(StoragePermalinkId id, StoragePermalinkDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = Context(scope);
            var row = await context.StoragePermalinks.SingleOrDefaultAsync(x => !x.IsSynchronizationTombstone && x.Id == id.Value && x.DeletedAt == null, cancellationToken);
            if (row is null) return Result.Success();
            if (!Matches(row, options?.IfMatchETag)) return Result.Failure(new StoragePermalinkConflictError("The permalink changed after it was read."));
            Tombstone(row, this.timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateConcurrencyException) { return Result.Failure(new StoragePermalinkConflictError("The permalink changed after it was read.")); }
        catch (Exception ex) { return Result.Failure(new StoragePermalinkProviderError("Could not delete the permalink.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkPage>> ListPageAsync(StoragePermalinkQuery query, CancellationToken cancellationToken = default)
    {
        query ??= new();
        if (query.Take is <= 0 or > 500) return Result<StoragePermalinkPage>.Failure(new StoragePermalinkValidationError("Permalink page size must be between 1 and 500."));
        if (!TryDecodeCursor(query.ContinuationToken, out var cursor)) return Result<StoragePermalinkPage>.Failure(new StoragePermalinkValidationError("The permalink continuation token is invalid."));
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = Context(scope);
            var now = this.timeProvider.GetUtcNow();
            IQueryable<StoragePermalink> rows = context.StoragePermalinks.AsNoTracking().Where(x => !x.IsSynchronizationTombstone);
            if (query.Id.HasValue) rows = rows.Where(x => x.Id == query.Id.Value.Value);
            if (query.Kind.HasValue) rows = rows.Where(x => x.StorageKind == (int)query.Kind.Value);
            if (!string.IsNullOrWhiteSpace(query.RegistrationName)) { var name = query.RegistrationName.Trim().ToLowerInvariant(); rows = rows.Where(x => x.RegistrationName == name); }
            if (!string.IsNullOrWhiteSpace(query.LocationContains)) { var value = query.LocationContains.Trim(); rows = rows.Where(x => x.RegistrationName.Contains(value) || x.Scope.Contains(value) || x.Path.Contains(value)); }
            rows = query.Status switch
            {
                StoragePermalinkStatus.Deleted => rows.Where(x => x.DeletedAt != null),
                StoragePermalinkStatus.Expired => rows.Where(x => x.DeletedAt == null && x.ExpiresAt != null && x.ExpiresAt <= now),
                StoragePermalinkStatus.Active => rows.Where(x => x.DeletedAt == null && (x.ExpiresAt == null || x.ExpiresAt > now)),
                _ => rows.Where(x => x.DeletedAt == null)
            };
            if (!string.IsNullOrEmpty(cursor)) rows = rows.Where(x => string.Compare(x.Id, cursor) > 0);
            var pageRows = await rows.OrderBy(x => x.Id).Take(query.Take + 1).ToListAsync(cancellationToken);
            var hasMore = pageRows.Count > query.Take;
            var items = pageRows.Take(query.Take).Select(this.Map).ToArray();
            return Result<StoragePermalinkPage>.Success(new() { Items = items, ContinuationToken = hasMore ? EncodeCursor(items[^1].Id.Value) : null });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Failure<StoragePermalinkPage>("Could not list permalinks.", ex); }
    }

    private static TContext Context(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<TContext>();
    private static async Task<StoragePermalink> FindActiveAsync(TContext context, StorageResourceLocation location, bool noTracking, CancellationToken cancellationToken)
    {
        var hash = location.ComputeHash();
        var query = context.StoragePermalinks.Where(x => x.ActiveLocationHash == hash && x.DeletedAt == null && x.StorageKind == (int)location.Kind && x.RegistrationName == location.RegistrationName && x.Scope == location.Scope && x.Path == location.Path);
        return await (noTracking ? query.AsNoTracking() : query).SingleOrDefaultAsync(cancellationToken);
    }
    private static StoragePermalink Create(StorageResourceLocation location, StoragePermalinkCreateOptions options, DateTimeOffset timestamp) => new() { Id = StoragePermalinkId.New().Value, StorageKind = (int)location.Kind, RegistrationName = location.RegistrationName, Scope = location.Scope, Path = location.Path, LocationHash = location.ComputeHash(), ActiveLocationHash = location.ComputeHash(), CreatedAt = timestamp, UpdatedAt = timestamp, StorageChangedAt = timestamp, ExpiresAt = options?.ExpiresAt, ConcurrencyVersion = Guid.NewGuid() };
    private static StoragePermalink CreateTombstone(StorageResourceLocation location, DateTimeOffset timestamp, bool isPrefix = false)
    {
        var id = StoragePermalinkId.New().Value;
        return new() { Id = id, StorageKind = (int)location.Kind, RegistrationName = location.RegistrationName, Scope = location.Scope, Path = location.Path, LocationHash = location.ComputeHash(), ActiveLocationHash = $"deleted:{id}", CreatedAt = timestamp, UpdatedAt = timestamp, StorageChangedAt = timestamp, IsSynchronizationTombstone = true, IsPrefixTombstone = isPrefix, ConcurrencyVersion = Guid.NewGuid() };
    }
    private static void ApplyLocation(StoragePermalink row, StorageResourceLocation location, DateTimeOffset timestamp) { row.StorageKind = (int)location.Kind; row.RegistrationName = location.RegistrationName; row.Scope = location.Scope; row.Path = location.Path; row.LocationHash = location.ComputeHash(); row.ActiveLocationHash = row.LocationHash; row.UpdatedAt = timestamp; row.StorageChangedAt = timestamp; row.ConcurrencyVersion = Guid.NewGuid(); }
    private static void Tombstone(StoragePermalink row, DateTimeOffset timestamp) { row.DeletedAt = timestamp; row.UpdatedAt = timestamp; row.StorageChangedAt = timestamp; row.ActiveLocationHash = $"deleted:{row.Id}"; row.ConcurrencyVersion = Guid.NewGuid(); }
    private StoragePermalinkEntry Map(StoragePermalink row) => new() { Id = new(row.Id), Location = new() { Kind = (StorageResourceKind)row.StorageKind, RegistrationName = row.RegistrationName, Scope = row.Scope, Path = row.Path }, CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt, ExpiresAt = row.ExpiresAt, DeletedAt = row.DeletedAt, ETag = row.ConcurrencyVersion.ToString("N"), Status = row.DeletedAt.HasValue ? StoragePermalinkStatus.Deleted : row.ExpiresAt.HasValue && row.ExpiresAt <= this.timeProvider.GetUtcNow() ? StoragePermalinkStatus.Expired : StoragePermalinkStatus.Active };
    private static bool Matches(StoragePermalink row, string etag) => string.IsNullOrWhiteSpace(etag) || row.ConcurrencyVersion.ToString("N") == etag;
    private static bool IsWithinPrefix(string path, string prefix) => path == prefix || path.StartsWith(prefix.TrimEnd('/') + "/", StringComparison.Ordinal);
    private static async Task<DateTimeOffset?> LatestDeletionAsync(TContext context, StorageResourceLocation location, CancellationToken cancellationToken)
    {
        var hash = location.ComputeHash();
        var exactCandidates = await context.StoragePermalinks.AsNoTracking()
            .Where(x => x.LocationHash == hash && (x.IsSynchronizationTombstone || x.DeletedAt != null))
            .Select(x => x.IsSynchronizationTombstone ? (DateTimeOffset?)x.StorageChangedAt : x.DeletedAt)
            .ToListAsync(cancellationToken);
        var exact = exactCandidates.OrderByDescending(x => x).FirstOrDefault();
        if (location.Kind != StorageResourceKind.File) return exact;
        var prefixes = await context.StoragePermalinks.AsNoTracking().Where(x => x.IsSynchronizationTombstone && x.IsPrefixTombstone && x.StorageKind == (int)location.Kind && x.RegistrationName == location.RegistrationName).Select(x => new { x.Path, OccurredAt = x.StorageChangedAt }).ToListAsync(cancellationToken);
        var prefix = prefixes.Where(x => IsWithinPrefix(location.Path, x.Path)).OrderByDescending(x => x.OccurredAt).Select(x => (DateTimeOffset?)x.OccurredAt).FirstOrDefault();
        return !exact.HasValue || prefix > exact ? prefix : exact;
    }
    private static async Task<IDbContextTransaction> BeginTransactionAsync(TContext context, CancellationToken cancellationToken) =>
        context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
    private static string Combine(string left, string right) => string.IsNullOrEmpty(right) ? left : $"{left.TrimEnd('/')}/{right.TrimStart('/')}";
    private static string EncodeCursor(string value) => Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(value));
    private static bool TryDecodeCursor(string token, out string value) { value = null; if (string.IsNullOrWhiteSpace(token)) return true; try { value = Encoding.UTF8.GetString(Base64UrlHelper.Decode(token)); return StoragePermalinkId.TryParse(value, out _); } catch (FormatException) { return false; } }
    private static Result<StoragePermalinkEntry> NotFound(string message = null) => Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotFoundError(message));
    private static Result<T> Conflict<T>() => Result<T>.Failure(new StoragePermalinkConflictError("The permalink changed after it was read."));
    private static Result<T> Failure<T>(string message, Exception exception) => Result<T>.Failure(new StoragePermalinkProviderError(message, exception));
}
