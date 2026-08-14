// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

/// <summary>Persists serialized documents with one dependency-injection-owned Entity Framework scope per operation.</summary>
/// <typeparam name="TContext">The document DbContext type.</typeparam>
/// <param name="scopeFactory">The root scope factory used to create and own one scope and context per operation.</param>
/// <param name="options">Optional paging and stored-document size safety limits.</param>
/// <remarks>
/// The provider never captures a caller-owned DbContext. Every operation creates, uses, and disposes its own scope and
/// <typeparamref name="TContext" />, which makes singleton document clients safe. Entity mapping is defined by annotations
/// on <see cref="StorageDocument" /> and mutations use database transactions and concurrency versions where required.
/// </remarks>
/// <example><code>var provider = new EntityFrameworkDocumentStoreProvider&lt;AppDbContext&gt;(scopeFactory);</code></example>
public class EntityFrameworkDocumentStoreProvider<TContext>(IServiceScopeFactory scopeFactory, DocumentStoreOptions options = null)
    : IDocumentStoreProvider, IDocumentStoreRetentionProvider where TContext : DbContext, IDocumentStoreContext
{
    private readonly DocumentStoreOptions options = options ?? new();

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedServerSide,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedEfficiently,
        SupportsContinuationPaging = true,
        SupportsServerSideCount = true,
        SupportsKeyOnlyProjection = true,
        SupportsConditionalWrite = true,
        SupportsConditionalDelete = true,
        SupportsAtomicPropertyUpdate = true,
        SupportsLogicalExpiration = true,
        SupportsRetention = true
    };

    /// <inheritdoc />
    public async Task<Result<StoredDocument>> GetAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var entity = await Exact(context, type, key).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            return entity is null || !Visible(entity, visibilityCutoff)
                ? Result<StoredDocument>.Failure(new DocumentStoreNotFoundError())
                : Result<StoredDocument>.Success(Map(entity));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<StoredDocument>.Failure(MapError(ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<StoredDocumentPage>> FindPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var take = query?.Take ?? this.options.DefaultTake;
            var rows = ApplyContinuation(ApplyQuery(context, type, query, visibilityCutoff), ReadNativeToken(query?.ContinuationToken));
            var page = await rows.AsNoTracking().OrderBy(x => x.PartitionKey).ThenBy(x => x.RowKey).Take(take + 1).ToListAsync(cancellationToken);
            return Result<StoredDocumentPage>.Success(new()
            {
                Items = page.Take(take).Select(Map).ToArray(),
                ContinuationToken = page.Count > take ? CreateToken("find", type, query, visibilityCutoff, new(page[take - 1].PartitionKey, page[take - 1].RowKey)) : null
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<StoredDocumentPage>.Failure(MapError(ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentKeyPage>> ListPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var take = query?.Take ?? this.options.DefaultTake;
            var rows = ApplyContinuation(ApplyQuery(context, type, query, visibilityCutoff), ReadNativeToken(query?.ContinuationToken));
            var page = await rows.AsNoTracking().OrderBy(x => x.PartitionKey).ThenBy(x => x.RowKey).Select(x => new { x.PartitionKey, x.RowKey }).Take(take + 1).ToListAsync(cancellationToken);
            return Result<DocumentKeyPage>.Success(new()
            {
                Items = page.Take(take).Select(x => new DocumentKey(x.PartitionKey, x.RowKey)).ToArray(),
                ContinuationToken = page.Count > take ? CreateToken("list", type, query, visibilityCutoff, new(page[take - 1].PartitionKey, page[take - 1].RowKey)) : null
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentKeyPage>.Failure(MapError(ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<long>> CountAsync(DocumentTypeIdentity type, DocumentCountQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var pageQuery = new DocumentQuery { DocumentKey = query?.DocumentKey, Filter = query?.Filter ?? DocumentKeyFilter.FullMatch, AllowFullScan = query?.AllowFullScan ?? false };
            return Result<long>.Success(await ApplyQuery(context, type, pageQuery, visibilityCutoff).AsNoTracking().LongCountAsync(cancellationToken));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<long>.Failure(MapError(ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var cutoff = visibilityCutoff.ToUnixTimeMilliseconds();
            return Result<bool>.Success(await Exact(context, type, key).AsNoTracking().AnyAsync(x => x.ExpiresAtUnixMilliseconds == null || x.ExpiresAtUnixMilliseconds > cutoff, cancellationToken));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<bool>.Failure(MapError(ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpsertAsync(DocumentTypeIdentity type, StoredDocumentWrite write, CancellationToken cancellationToken = default)
    {
        if (write.Key.PartitionKey.Length > StorageDocument.MaximumKeyLength || write.Key.RowKey.Length > StorageDocument.MaximumKeyLength)
        {
            return Result<DocumentInfo>.Failure(new DocumentStoreInvalidQueryError($"Entity Framework document keys cannot exceed {StorageDocument.MaximumKeyLength} characters."));
        }

        var attempts = string.IsNullOrWhiteSpace(write.Options.IfMatchETag) && !write.Options.CreateOnly ? 4 : 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();
                var entity = await Exact(context, type, write.Key).SingleOrDefaultAsync(cancellationToken);
                if (write.Options.CreateOnly && entity is not null) return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("A physical document already exists."));
                if (!Matches(entity, write.Options.IfMatchETag)) return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document ETag changed."));

                var now = DateTimeOffset.UtcNow;
                if (entity is null)
                {
                    entity = Create(type, write.Key, now);
                    context.StorageDocuments.Add(entity);
                }

                entity.Content = write.Content.ToArray();
                entity.ContentHash = write.ContentHash;
                entity.StoredContentHash = write.StoredContentHash;
                if (write.Properties is not null)
                {
                    entity.Properties = write.Properties.Clone().ToDictionary(x => x.Key, x => x.Value);
                }

                entity.TransformMetadata = (write.TransformMetadata?.Clone() ?? new PropertyBag()).ToDictionary(x => x.Key, x => x.Value);
                entity.ExpiresAtUnixMilliseconds = write.PreserveExpiration
                    ? entity.ExpiresAtUnixMilliseconds
                    : ToUnixTimeMilliseconds(write.ExpiresAt);
                entity.UpdatedDate = now;
                entity.ConcurrencyVersion = Guid.NewGuid();
                await context.SaveChangesAsync(cancellationToken);
                return Result<DocumentInfo>.Success(ToInfo(entity));
            }
            catch (OperationCanceledException) { throw; }
            catch (DbUpdateException) when (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), cancellationToken);
            }
            catch (Exception ex) { return Result<DocumentInfo>.Failure(MapError(ex)); }
        }

        return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document changed during the write."));
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentTypeIdentity type, DocumentPropertiesUpdate update, DateTimeOffset? resolvedExpiresAt, bool preserveExpiration, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var entity = await Exact(context, type, update.Key).SingleOrDefaultAsync(cancellationToken);
            if (entity is null) return Result<DocumentInfo>.Failure(new DocumentStoreNotFoundError());
            if (!Matches(entity, update.IfMatchETag)) return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document ETag changed."));
            if (update.Properties is not null) entity.Properties = update.Properties.ToDictionary(x => x.Key, x => x.Value);
            if (!preserveExpiration) entity.ExpiresAtUnixMilliseconds = ToUnixTimeMilliseconds(resolvedExpiresAt);
            entity.UpdatedDate = DateTimeOffset.UtcNow;
            entity.ConcurrencyVersion = Guid.NewGuid();
            await context.SaveChangesAsync(cancellationToken);
            return Result<DocumentInfo>.Success(ToInfo(entity));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentInfo>.Failure(MapError(ex)); }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(DocumentTypeIdentity type, DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var entity = await Exact(context, type, key).SingleOrDefaultAsync(cancellationToken);
            if (entity is null) return Result.Success();
            if (!Matches(entity, options?.IfMatchETag)) return Result.Failure(new DocumentStoreConflictError("The document ETag changed."));
            context.StorageDocuments.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result.Failure(MapError(ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentRetentionSweepResult>> SweepExpiredAsync(DocumentRetentionSweepRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            var deleted = 0L;
            var deletedKeys = new List<DocumentKey>();
            var batches = 0;
            var hasMore = false;
            for (; batches < request.MaxBatches; batches++)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();
                var typeHash = HashHelper.ComputeSha256(request.DocumentType.Value);
                var cutoff = request.VisibilityCutoff.ToUnixTimeMilliseconds();
                var rows = await context.StorageDocuments
                    .Where(x => x.TypeHash == typeHash && x.Type == request.DocumentType.Value && x.ExpiresAtUnixMilliseconds != null && x.ExpiresAtUnixMilliseconds <= cutoff)
                    .OrderBy(x => x.ExpiresAtUnixMilliseconds)
                    .ThenBy(x => x.Id)
                    .Take(request.BatchSize)
                    .ToListAsync(cancellationToken);
                context.StorageDocuments.RemoveRange(rows);
                await context.SaveChangesAsync(cancellationToken);
                deleted += rows.Count;
                deletedKeys.AddRange(rows.Select(x => new DocumentKey(x.PartitionKey, x.RowKey)));
                hasMore = rows.Count == request.BatchSize;
                if (!hasMore)
                {
                    batches++;
                    break;
                }

                if (request.BatchDelay > TimeSpan.Zero)
                {
                    await Task.Delay(request.BatchDelay, cancellationToken);
                }
            }

            return Result<DocumentRetentionSweepResult>.Success(new()
            {
                DocumentType = request.DocumentType,
                DeletedCount = deleted,
                DeletedKeys = deletedKeys,
                BatchCount = batches,
                HasMore = hasMore
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentRetentionSweepResult>.Failure(MapError(ex)); }
    }

    private static IQueryable<StorageDocument> Exact(TContext context, DocumentTypeIdentity type, DocumentKey key)
    {
        var typeHash = HashHelper.ComputeSha256(type.Value);
        var partitionHash = HashHelper.ComputeSha256(key.PartitionKey);
        var rowHash = HashHelper.ComputeSha256(key.RowKey);
        return context.StorageDocuments.Where(x => x.TypeHash == typeHash && x.Type == type.Value && x.PartitionKeyHash == partitionHash && x.PartitionKey == key.PartitionKey && x.RowKeyHash == rowHash && x.RowKey == key.RowKey);
    }

    private static IQueryable<StorageDocument> ApplyQuery(TContext context, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset cutoff)
    {
        var typeHash = HashHelper.ComputeSha256(type.Value);
        var cutoffUnixMilliseconds = cutoff.ToUnixTimeMilliseconds();
        var rows = context.StorageDocuments.Where(x => x.TypeHash == typeHash && x.Type == type.Value && (x.ExpiresAtUnixMilliseconds == null || x.ExpiresAtUnixMilliseconds > cutoffUnixMilliseconds));
        if (query?.DocumentKey is not DocumentKey key) return rows;
        var partitionHash = HashHelper.ComputeSha256(key.PartitionKey);
        rows = rows.Where(x => x.PartitionKeyHash == partitionHash && x.PartitionKey == key.PartitionKey);
        return query.Filter switch
        {
            DocumentKeyFilter.FullMatch => rows.Where(x => x.RowKeyHash == HashHelper.ComputeSha256(key.RowKey) && x.RowKey == key.RowKey),
            DocumentKeyFilter.RowKeyPrefixMatch => rows.Where(x => x.RowKey.StartsWith(key.RowKey ?? string.Empty)),
            DocumentKeyFilter.RowKeySuffixMatch => rows.Where(x => x.RowKey.EndsWith(key.RowKey ?? string.Empty)),
            _ => rows.Where(_ => false)
        };
    }

    private static IQueryable<StorageDocument> ApplyContinuation(IQueryable<StorageDocument> rows, DocumentKey? key) => key is null ? rows : rows.Where(x => string.Compare(x.PartitionKey, key.Value.PartitionKey) > 0 || (x.PartitionKey == key.Value.PartitionKey && string.Compare(x.RowKey, key.Value.RowKey) > 0));
    private static bool Visible(StorageDocument entity, DateTimeOffset cutoff) => entity.ExpiresAtUnixMilliseconds is null || entity.ExpiresAtUnixMilliseconds > cutoff.ToUnixTimeMilliseconds();
    private static bool Matches(StorageDocument entity, string etag) => string.IsNullOrWhiteSpace(etag) || (entity is not null && string.Equals(entity.ConcurrencyVersion.ToString("N"), etag, StringComparison.Ordinal));

    private static StorageDocument Create(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), Type = type.Value, TypeHash = HashHelper.ComputeSha256(type.Value), PartitionKey = key.PartitionKey,
        PartitionKeyHash = HashHelper.ComputeSha256(key.PartitionKey), RowKey = key.RowKey, RowKeyHash = HashHelper.ComputeSha256(key.RowKey), CreatedDate = now
    };

    private static StoredDocument Map(StorageDocument x) => new()
    {
        Key = new(x.PartitionKey, x.RowKey), Content = x.Content?.ToArray() ?? [], ContentHash = x.ContentHash,
        StoredContentHash = x.StoredContentHash, ETag = x.ConcurrencyVersion.ToString("N"), CreatedAt = x.CreatedDate,
        LastModifiedAt = x.UpdatedDate ?? x.CreatedDate, ExpiresAt = FromUnixTimeMilliseconds(x.ExpiresAtUnixMilliseconds), Properties = new PropertyBag(x.Properties), TransformMetadata = new PropertyBag(x.TransformMetadata)
    };
    private static DocumentInfo ToInfo(StorageDocument x) => new() { Key = new(x.PartitionKey, x.RowKey), ETag = x.ConcurrencyVersion.ToString("N"), ContentHash = x.ContentHash, CreatedAt = x.CreatedDate, LastModifiedAt = x.UpdatedDate ?? x.CreatedDate, ExpiresAt = FromUnixTimeMilliseconds(x.ExpiresAtUnixMilliseconds), Properties = new PropertyBag(x.Properties) };
    private static long? ToUnixTimeMilliseconds(DateTimeOffset? value) => value?.ToUnixTimeMilliseconds();
    private static DateTimeOffset? FromUnixTimeMilliseconds(long? value) => value is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(value.Value);
    private static IResultError MapError(Exception ex) => ex is DbUpdateConcurrencyException ? new DocumentStoreConflictError("The document ETag changed.") : new DocumentStoreProviderError("Entity Framework document operation failed.", ex);

    private static string CreateToken(string operation, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, DocumentKey key)
    {
        var result = DocumentContinuationTokenSerializer.Serialize(new() { Provider = type.Value, QueryHash = DocumentQueryHash.Compute(operation, type, query, query?.Take ?? 100), VisibilityCutoff = visibilityCutoff, NativeToken = JsonSerializer.Serialize(key) });
        return result.IsSuccess ? result.Value : null;
    }
    private static DocumentKey? ReadNativeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var result = DocumentContinuationTokenSerializer.Deserialize(token);
        return result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value.NativeToken) ? JsonSerializer.Deserialize<DocumentKey>(result.Value.NativeToken) : null;
    }
}
