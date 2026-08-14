// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text.Json;

/// <summary>
/// Stores copied serialized documents in process memory with atomic conditional mutations and bounded retention.
/// </summary>
/// <param name="loggerFactory">The optional logger factory used to create the typed provider logger.</param>
/// <remarks>
/// State is local to the provider instance and is not shared across application processes. All synchronization and backing
/// collections remain private; returned bytes, records, and property bags are cloned so caller mutation cannot alter stored
/// state. The provider is useful for tests and single-process scenarios, not distributed persistence.
/// </remarks>
/// <example><code>var provider = new InMemoryDocumentStoreProvider(loggerFactory);</code></example>
public class InMemoryDocumentStoreProvider(ILoggerFactory loggerFactory = null) : IDocumentStoreProvider, IDocumentStoreRetentionProvider
{
    private readonly object syncRoot = new();
    private readonly Dictionary<(string Type, string Partition, string Row), StoredDocument> documents = [];

    /// <summary>Gets the typed provider logger used for non-sensitive operational diagnostics.</summary>
    /// <example><code>protected ILogger ProviderLogger =&gt; this.Logger;</code></example>
    protected ILogger<InMemoryDocumentStoreProvider> Logger { get; } =
        loggerFactory?.CreateLogger<InMemoryDocumentStoreProvider>() ?? NullLogger<InMemoryDocumentStoreProvider>.Instance;

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedEfficiently,
        FullScan = DocumentQuerySupport.SupportedEfficiently,
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
    public Task<Result<StoredDocument>> GetAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.syncRoot)
        {
            return Task.FromResult(this.documents.TryGetValue(ToKey(type, key), out var document) && IsVisible(document, visibilityCutoff)
                ? Result<StoredDocument>.Success(Clone(document))
                : Result<StoredDocument>.Failure(new DocumentStoreNotFoundError($"Document '{key.PartitionKey}/{key.RowKey}' was not found.")));
        }
    }

    /// <inheritdoc />
    public Task<Result<StoredDocumentPage>> FindPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.syncRoot)
        {
            var rows = this.Query(type, query?.DocumentKey, query?.Filter ?? DocumentKeyFilter.FullMatch, visibilityCutoff);
            rows = ApplyContinuation(rows, ReadNativeToken(query?.ContinuationToken));
            var take = query?.Take ?? 100;
            var page = rows.Take(take + 1).ToArray();
            return Task.FromResult(Result<StoredDocumentPage>.Success(new()
            {
                Items = page.Take(take).Select(Clone).ToArray(),
                ContinuationToken = page.Length > take ? CreateToken("find", type, query, visibilityCutoff, page[take - 1].Key) : null
            }));
        }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentKeyPage>> ListPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.syncRoot)
        {
            var rows = this.Query(type, query?.DocumentKey, query?.Filter ?? DocumentKeyFilter.FullMatch, visibilityCutoff);
            rows = ApplyContinuation(rows, ReadNativeToken(query?.ContinuationToken));
            var take = query?.Take ?? 100;
            var page = rows.Take(take + 1).ToArray();
            return Result<DocumentKeyPage>.Success(new()
            {
                Items = page.Take(take).Select(x => x.Key).ToArray(),
                ContinuationToken = page.Length > take ? CreateToken("list", type, query, visibilityCutoff, page[take - 1].Key) : null
            });
        }
    }

    /// <inheritdoc />
    public Task<Result<long>> CountAsync(DocumentTypeIdentity type, DocumentCountQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.syncRoot)
        {
            var count = this.Query(type, query?.DocumentKey, query?.Filter ?? DocumentKeyFilter.FullMatch, visibilityCutoff).LongCount();
            return Task.FromResult(Result<long>.Success(count));
        }
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.syncRoot)
        {
            return Task.FromResult(Result<bool>.Success(
                this.documents.TryGetValue(ToKey(type, key), out var document) && IsVisible(document, visibilityCutoff)));
        }
    }

    /// <inheritdoc />
    public Task<Result<DocumentInfo>> UpsertAsync(DocumentTypeIdentity type, StoredDocumentWrite write, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.syncRoot)
        {
            var storageKey = ToKey(type, write.Key);
            this.documents.TryGetValue(storageKey, out var current);
            if (write.Options.CreateOnly && current is not null)
            {
                return Task.FromResult(Result<DocumentInfo>.Failure(new DocumentStoreConflictError("A physical document already exists.")));
            }

            if (!string.IsNullOrWhiteSpace(write.Options.IfMatchETag) &&
                (current is null || !string.Equals(current.ETag, write.Options.IfMatchETag, StringComparison.Ordinal)))
            {
                return Task.FromResult(Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document ETag changed.")));
            }

            var now = DateTimeOffset.UtcNow;
            var stored = new StoredDocument
            {
                Key = write.Key,
                Content = write.Content.ToArray(),
                ContentHash = write.ContentHash,
                StoredContentHash = write.StoredContentHash,
                ETag = Guid.NewGuid().ToString("N"),
                CreatedAt = current?.CreatedAt ?? now,
                LastModifiedAt = now,
                ExpiresAt = write.PreserveExpiration ? current?.ExpiresAt : write.ExpiresAt,
                Properties = write.Properties?.Clone() ?? current?.Properties?.Clone() ?? new PropertyBag(),
                TransformMetadata = write.TransformMetadata?.Clone() ?? new PropertyBag()
            };
            this.documents[storageKey] = stored;
            return Task.FromResult(Result<DocumentInfo>.Success(ToInfo(stored)));
        }
    }

    /// <inheritdoc />
    public Task<Result<DocumentInfo>> UpdatePropertiesAsync(
        DocumentTypeIdentity type,
        DocumentPropertiesUpdate update,
        DateTimeOffset? resolvedExpiresAt,
        bool preserveExpiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.syncRoot)
        {
            var storageKey = ToKey(type, update.Key);
            if (!this.documents.TryGetValue(storageKey, out var current))
            {
                return Task.FromResult(Result<DocumentInfo>.Failure(new DocumentStoreNotFoundError()));
            }

            if (!string.IsNullOrWhiteSpace(update.IfMatchETag) && !string.Equals(current.ETag, update.IfMatchETag, StringComparison.Ordinal))
            {
                return Task.FromResult(Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document ETag changed.")));
            }

            var updated = current with
            {
                ETag = Guid.NewGuid().ToString("N"),
                LastModifiedAt = DateTimeOffset.UtcNow,
                ExpiresAt = preserveExpiration ? current.ExpiresAt : resolvedExpiresAt,
                Properties = update.Properties?.Clone() ?? current.Properties.Clone()
            };
            this.documents[storageKey] = updated;
            return Task.FromResult(Result<DocumentInfo>.Success(ToInfo(updated)));
        }
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(DocumentTypeIdentity type, DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.syncRoot)
        {
            var storageKey = ToKey(type, key);
            if (this.documents.TryGetValue(storageKey, out var current) &&
                !string.IsNullOrWhiteSpace(options?.IfMatchETag) &&
                !string.Equals(current.ETag, options.IfMatchETag, StringComparison.Ordinal))
            {
                return Task.FromResult(Result.Failure(new DocumentStoreConflictError("The document ETag changed.")));
            }

            this.documents.Remove(storageKey);
            return Task.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentRetentionSweepResult>> SweepExpiredAsync(
        DocumentRetentionSweepRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var deleted = 0L;
        var deletedKeys = new List<DocumentKey>();
        var batches = 0;
        var hasMore = false;

        for (; batches < request.MaxBatches; batches++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int removed;
            lock (this.syncRoot)
            {
                var keys = this.documents
                    .Where(x => x.Key.Type == request.DocumentType.Value && x.Value.ExpiresAt is not null && x.Value.ExpiresAt <= request.VisibilityCutoff)
                    .OrderBy(x => x.Value.ExpiresAt)
                    .ThenBy(x => x.Key.Partition, StringComparer.Ordinal)
                    .ThenBy(x => x.Key.Row, StringComparer.Ordinal)
                    .Take(request.BatchSize)
                    .Select(x => x.Key)
                    .ToArray();
                foreach (var key in keys)
                {
                    this.documents.Remove(key);
                    deletedKeys.Add(new(key.Partition, key.Row));
                }

                removed = keys.Length;
                hasMore = removed == request.BatchSize;
            }

            deleted += removed;
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

    private IEnumerable<StoredDocument> Query(DocumentTypeIdentity type, DocumentKey? key, DocumentKeyFilter filter, DateTimeOffset cutoff)
    {
        var rows = this.documents
            .Where(x => string.Equals(x.Key.Type, type.Value, StringComparison.Ordinal) && IsVisible(x.Value, cutoff))
            .Select(x => x.Value);
        if (key is null)
        {
            return Order(rows);
        }

        var match = key.Value;
        rows = filter switch
        {
            DocumentKeyFilter.FullMatch => rows.Where(x => x.Key == match),
            DocumentKeyFilter.RowKeyPrefixMatch => rows.Where(x => x.Key.PartitionKey == match.PartitionKey && x.Key.RowKey.StartsWith(match.RowKey ?? string.Empty, StringComparison.Ordinal)),
            DocumentKeyFilter.RowKeySuffixMatch => rows.Where(x => x.Key.PartitionKey == match.PartitionKey && x.Key.RowKey.EndsWith(match.RowKey ?? string.Empty, StringComparison.Ordinal)),
            _ => []
        };
        return Order(rows);
    }

    private static IEnumerable<StoredDocument> Order(IEnumerable<StoredDocument> rows) => rows
        .OrderBy(x => x.Key.PartitionKey, StringComparer.Ordinal)
        .ThenBy(x => x.Key.RowKey, StringComparer.Ordinal);

    private static IEnumerable<StoredDocument> ApplyContinuation(IEnumerable<StoredDocument> rows, DocumentKey? key) => key is null
        ? rows
        : rows.Where(x => string.Compare(x.Key.PartitionKey, key.Value.PartitionKey, StringComparison.Ordinal) > 0 ||
            (x.Key.PartitionKey == key.Value.PartitionKey && string.Compare(x.Key.RowKey, key.Value.RowKey, StringComparison.Ordinal) > 0));

    private static string CreateToken(string operation, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, DocumentKey lastKey)
    {
        var hash = DocumentQueryHash.Compute(operation, type, query, query?.Take ?? 100);
        var token = DocumentContinuationTokenSerializer.Serialize(new DocumentContinuationToken
        {
            Provider = type.Value,
            QueryHash = hash,
            VisibilityCutoff = visibilityCutoff,
            NativeToken = JsonSerializer.Serialize(lastKey)
        });
        return token.IsSuccess ? token.Value : null;
    }

    private static DocumentKey? ReadNativeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parsed = DocumentContinuationTokenSerializer.Deserialize(token);
        return parsed.IsSuccess && !string.IsNullOrWhiteSpace(parsed.Value.NativeToken)
            ? JsonSerializer.Deserialize<DocumentKey>(parsed.Value.NativeToken)
            : null;
    }

    private static bool IsVisible(StoredDocument document, DateTimeOffset cutoff) => document.ExpiresAt is null || document.ExpiresAt > cutoff;
    private static (string Type, string Partition, string Row) ToKey(DocumentTypeIdentity type, DocumentKey key) => (type.Value, key.PartitionKey, key.RowKey);
    private static StoredDocument Clone(StoredDocument value) => value with
    {
        Content = value.Content.ToArray(),
        Properties = value.Properties?.Clone() ?? new PropertyBag(),
        TransformMetadata = value.TransformMetadata?.Clone() ?? new PropertyBag()
    };
    private static DocumentInfo ToInfo(StoredDocument value) => new()
    {
        Key = value.Key,
        ETag = value.ETag,
        ContentHash = value.ContentHash,
        CreatedAt = value.CreatedAt,
        LastModifiedAt = value.LastModifiedAt,
        ExpiresAt = value.ExpiresAt,
        Properties = value.Properties?.Clone() ?? new PropertyBag()
    };
}
