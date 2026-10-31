// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using System.Text.Json;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;

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
    : IDocumentStoreProvider, IDocumentStoreRetentionProvider, IDocumentStoreBatchGetProvider, IDocumentStoreBulkWriteProvider where TContext : DbContext, IDocumentStoreContext
{
    private readonly DocumentStoreOptions options = options ?? new();

    // Per instance on purpose: EF binds a compiled query to the first provider it runs against, and the same
    // TContext type can be used with different database providers in one process.
    private readonly Func<TContext, string, string, string[], IAsyncEnumerable<StorageDocument>> loadTrackedQuery =
        EF.CompileAsyncQuery((TContext dbContext, string typeHash, string partitionKeyHash, string[] rowKeyHashes) =>
            dbContext.StorageDocuments
                .Where(x => x.TypeHash == typeHash && x.PartitionKeyHash == partitionKeyHash && rowKeyHashes.Contains(x.RowKeyHash)));
    private readonly Func<TContext, string, string, string, CancellationToken, Task<StorageDocument>> readQuery =
        EF.CompileAsyncQuery((TContext dbContext, string typeHash, string partitionKeyHash, string rowKeyHash, CancellationToken token) =>
            dbContext.StorageDocuments.AsNoTracking()
                .FirstOrDefault(x => x.TypeHash == typeHash && x.PartitionKeyHash == partitionKeyHash && x.RowKeyHash == rowKeyHash));

    private readonly Func<TContext, string, string, string[], IAsyncEnumerable<StorageDocument>> readManyQuery =
        EF.CompileAsyncQuery((TContext dbContext, string typeHash, string partitionKeyHash, string[] rowKeyHashes) =>
            dbContext.StorageDocuments.AsNoTracking()
                .Where(x => x.TypeHash == typeHash && x.PartitionKeyHash == partitionKeyHash && rowKeyHashes.Contains(x.RowKeyHash)));

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<StoredDocument>>> GetManyAsync(DocumentTypeIdentity type, IReadOnlyList<DocumentKey> keys, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            var found = new List<StoredDocument>(keys.Count);
            if (keys.Count == 0)
            {
                return Result<IReadOnlyList<StoredDocument>>.Success(found);
            }

            var batchSize = Math.Clamp(this.options.GetManyBatchSize, 1, 1000);
            var typeHash = HashHelper.ComputeSha256(type.Value);
            await using var scope = scopeFactory.CreateAsyncScope();
            var batches = keys.Distinct().GroupBy(x => x.PartitionKey)
                .SelectMany(partition => partition.Chunk(batchSize).Select(chunk => (PartitionKey: partition.Key, Chunk: chunk)))
                .ToArray();
            var parallelism = Math.Min(batches.Length, Math.Clamp(this.options.GetManyMaxParallelism, 1, 32));
            if (parallelism == 1)
            {
                var context = scope.ServiceProvider.GetRequiredService<TContext>();
                foreach (var batch in batches)
                {
                    await this.ReadBatchAsync(context, type, typeHash, batch.PartitionKey, batch.Chunk, visibilityCutoff, found, cancellationToken);
                }

                return Result<IReadOnlyList<StoredDocument>>.Success(found);
            }

            await Parallel.ForEachAsync(batches, new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = cancellationToken }, async (batch, token) =>
            {
                var partial = new List<StoredDocument>(batch.Chunk.Length);
                await using var batchScope = scopeFactory.CreateAsyncScope();
                var batchContext = batchScope.ServiceProvider.GetRequiredService<TContext>();
                await this.ReadBatchAsync(batchContext, type, typeHash, batch.PartitionKey, batch.Chunk, visibilityCutoff, partial, token);
                lock (found)
                {
                    found.AddRange(partial);
                }
            });

            return Result<IReadOnlyList<StoredDocument>>.Success(found);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<IReadOnlyList<StoredDocument>>.Failure(MapError(ex)); }
    }

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
            var entity = await this.readQuery(context, HashHelper.ComputeSha256(type.Value), HashHelper.ComputeSha256(key.PartitionKey), HashHelper.ComputeSha256(key.RowKey), cancellationToken);

            // Hash columns narrow the lookup, the plain values rule out a hash collision.
            return entity is null || entity.Type != type.Value || entity.PartitionKey != key.PartitionKey || entity.RowKey != key.RowKey || !Visible(entity, visibilityCutoff)
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

                ApplyWrite(entity, write, now);
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
    public async Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(DocumentTypeIdentity type, IReadOnlyList<StoredDocumentWrite> writes, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.UpsertManyInOneCommitAsync(type, writes, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // Nothing was committed. Contention or a constraint violation is resolved by the per-operation path, which
            // re-reads every row and keeps the retry and conflict semantics of single upserts.
            return await this.UpsertManySequentialAsync(type, writes, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(DocumentTypeIdentity type, IReadOnlyList<DocumentDelete> deletes, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.DeleteManyInOneCommitAsync(type, deletes, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return await this.DeleteManySequentialAsync(type, deletes, cancellationToken);
        }
    }

    private async Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyInOneCommitAsync(DocumentTypeIdentity type, IReadOnlyList<StoredDocumentWrite> writes, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var rows = await this.LoadTrackedAsync(context, type, writes.Select(x => x.Key), cancellationToken);
        var completed = new List<DocumentInfo>(writes.Count);
        DocumentKey? failedKey = null;
        IReadOnlyList<string> failureMessages = null;
        foreach (var write in writes)
        {
            rows.TryGetValue(write.Key, out var entity);
            IResultError error = null;
            if (write.Key.PartitionKey.Length > StorageDocument.MaximumKeyLength || write.Key.RowKey.Length > StorageDocument.MaximumKeyLength)
            {
                error = new DocumentStoreInvalidQueryError($"Entity Framework document keys cannot exceed {StorageDocument.MaximumKeyLength} characters.");
            }
            else if (write.Options.CreateOnly && entity is not null)
            {
                error = new DocumentStoreConflictError("A physical document already exists.");
            }
            else if (!Matches(entity, write.Options.IfMatchETag))
            {
                error = new DocumentStoreConflictError("The document ETag changed.");
            }

            if (error is not null)
            {
                failedKey = write.Key;
                failureMessages = Result<DocumentInfo>.Failure(error).Messages;
                break;
            }

            var now = DateTimeOffset.UtcNow;
            if (entity is null)
            {
                entity = Create(type, write.Key, now);
                context.StorageDocuments.Add(entity);
                rows[write.Key] = entity;
            }

            ApplyWrite(entity, write, now);
            completed.Add(ToInfo(entity));
        }

        if (completed.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return failedKey is null
            ? Result<DocumentBatchResult<DocumentInfo>>.Success(new() { Items = completed })
            : PartialResult(completed, failedKey.Value, failureMessages);
    }

    private async Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyInOneCommitAsync(DocumentTypeIdentity type, IReadOnlyList<DocumentDelete> deletes, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var rows = await this.LoadTrackedAsync(context, type, deletes.Select(x => x.Key), cancellationToken);
        var completed = new List<DocumentKey>(deletes.Count);
        DocumentKey? failedKey = null;
        IReadOnlyList<string> failureMessages = null;
        var removed = 0;
        foreach (var delete in deletes)
        {
            if (rows.TryGetValue(delete.Key, out var entity))
            {
                if (!Matches(entity, delete.Options?.IfMatchETag))
                {
                    failedKey = delete.Key;
                    failureMessages = Result.Failure(new DocumentStoreConflictError("The document ETag changed.")).Messages;
                    break;
                }

                context.StorageDocuments.Remove(entity);
                rows.Remove(delete.Key);
                removed++;
            }

            completed.Add(delete.Key);
        }

        if (removed > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return failedKey is null
            ? Result<DocumentBatchResult<DocumentKey>>.Success(new() { Items = completed })
            : PartialResult(completed, failedKey.Value, failureMessages);
    }

    private async Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManySequentialAsync(DocumentTypeIdentity type, IReadOnlyList<StoredDocumentWrite> writes, CancellationToken cancellationToken)
    {
        var completed = new List<DocumentInfo>(writes.Count);
        foreach (var write in writes)
        {
            var result = await this.UpsertAsync(type, write, cancellationToken);
            if (result.IsFailure)
            {
                return PartialResult(completed, write.Key, result.Messages);
            }

            completed.Add(result.Value);
        }

        return Result<DocumentBatchResult<DocumentInfo>>.Success(new() { Items = completed });
    }

    private async Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManySequentialAsync(DocumentTypeIdentity type, IReadOnlyList<DocumentDelete> deletes, CancellationToken cancellationToken)
    {
        var completed = new List<DocumentKey>(deletes.Count);
        foreach (var delete in deletes)
        {
            var result = await this.DeleteAsync(type, delete.Key, delete.Options, cancellationToken);
            if (result.IsFailure)
            {
                return PartialResult(completed, delete.Key, result.Messages);
            }

            completed.Add(delete.Key);
        }

        return Result<DocumentBatchResult<DocumentKey>>.Success(new() { Items = completed });
    }

    private async Task ReadBatchAsync(TContext context, DocumentTypeIdentity type, string typeHash, string partitionKey, DocumentKey[] chunk, DateTimeOffset visibilityCutoff, List<StoredDocument> found, CancellationToken cancellationToken)
    {
        var requested = chunk.ToDictionary(x => x.RowKey);
        var rowHashes = chunk.Select(x => HashHelper.ComputeSha256(x.RowKey)).ToArray();
        await foreach (var entity in this.readManyQuery(context, typeHash, HashHelper.ComputeSha256(partitionKey), rowHashes).WithCancellation(cancellationToken))
        {
            // Hash columns narrow the lookup, the plain values rule out a hash collision.
            if (entity.Type == type.Value && entity.PartitionKey == partitionKey && requested.ContainsKey(entity.RowKey) && Visible(entity, visibilityCutoff))
            {
                found.Add(Map(entity));
            }
        }
    }

    private async Task<Dictionary<DocumentKey, StorageDocument>> LoadTrackedAsync(TContext context, DocumentTypeIdentity type, IEnumerable<DocumentKey> keys, CancellationToken cancellationToken)
    {
        var rows = new Dictionary<DocumentKey, StorageDocument>();
        var batchSize = Math.Clamp(this.options.GetManyBatchSize, 1, 1000);
        var typeHash = HashHelper.ComputeSha256(type.Value);
        foreach (var partition in keys.Distinct().GroupBy(x => x.PartitionKey))
        {
            // Keys over the length limit are rejected per operation, they cannot exist in the table.
            var valid = partition.Where(x => x.PartitionKey.Length <= StorageDocument.MaximumKeyLength && x.RowKey.Length <= StorageDocument.MaximumKeyLength).ToArray();
            if (valid.Length == 0)
            {
                continue;
            }

            var partitionHash = HashHelper.ComputeSha256(partition.Key);
            foreach (var chunk in valid.Chunk(batchSize))
            {
                var requested = chunk.Select(x => x.RowKey).ToHashSet(StringComparer.Ordinal);
                var rowHashes = chunk.Select(x => HashHelper.ComputeSha256(x.RowKey)).ToArray();
                await foreach (var entity in this.loadTrackedQuery(context, typeHash, partitionHash, rowHashes).WithCancellation(cancellationToken))
                {
                    // Hash columns narrow the lookup, the plain values rule out a hash collision.
                    if (entity.Type == type.Value && entity.PartitionKey == partition.Key && requested.Contains(entity.RowKey))
                    {
                        rows[new DocumentKey(entity.PartitionKey, entity.RowKey)] = entity;
                    }
                }
            }
        }

        return rows;
    }

    private static Result<DocumentBatchResult<TItem>> PartialResult<TItem>(List<TItem> completed, DocumentKey failedKey, IEnumerable<string> messages) =>
        Result<DocumentBatchResult<TItem>>.Success(new()
        {
            Items = completed,
            FailedKey = failedKey,
            FailedKeys = [failedKey]
        }).WithMessages(messages);

    private static void ApplyWrite(StorageDocument entity, StoredDocumentWrite write, DateTimeOffset now)
    {
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
        if (query?.DocumentKey is not { } key) return rows;
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

    // The leading PartitionKey >= p bound is redundant but gives the optimizer a plain range seek on (TypeHash, PartitionKey, RowKey).
    private static IQueryable<StorageDocument> ApplyContinuation(IQueryable<StorageDocument> rows, DocumentKey? key) => key is null
        ? rows
        : rows.Where(x => string.Compare(x.PartitionKey, key.Value.PartitionKey) >= 0 && (string.Compare(x.PartitionKey, key.Value.PartitionKey) > 0 || string.Compare(x.RowKey, key.Value.RowKey) > 0));
    private static bool Visible(StorageDocument entity, DateTimeOffset cutoff) => entity.ExpiresAtUnixMilliseconds is null || entity.ExpiresAtUnixMilliseconds > cutoff.ToUnixTimeMilliseconds();
    private static bool Matches(StorageDocument entity, string etag) => string.IsNullOrWhiteSpace(etag) || (entity is not null && string.Equals(entity.ConcurrencyVersion.ToString("N"), etag, StringComparison.Ordinal));

    // The row id is left empty on purpose: EF generates it on add with the generator of the database provider.
    private static StorageDocument Create(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset now) => new()
    {
        Type = type.Value,
        TypeHash = HashHelper.ComputeSha256(type.Value),
        PartitionKey = key.PartitionKey,
        PartitionKeyHash = HashHelper.ComputeSha256(key.PartitionKey),
        RowKey = key.RowKey,
        RowKeyHash = HashHelper.ComputeSha256(key.RowKey),
        CreatedDate = now
    };

    // Every caller reads with AsNoTracking and discards the entity, so the content array can be handed out without a copy.
    private static StoredDocument Map(StorageDocument x) => new()
    {
        Key = new(x.PartitionKey, x.RowKey),
        Content = x.Content ?? [],
        ContentHash = x.ContentHash,
        StoredContentHash = x.StoredContentHash,
        ETag = x.ConcurrencyVersion.ToString("N"),
        CreatedAt = x.CreatedDate,
        LastModifiedAt = x.UpdatedDate ?? x.CreatedDate,
        ExpiresAt = FromUnixTimeMilliseconds(x.ExpiresAtUnixMilliseconds),
        Properties = new PropertyBag(x.Properties),
        TransformMetadata = new PropertyBag(x.TransformMetadata)
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
