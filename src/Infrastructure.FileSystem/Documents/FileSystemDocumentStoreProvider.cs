namespace BridgingIT.DevKit.Infrastructure.FileSystem;

using System.Text.Json;

/// <summary>Persists serialized documents and metadata in provider-owned generation files.</summary>
/// <example><code>var provider = new FileSystemDocumentStoreProvider(options);</code></example>
public sealed class FileSystemDocumentStoreProvider : IDocumentStoreProvider, IDocumentStoreRetentionProvider
{
    private readonly FileSystemStore store;
    private readonly DocumentStoreOptions options;

    /// <summary>Gets the low-level store for trusted diagnostics, ownership-aware tooling and explicit quiescent recovery.</summary>
    /// <remarks>Ordinary document operations should use the provider or client methods. Low-level mutations require the store's ownership and persistence protocol.</remarks>
    /// <example><code>var configuration = provider.Store.Options;</code></example>
    public FileSystemStore Store => this.store;

    /// <summary>Creates a filesystem document provider.</summary>
    /// <example><code>var provider = new FileSystemDocumentStoreProvider(options);</code></example>
    public FileSystemDocumentStoreProvider(FileSystemStorageOptions options, DocumentStoreOptions documentStoreOptions = null)
    {
        this.store = new(options);
        this.options = documentStoreOptions ?? new();

        if (this.options.Validate().IsFailure)
        {
            throw new ArgumentException("Invalid document store options.", nameof(documentStoreOptions));
        }
    }

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedClientSide,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedClientSide,
        FullScan = DocumentQuerySupport.SupportedClientSide,
        KeyListing = DocumentQuerySupport.SupportedClientSide,
        SupportsContinuationPaging = true,
        SupportsKeyOnlyProjection = true,
        SupportsConditionalWrite = true,
        SupportsConditionalDelete = true,
        SupportsAtomicPropertyUpdate = true,
        SupportsLogicalExpiration = true,
        SupportsRetention = true
    };

    /// <inheritdoc />
    public Task<Result<StoredDocument>> GetAsync(
        DocumentTypeIdentity type,
        DocumentKey key,
        DateTimeOffset visibilityCutoff,
        CancellationToken cancellationToken = default)
    {
        return FileSystemResults.DocumentAsync(async () =>
        {
            var identity = Identity(type, key);
            var path = this.store.Resolve(identity, false);
            using var authority = await this.store.AcquireAsync(
                path,
                immediate: false,
                cancellationToken).ConfigureAwait(false);
            var record = this.store.ReadUnderAuthority(path, authority, identity);

            if (record?.Document is not { } document || !Visible(document, visibilityCutoff))
            {
                return Result<StoredDocument>.Failure(new DocumentStoreNotFoundError());
            }

            await using var payload = this.store.OpenPayload(path, record);
            authority.Dispose(); // The immutable open file handle now pins the selected generation.

            if (record.Length > int.MaxValue)
            {
                return Result<StoredDocument>.Failure(new DocumentStoreSizeLimitError());
            }

            var content = new byte[(int)record.Length];
            await payload.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);

            return Result<StoredDocument>.Success(document with { Content = content });
        });
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.DocumentAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = Identity(type, key);
            var record = this.store.Read(this.store.Resolve(identity, false), identity);
            await Task.CompletedTask;

            return Result<bool>.Success(record?.Document is { } document && Visible(document, visibilityCutoff));
        });
    }

    /// <inheritdoc />
    public Task<Result<DocumentInfo>> UpsertAsync(DocumentTypeIdentity type, StoredDocumentWrite write, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.DocumentAsync(async () =>
        {
            var identity = Identity(type, write.Key);
            var path = this.store.Resolve(identity);
            using var source = new MemoryStream(write.Content, false);
            using var staged = await this.store.StageAsync(path, source, null, cancellationToken).ConfigureAwait(false);
            using var authority = await this.store.AcquireAsync(path, false, cancellationToken).ConfigureAwait(false);
            var current = this.store.Read(path, identity)?.Document;

            if ((write.Options.CreateOnly && current is not null)
                || (!string.IsNullOrWhiteSpace(write.Options.IfMatchETag) && write.Options.IfMatchETag != current?.ETag))
            {
                return Result<DocumentInfo>.Failure(new DocumentStoreConflictError());
            }

            var now = DateTimeOffset.UtcNow;
            var document = new StoredDocument
            {
                Key = write.Key,
                ContentHash = write.ContentHash,
                StoredContentHash = write.StoredContentHash,
                ETag = Guid.NewGuid().ToString("N"),
                CreatedAt = current?.CreatedAt ?? now,
                LastModifiedAt = now,
                ExpiresAt = write.PreserveExpiration ? current?.ExpiresAt : write.ExpiresAt?.ToUniversalTime(),
                Properties = write.Properties?.Clone() ?? current?.Properties?.Clone() ?? new(),
                TransformMetadata = write.TransformMetadata?.Clone() ?? new()
            };
            this.store.Publish(path, new() { Identity = identity, Payload = staged.File, Length = staged.Length, Document = document }, authority, cancellationToken, staged);

            return Result<DocumentInfo>.Success(ToInfo(document));
        });
    }

    /// <inheritdoc />
    public Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentTypeIdentity type, DocumentPropertiesUpdate update, DateTimeOffset? resolvedExpiresAt, bool preserveExpiration, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.DocumentAsync(async () =>
        {
            var identity = Identity(type, update.Key);
            var path = this.store.Resolve(identity, false);
            using var authority = await this.store.AcquireAsync(path, false, cancellationToken).ConfigureAwait(false);
            var record = this.store.ReadUnderAuthority(path, authority, identity);

            if (record?.Document is not { } current)
            {
                return Result<DocumentInfo>.Failure(new DocumentStoreNotFoundError());
            }

            if (!string.IsNullOrWhiteSpace(update.IfMatchETag) && update.IfMatchETag != current.ETag)
            {
                return Result<DocumentInfo>.Failure(new DocumentStoreConflictError());
            }

            var document = current with
            {
                ETag = Guid.NewGuid().ToString("N"),
                LastModifiedAt = DateTimeOffset.UtcNow,
                ExpiresAt = preserveExpiration ? current.ExpiresAt : resolvedExpiresAt?.ToUniversalTime(),
                Properties = update.Properties?.Clone() ?? current.Properties.Clone()
            };
            this.store.Publish(path, record with { Document = document }, authority, cancellationToken);

            return Result<DocumentInfo>.Success(ToInfo(document));
        });
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(DocumentTypeIdentity type, DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await FileSystemResults.DocumentAsync(async () =>
        {
            var identity = Identity(type, key);
            var path = this.store.Resolve(identity, false);
            using var authority = await this.store.AcquireAsync(path, false, cancellationToken).ConfigureAwait(false);
            var record = this.store.ReadUnderAuthority(path, authority, identity);
            var current = record?.Document;

            if (current is not null && !string.IsNullOrWhiteSpace(options?.IfMatchETag) && current.ETag != options.IfMatchETag)
            {
                return Result<bool>.Failure(new DocumentStoreConflictError());
            }

            cancellationToken.ThrowIfCancellationRequested();
            this.store.Delete(path, record, authority, cancellationToken);

            return Result<bool>.Success(true);
        }).ConfigureAwait(false);

        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    /// <inheritdoc />
    public Task<Result<StoredDocumentPage>> FindPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.DocumentAsync(async () =>
        {
            var page = this.Page(type, query, visibilityCutoff, "find", cancellationToken);
            var documents = new List<StoredDocument>();
            foreach (var(path, record)in page.Rows)
            {
                FileStream stream;
                FileSystemRecord current;
                using (var authority = await this.store.AcquireAsync(path, false, cancellationToken).ConfigureAwait(false))
                {
                    current = this.store.ReadUnderAuthority(path, authority, record.Identity);

                    if (current?.Document is not { } metadata || !Visible(metadata, visibilityCutoff))
                    {
                        continue;
                    }

                    stream = this.store.OpenPayload(path, current);
                }

                await using var payload = stream;

                if (current.Length > int.MaxValue)
                {
                    return Result<StoredDocumentPage>.Failure(new DocumentStoreSizeLimitError());
                }

                var bytes = new byte[(int)current.Length];
                await payload.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
                documents.Add(current.Document with { Content = bytes });
            }

            return Result<StoredDocumentPage>.Success(new() { Items = documents, ContinuationToken = page.Token });
        });
    }

    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.DocumentAsync(async () =>
        {
            var page = this.Page(type, query, visibilityCutoff, "list", cancellationToken);
            await Task.CompletedTask;

            return Result<DocumentKeyPage>.Success(new() { Items = page.Rows.Select(x => x.Record.Document.Key).ToArray(), ContinuationToken = page.Token });
        });
    }

    /// <inheritdoc />
    public Task<Result<long>> CountAsync(DocumentTypeIdentity type, DocumentCountQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.DocumentAsync(async () =>
        {
            long count = 0;

            foreach (var entry in this.Matching(type, query.DocumentKey, query.Filter, visibilityCutoff, cancellationToken))
            {
                count++;
            }

            await Task.CompletedTask;

            return Result<long>.Success(count);
        });
    }

    private (List<(string Path, FileSystemRecord Record)> Rows, string Token) Page(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset cutoff, string operation, CancellationToken cancellationToken)
    {
        var take = query.Take ?? this.options.DefaultTake;

        if (take <= 0)
        {
            throw new FileSystemContinuationException("Invalid document page size.");
        }

        DocumentKey? after = null;

        if (!string.IsNullOrEmpty(query.ContinuationToken))
        {
            var parsed = DocumentContinuationTokenSerializer.Deserialize(query.ContinuationToken);

            if (parsed.IsFailure || parsed.Value.Provider != type.Value || parsed.Value.VisibilityCutoff != cutoff
                || parsed.Value.QueryHash != DocumentQueryHash.Compute(operation, type, query, take)
                || parsed.Value.NativeToken is null || parsed.Value.NativeToken.Length > 16384)
            {
                throw new FileSystemContinuationException("Invalid continuation token.");
            }

            try
            {
                after = JsonSerializer.Deserialize<DocumentKey>(parsed.Value.NativeToken);
            }
            catch (JsonException)
            {
                throw new FileSystemContinuationException("Invalid native document continuation position.");
            }

            if (string.IsNullOrWhiteSpace(after.Value.PartitionKey) || string.IsNullOrWhiteSpace(after.Value.RowKey))
            {
                throw new FileSystemContinuationException("Invalid native document continuation position.");
            }
        }

        var rows = new SortedDictionary<DocumentKey, (string Path, FileSystemRecord Record)>(Comparer<DocumentKey>.Create(Compare));

        foreach (var entry in this.Matching(type, query.DocumentKey, query.Filter, cutoff, cancellationToken))
        {
            if (after is not null && Compare(entry.Record.Document.Key, after.Value) <= 0)
            {
                continue;
            }

            rows[entry.Record.Document.Key] = entry;

            if (rows.Count > (long)take + 1)
            {
                rows.Remove(rows.Keys.Last());
            }
        }

        var page = rows.Values.Take(take).ToList();
        var token = rows.Count > take ? DocumentContinuationTokenSerializer.Serialize(new DocumentContinuationToken { Provider = type.Value,
            QueryHash = DocumentQueryHash.Compute(operation, type, query, take), VisibilityCutoff = cutoff,
            NativeToken = JsonSerializer.Serialize(page[^1].Record.Document.Key) }).Value : null;

        return (page, token);
    }

    private IEnumerable<(string Path, FileSystemRecord Record)> Matching(DocumentTypeIdentity type, DocumentKey? key, DocumentKeyFilter filter, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        if (key is not null && filter == DocumentKeyFilter.FullMatch)
        {
            var identity = Identity(type, key.Value);
            var path = this.store.Resolve(identity, false);
            var record = this.store.Read(path, identity);

            if (record?.Document is { } document && Visible(document, cutoff))
            {
                yield return (path, record);
            }

            yield break;
        }

        var inspected = 0;

        foreach (var path in this.store.Paths("documents", type.Value, key?.PartitionKey))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (++inspected > this.store.MaxQueryMetadataEntries)
            {
                throw new FileSystemMetadataLimitException("Metadata scan bound exceeded.");
            }

            var record = this.store.Read(path);

            if (record?.Document is not { } document || record.Identity[1] != type.Value || !Visible(document, cutoff))
            {
                continue;
            }

            if (key is null || (document.Key.PartitionKey == key.Value.PartitionKey && (filter switch
            {
                DocumentKeyFilter.RowKeyPrefixMatch => document.Key.RowKey.StartsWith(key.Value.RowKey ?? "", StringComparison.Ordinal),
                DocumentKeyFilter.RowKeySuffixMatch => document.Key.RowKey.EndsWith(key.Value.RowKey ?? "", StringComparison.Ordinal),
                _ => false
            })))
            {
                yield return (path, record);
            }
        }
    }

    private static int Compare(DocumentKey x, DocumentKey y)
    {
        return string.CompareOrdinal(x.PartitionKey, y.PartitionKey)is var partition && partition != 0 ? partition : string.CompareOrdinal(x.RowKey, y.RowKey);
    }

    private static bool Visible(StoredDocument document, DateTimeOffset cutoff)
    {
        return document.ExpiresAt is null || document.ExpiresAt > cutoff;
    }

    private static string[] Identity(DocumentTypeIdentity type, DocumentKey key)
    {
        return["documents", type.Value, key.PartitionKey, key.RowKey];
    }

    private static DocumentInfo ToInfo(StoredDocument document)
    {
        return new()
        {
            Key = document.Key,
            ETag = document.ETag,
            ContentHash = document.ContentHash,
            CreatedAt = document.CreatedAt,
            LastModifiedAt = document.LastModifiedAt,
            ExpiresAt = document.ExpiresAt,
            Properties = document.Properties.Clone()
        };
    }

    /// <inheritdoc />
    public Task<Result<DocumentRetentionSweepResult>> SweepExpiredAsync(DocumentRetentionSweepRequest request, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.DocumentAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.BatchSize <= 0 || request.MaxBatches <= 0 || request.BatchDelay < TimeSpan.Zero)
            {
                return Result<DocumentRetentionSweepResult>.Failure(new DocumentStoreInvalidQueryError("Invalid retention bounds."));
            }

            var candidates = await this.store.RetentionCandidatesAsync("documents", request.DocumentType.Value, cancellationToken).ConfigureAwait(false);
            this.store.Fault?.Invoke("retention-discovered");
            var deleted = new List<DocumentKey>();
            var limit = (long)request.BatchSize * request.MaxBatches;

            foreach (var path in candidates.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (deleted.Count >= limit)
                {
                    break;
                }

                using (var authority = await this.store.AcquireAsync(path, true, cancellationToken).ConfigureAwait(false))
                {
                    if (authority is null)
                    {
                        continue;
                    }

                    var record = this.store.Read(path);
                    this.store.TryCollect(path, authority);

                    if (record?.Document is not { ExpiresAt: { } expiry } document || record.Identity[1] != request.DocumentType.Value || expiry > request.VisibilityCutoff)
                    {
                        continue;
                    }

                    this.store.Delete(path, record, authority, cancellationToken);
                    deleted.Add(document.Key);
                }

                if (deleted.Count % request.BatchSize == 0 && request.BatchDelay > TimeSpan.Zero && deleted.Count < limit)
                {
                    await Task.Delay(request.BatchDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            return Result<DocumentRetentionSweepResult>.Success(new() { DocumentType = request.DocumentType, DeletedKeys = deleted,
                DeletedCount = deleted.Count, BatchCount = (int)((deleted.Count + (long)request.BatchSize - 1) / request.BatchSize), HasMore = candidates.HasMore || deleted.Count == limit });
        });
    }
}