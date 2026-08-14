// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Application.Storage;
using Common;
using Microsoft.Azure.Cosmos;
using System.Linq.Expressions;
using System.Text.Json;

/// <summary>Persists provider-neutral serialized document envelopes with ETags, paging, and native TTL in Cosmos DB.</summary>
/// <param name="provider">The initialized Cosmos SQL provider used for item and continuation-page operations.</param>
/// <param name="options">Optional paging and stored-document size safety limits.</param>
/// <remarks>
/// Serialized content remains opaque to Cosmos. The provider stores exact expiration alongside per-item TTL, enforces
/// Cosmos ETags for conditional mutation, and keeps native continuation state inside Document Storage continuation tokens.
/// Container registration configures default TTL to -1 so non-expiring and per-item-expiring documents can coexist.
/// </remarks>
/// <example><code>var provider = new CosmosDocumentStoreProvider(cosmosSqlProvider);</code></example>
public class CosmosDocumentStoreProvider(ICosmosSqlProvider<CosmosStorageDocument> provider, DocumentStoreOptions options = null, string clientName = "default") : IDocumentStoreProvider
{
    private readonly DocumentStoreOptions options = options ?? new();
    private readonly string clientName = string.IsNullOrWhiteSpace(clientName) ? "default" : clientName.Trim().ToLowerInvariant();

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently, RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedServerSide, FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedServerSide, SupportsContinuationPaging = true, SupportsServerSideCount = false,
        SupportsKeyOnlyProjection = true, SupportsConditionalWrite = true, SupportsConditionalDelete = true,
        SupportsAtomicPropertyUpdate = true, SupportsLogicalExpiration = true, SupportsRetention = true
    };

    /// <inheritdoc />
    public async Task<Result<StoredDocument>> GetAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            var storageType = this.StorageType(type);
            var item = (await provider.ReadItemsAsync(x => x.Type == storageType && x.PartitionKey == key.PartitionKey && x.RowKey == key.RowKey && (x.Ttl == -1 || x.ExpiresAt > visibilityCutoff), partitionKeyValue: storageType, cancellationToken: cancellationToken)).FirstOrDefault();
            return item is null ? Result<StoredDocument>.Failure(new DocumentStoreNotFoundError()) : Result<StoredDocument>.Success(Map(item));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<StoredDocument>.Failure(new DocumentStoreProviderError("Cosmos document read failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<StoredDocumentPage>> FindPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            var take = query?.Take ?? this.options.DefaultTake;
            var storageType = this.StorageType(type);
            var page = await provider.ReadItemsPageResultAsync(Expressions(storageType, query, visibilityCutoff), take, x => x.RowKey, partitionKeyValue: storageType, continuationToken: ReadNative(query?.ContinuationToken), cancellationToken: cancellationToken);
            if (page.IsFailure) return page.Wrap<StoredDocumentPage>();
            return Result<StoredDocumentPage>.Success(new() { Items = page.Value.Items.Select(Map).ToArray(), ContinuationToken = string.IsNullOrWhiteSpace(page.Value.ContinuationToken) ? null : CreateToken("find", type, query, visibilityCutoff, page.Value.ContinuationToken) });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<StoredDocumentPage>.Failure(new DocumentStoreProviderError("Cosmos document page failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentKeyPage>> ListPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            var take = query?.Take ?? this.options.DefaultTake;
            var storageType = this.StorageType(type);
            var page = await provider.ReadItemsPageResultAsync(
                Expressions(storageType, query, visibilityCutoff),
                x => new CosmosStorageDocument { PartitionKey = x.PartitionKey, RowKey = x.RowKey },
                take,
                x => x.RowKey,
                partitionKeyValue: storageType,
                continuationToken: ReadNative(query?.ContinuationToken),
                cancellationToken: cancellationToken);
            if (page.IsFailure) return page.Wrap<DocumentKeyPage>();
            return Result<DocumentKeyPage>.Success(new()
            {
                Items = page.Value.Items.Select(x => new DocumentKey(x.PartitionKey, x.RowKey)).ToArray(),
                ContinuationToken = string.IsNullOrWhiteSpace(page.Value.ContinuationToken)
                    ? null
                    : CreateToken("list", type, query, visibilityCutoff, page.Value.ContinuationToken)
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentKeyPage>.Failure(new DocumentStoreProviderError("Cosmos document key page failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<long>> CountAsync(DocumentTypeIdentity type, DocumentCountQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        var pageQuery = new DocumentQuery { DocumentKey = query?.DocumentKey, Filter = query?.Filter ?? DocumentKeyFilter.FullMatch, AllowFullScan = query?.AllowFullScan ?? false, Take = this.options.MaxTake };
        long count = 0;
        while (true)
        {
            var result = await this.ListPageAsync(type, pageQuery, visibilityCutoff, cancellationToken);
            if (result.IsFailure) return result.Wrap<long>();
            count += result.Value.Items.Count;
            if (string.IsNullOrWhiteSpace(result.Value.ContinuationToken)) break;
            pageQuery = new DocumentQuery { DocumentKey = query?.DocumentKey, Filter = query?.Filter ?? DocumentKeyFilter.FullMatch, AllowFullScan = query?.AllowFullScan ?? false, Take = this.options.MaxTake, ContinuationToken = result.Value.ContinuationToken };
        }

        return Result<long>.Success(count);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        var result = await this.ListPageAsync(type, new DocumentQuery
        {
            DocumentKey = key,
            Filter = DocumentKeyFilter.FullMatch,
            Take = 1
        }, visibilityCutoff, cancellationToken);
        return result.IsFailure ? result.Wrap<bool>() : Result<bool>.Success(result.Value.Items.Count != 0);
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpsertAsync(DocumentTypeIdentity type, StoredDocumentWrite write, CancellationToken cancellationToken = default)
    {
        try
        {
            var storageType = this.StorageType(type);
            var current = (await provider.ReadItemsAsync(x => x.Type == storageType && x.PartitionKey == write.Key.PartitionKey && x.RowKey == write.Key.RowKey, partitionKeyValue: storageType, cancellationToken: cancellationToken)).FirstOrDefault();
            if (write.Options.CreateOnly && current is not null) return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("A physical document already exists."));
            if (!string.IsNullOrWhiteSpace(write.Options.IfMatchETag) && (current is null || ETag(current) != write.Options.IfMatchETag)) return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document ETag changed."));
            var now = DateTimeOffset.UtcNow;
            var isNew = current is null;
            current ??= new() { Id = GuidGenerator.Create($"{storageType}-{write.Key.PartitionKey}-{write.Key.RowKey}").ToString(), Type = storageType, PartitionKey = write.Key.PartitionKey, RowKey = write.Key.RowKey, CreatedDate = now };
            current.Content = write.Content.ToArray(); current.ContentHash = write.ContentHash; current.StoredContentHash = write.StoredContentHash;
            current.Properties = (write.Properties?.Clone() ?? new PropertyBag(current.Properties)).ToDictionary(x => x.Key, x => x.Value);
            current.TransformMetadataJson = EncodeBag(write.TransformMetadata);
            if (!write.PreserveExpiration) current.ExpiresAt = write.ExpiresAt;
            current.Ttl = ToTtl(current.ExpiresAt, now); current.UpdatedDate = now;
            current = isNew
                ? await provider.CreateItemAsync(current, storageType, cancellationToken)
                : await provider.UpsertItemAsync(current, storageType, current.ETag, cancellationToken);
            return Result<DocumentInfo>.Success(ToInfo(current));
        }
        catch (CosmosException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.PreconditionFailed) { return Result<DocumentInfo>.Failure(new DocumentStoreConflictError(ex.Message)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentInfo>.Failure(new DocumentStoreProviderError("Cosmos document write failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentTypeIdentity type, DocumentPropertiesUpdate update, DateTimeOffset? resolvedExpiresAt, bool preserveExpiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var storageType = this.StorageType(type);
            var current = (await provider.ReadItemsAsync(x => x.Type == storageType && x.PartitionKey == update.Key.PartitionKey && x.RowKey == update.Key.RowKey, partitionKeyValue: storageType, cancellationToken: cancellationToken)).FirstOrDefault();
            if (current is null) return Result<DocumentInfo>.Failure(new DocumentStoreNotFoundError());
            if (!string.IsNullOrWhiteSpace(update.IfMatchETag) && ETag(current) != update.IfMatchETag) return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document ETag changed."));
            if (update.Properties is not null) current.Properties = update.Properties.ToDictionary(x => x.Key, x => x.Value);
            if (!preserveExpiration) current.ExpiresAt = resolvedExpiresAt;
            current.UpdatedDate = DateTimeOffset.UtcNow; current.Ttl = ToTtl(current.ExpiresAt, current.UpdatedDate.Value);
            current = await provider.UpsertItemAsync(current, storageType, current.ETag, cancellationToken);
            return Result<DocumentInfo>.Success(ToInfo(current));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed) { return Result<DocumentInfo>.Failure(new DocumentStoreConflictError(ex.Message)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentInfo>.Failure(new DocumentStoreProviderError("Cosmos metadata update failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(DocumentTypeIdentity type, DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var storageType = this.StorageType(type);
            var current = (await provider.ReadItemsAsync(x => x.Type == storageType && x.PartitionKey == key.PartitionKey && x.RowKey == key.RowKey, partitionKeyValue: storageType, cancellationToken: cancellationToken)).FirstOrDefault();
            if (current is null) return Result.Success();
            if (!string.IsNullOrWhiteSpace(options?.IfMatchETag) && ETag(current) != options.IfMatchETag) return Result.Failure(new DocumentStoreConflictError("The document ETag changed."));
            var deleted = string.IsNullOrWhiteSpace(options?.IfMatchETag)
                ? await provider.DeleteItemAsync(current.Id, storageType, cancellationToken)
                : await provider.DeleteItemAsync(current.Id, storageType, options.IfMatchETag, cancellationToken);
            return deleted ? Result.Success() : Result.Failure(new DocumentStoreConflictError("The document ETag changed."));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result.Failure(new DocumentStoreProviderError("Cosmos document delete failed.", ex)); }
    }

    private static IEnumerable<Expression<Func<CosmosStorageDocument, bool>>> Expressions(string storageType, DocumentQuery query, DateTimeOffset cutoff)
    {
        yield return x => x.Type == storageType;
        yield return x => x.Ttl == -1 || x.ExpiresAt > cutoff;
        if (query?.DocumentKey is not DocumentKey key) yield break;
        yield return x => x.PartitionKey == key.PartitionKey;
        if (query.Filter == DocumentKeyFilter.FullMatch) yield return x => x.RowKey == key.RowKey;
        else if (query.Filter == DocumentKeyFilter.RowKeyPrefixMatch) yield return x => x.RowKey.StartsWith(key.RowKey);
        else if (query.Filter == DocumentKeyFilter.RowKeySuffixMatch) yield return x => x.RowKey.EndsWith(key.RowKey);
    }

    private static StoredDocument Map(CosmosStorageDocument x) => new() { Key = new(x.PartitionKey, x.RowKey), Content = x.Content?.ToArray() ?? [], ContentHash = x.ContentHash, StoredContentHash = x.StoredContentHash, ETag = ETag(x), CreatedAt = x.CreatedDate, LastModifiedAt = x.UpdatedDate ?? x.CreatedDate, ExpiresAt = x.ExpiresAt, Properties = new PropertyBag(x.Properties), TransformMetadata = DecodeBag(x.TransformMetadataJson) };
    private static DocumentInfo ToInfo(CosmosStorageDocument x) => new() { Key = new(x.PartitionKey, x.RowKey), ETag = ETag(x), ContentHash = x.ContentHash, CreatedAt = x.CreatedDate, LastModifiedAt = x.UpdatedDate ?? x.CreatedDate, ExpiresAt = x.ExpiresAt, Properties = new PropertyBag(x.Properties) };
    private static string ETag(CosmosStorageDocument x) => x.ETag;
    private static int ToTtl(DateTimeOffset? expiresAt, DateTimeOffset now) => expiresAt is null ? -1 : Math.Max(1, (int)Math.Ceiling((expiresAt.Value - now).TotalSeconds));
    private static string EncodeBag(PropertyBag bag) => JsonSerializer.Serialize((bag ?? new()).ToDictionary(x => x.Key, x => PropertyBagScalarCodec.Encode(x.Value)));
    private static PropertyBag DecodeBag(string json) { if (string.IsNullOrWhiteSpace(json)) return new(); var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json); return new(values.ToDictionary(x => x.Key, x => PropertyBagScalarCodec.Decode(x.Value))); }
    private static string CreateToken(string operation, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, string native) { var result = DocumentContinuationTokenSerializer.Serialize(new() { Provider = type.Value, QueryHash = DocumentQueryHash.Compute(operation, type, query, query?.Take ?? 100), VisibilityCutoff = visibilityCutoff, NativeToken = native }); return result.IsSuccess ? result.Value : null; }
    private static string ReadNative(string token) { if (string.IsNullOrWhiteSpace(token)) return null; var result = DocumentContinuationTokenSerializer.Deserialize(token); return result.IsSuccess ? result.Value.NativeToken : null; }
    private string StorageType(DocumentTypeIdentity type) => $"{this.clientName}:{type.Value}";
}
