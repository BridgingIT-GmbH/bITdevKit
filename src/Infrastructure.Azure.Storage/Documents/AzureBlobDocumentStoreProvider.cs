// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.Azure;

using global::Azure;
using global::Azure.Storage.Blobs.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

/// <summary>Persists serialized documents in deterministic, provider-managed Azure Blob containers.</summary>
/// <remarks>
/// The provider requires only storage-account connectivity. For each normalized client name and document type identity it
/// derives a deterministic container name, creates or reuses that container through an asynchronous initialization gate,
/// and never deletes provider resources during disposal. Conditional operations use Azure ETags and serialized document
/// metadata uses reserved <c>bdk_</c> properties.
/// </remarks>
/// <example><code>var provider = new AzureBlobDocumentStoreProvider(loggerFactory, blobServiceClient);</code></example>
public class AzureBlobDocumentStoreProvider : IDocumentStoreProvider, IDocumentStoreRetentionProvider
{
    private const string ContentHashKey = "bdk_content_hash";
    private const string StoredHashKey = "bdk_stored_content_hash";
    private const string CreatedKey = "bdk_created_at";
    private const string ModifiedKey = "bdk_modified_at";
    private const string ExpiresKey = "bdk_expires_at";
    private const string PropertiesKey = "bdk_properties";
    private const string TransformsKey = "bdk_transforms";
    private readonly BlobServiceClient serviceClient;
    private readonly string prefix;
    private readonly string clientName;
    private readonly DocumentStoreOptions options;
    private readonly Dictionary<string, AsyncInitializationGate> gates = [];
    private readonly object gateLock = new();

    /// <summary>Initializes a provider from an existing storage-account service client.</summary>
    /// <param name="loggerFactory">The optional logger factory used for typed operational logging.</param>
    /// <param name="serviceClient">The account-level Blob service client; callers do not create or pass container clients.</param>
    /// <param name="containerNamePrefix">An optional validated prefix prepended to deterministic container names.</param>
    /// <param name="options">Optional paging and stored-document size safety limits.</param>
    /// <param name="clientName">The normalized named Document Storage client identity used for resource isolation.</param>
    /// <example><code>var provider = new AzureBlobDocumentStoreProvider(loggerFactory, blobServiceClient, clientName: "archive");</code></example>
    public AzureBlobDocumentStoreProvider(ILoggerFactory loggerFactory, BlobServiceClient serviceClient, string containerNamePrefix = null, DocumentStoreOptions options = null, string clientName = "default")
    {
        this.serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
        this.prefix = NormalizePrefix(containerNamePrefix);
        this.clientName = NormalizeClientName(clientName);
        this.options = options ?? new();
        this.Logger = loggerFactory?.CreateLogger<AzureBlobDocumentStoreProvider>() ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<AzureBlobDocumentStoreProvider>();
    }

    /// <summary>Initializes a provider from a storage-account connection string.</summary>
    /// <param name="loggerFactory">The optional logger factory used for typed operational logging.</param>
    /// <param name="connectionString">The existing Azure Storage account connection string.</param>
    /// <param name="containerNamePrefix">An optional validated prefix prepended to deterministic container names.</param>
    /// <param name="clientOptions">Optional Azure Blob transport client options.</param>
    /// <param name="serializer">Reserved for registration compatibility; payload serialization is owned by the document client.</param>
    /// <param name="options">Optional paging and stored-document size safety limits.</param>
    /// <param name="clientName">The normalized named Document Storage client identity used for resource isolation.</param>
    /// <example><code>var provider = new AzureBlobDocumentStoreProvider(loggerFactory, connectionString, clientName: "archive");</code></example>
    public AzureBlobDocumentStoreProvider(ILoggerFactory loggerFactory, string connectionString, string containerNamePrefix = null, BlobClientOptions clientOptions = null, ISerializer serializer = null, DocumentStoreOptions options = null, string clientName = "default")
        : this(loggerFactory, new BlobServiceClient(connectionString, clientOptions), containerNamePrefix, options, clientName) { }

    /// <summary>Gets the typed provider logger used for non-sensitive resource and operation diagnostics.</summary>
    /// <example><code>protected ILogger ProviderLogger =&gt; this.Logger;</code></example>
    protected ILogger<AzureBlobDocumentStoreProvider> Logger { get; }

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeySuffixMatch = DocumentQuerySupport.Unsupported,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedEfficiently,
        SupportsContinuationPaging = true,
        SupportsServerSideCount = false,
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
            var blob = (await this.GetContainerAsync(type, cancellationToken)).GetBlobClient(Name(key));
            var response = await blob.DownloadContentAsync(cancellationToken);
            var document = Map(key, response.Value.Content.ToArray(), response.Value.Details.Metadata, response.Value.Details.ETag.ToString(), response.Value.Details.LastModified);
            return document.ExpiresAt is not null && document.ExpiresAt <= visibilityCutoff
                ? Result<StoredDocument>.Failure(new DocumentStoreNotFoundError())
                : Result<StoredDocument>.Success(document);
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return Result<StoredDocument>.Failure(new DocumentStoreNotFoundError()); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<StoredDocument>.Failure(new DocumentStoreProviderError("Azure Blob document read failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<StoredDocumentPage>> FindPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await this.GetContainerAsync(type, cancellationToken);
            var take = query?.Take ?? this.options.DefaultTake;
            var native = ReadNativeToken(query?.ContinuationToken);
            var prefix = QueryPrefix(query);
            var items = new List<StoredDocument>();
            string next = null;
            await foreach (var page in container.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, prefix, cancellationToken).AsPages(native, take).WithCancellation(cancellationToken))
            {
                foreach (var item in page.Values)
                {
                    var key = ParseName(item.Name);
                    if (!Matches(key, query)) continue;
                    var expires = ReadDate(item.Metadata, ExpiresKey);
                    if (expires is not null && expires <= visibilityCutoff) continue;
                    var content = await container.GetBlobClient(item.Name).DownloadContentAsync(cancellationToken);
                    items.Add(Map(key, content.Value.Content.ToArray(), item.Metadata, item.Properties.ETag?.ToString(), item.Properties.LastModified ?? DateTimeOffset.UtcNow));
                }
                next = page.ContinuationToken;
                if (items.Count >= take || string.IsNullOrWhiteSpace(next)) break;
            }
            return Result<StoredDocumentPage>.Success(new()
            {
                Items = items.Take(take).ToArray(),
                ContinuationToken = string.IsNullOrWhiteSpace(next) ? null : CreateToken("find", type, query, visibilityCutoff, next)
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<StoredDocumentPage>.Failure(new DocumentStoreProviderError("Azure Blob document page failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentKeyPage>> ListPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        var result = await this.FindPageAsync(type, query, visibilityCutoff, cancellationToken);
        return result.IsFailure ? result.Wrap<DocumentKeyPage>() : Result<DocumentKeyPage>.Success(new() { Items = result.Value.Items.Select(x => x.Key).ToArray(), ContinuationToken = RebindToken("list", type, query, visibilityCutoff, result.Value.ContinuationToken) });
    }

    /// <inheritdoc />
    public async Task<Result<long>> CountAsync(DocumentTypeIdentity type, DocumentCountQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        var pageQuery = new DocumentQuery { DocumentKey = query?.DocumentKey, Filter = query?.Filter ?? DocumentKeyFilter.FullMatch, AllowFullScan = query?.AllowFullScan ?? false, Take = this.options.MaxTake };
        long count = 0;
        do
        {
            var page = await this.ListPageAsync(type, pageQuery, visibilityCutoff, cancellationToken);
            if (page.IsFailure) return page.Wrap<long>();
            count += page.Value.Items.Count;
            pageQuery = new DocumentQuery { DocumentKey = query?.DocumentKey, Filter = query?.Filter ?? DocumentKeyFilter.FullMatch, AllowFullScan = query?.AllowFullScan ?? false, Take = this.options.MaxTake, ContinuationToken = page.Value.ContinuationToken };
        } while (!string.IsNullOrWhiteSpace(pageQuery.ContinuationToken));
        return Result<long>.Success(count);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        var result = await this.GetAsync(type, key, visibilityCutoff, cancellationToken);
        return result.IsSuccess ? Result<bool>.Success(true) : result.Errors.Any(x => x is DocumentStoreNotFoundError) ? Result<bool>.Success(false) : result.Wrap<bool>();
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpsertAsync(DocumentTypeIdentity type, StoredDocumentWrite write, CancellationToken cancellationToken = default)
    {
        try
        {
            var blob = (await this.GetContainerAsync(type, cancellationToken)).GetBlobClient(Name(write.Key));
            StoredDocument current = null;
            if (write.PreserveExpiration || write.Properties is null)
            {
                var read = await this.GetPhysicalAsync(blob, write.Key, cancellationToken);
                if (read.IsSuccess) current = read.Value;
            }
            var now = DateTimeOffset.UtcNow;
            var metadata = Metadata(write, current, now);
            var conditions = new BlobRequestConditions();
            if (write.Options.CreateOnly) conditions.IfNoneMatch = ETag.All;
            if (!string.IsNullOrWhiteSpace(write.Options.IfMatchETag)) conditions.IfMatch = new ETag(write.Options.IfMatchETag);
            var response = await blob.UploadAsync(BinaryData.FromBytes(write.Content), new BlobUploadOptions { Metadata = metadata, Conditions = conditions }, cancellationToken);
            return Result<DocumentInfo>.Success(new() { Key = write.Key, ETag = response.Value.ETag.ToString(), ContentHash = write.ContentHash, CreatedAt = current?.CreatedAt ?? now, LastModifiedAt = now, ExpiresAt = write.PreserveExpiration ? current?.ExpiresAt : write.ExpiresAt, Properties = write.Properties?.Clone() ?? current?.Properties?.Clone() ?? new PropertyBag() });
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412) { return Result<DocumentInfo>.Failure(new DocumentStoreConflictError(ex.Message)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentInfo>.Failure(new DocumentStoreProviderError("Azure Blob document write failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentTypeIdentity type, DocumentPropertiesUpdate update, DateTimeOffset? resolvedExpiresAt, bool preserveExpiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var blob = (await this.GetContainerAsync(type, cancellationToken)).GetBlobClient(Name(update.Key));
            var read = await this.GetPhysicalAsync(blob, update.Key, cancellationToken);
            if (read.IsFailure) return read.Wrap<DocumentInfo>();
            var current = read.Value;
            var metadata = new Dictionary<string, string>((await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                [ModifiedKey] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                [PropertiesKey] = EncodeBag(update.Properties ?? current.Properties)
            };
            if (!preserveExpiration) SetDate(metadata, ExpiresKey, resolvedExpiresAt);
            var etag = string.IsNullOrWhiteSpace(update.IfMatchETag) ? current.ETag : update.IfMatchETag;
            var response = await blob.SetMetadataAsync(metadata, new BlobRequestConditions { IfMatch = new ETag(etag) }, cancellationToken);
            return Result<DocumentInfo>.Success(new() { Key = update.Key, ETag = response.Value.ETag.ToString(), ContentHash = current.ContentHash, CreatedAt = current.CreatedAt, LastModifiedAt = DateTimeOffset.UtcNow, ExpiresAt = preserveExpiration ? current.ExpiresAt : resolvedExpiresAt, Properties = update.Properties?.Clone() ?? current.Properties.Clone() });
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return Result<DocumentInfo>.Failure(new DocumentStoreNotFoundError()); }
        catch (RequestFailedException ex) when (ex.Status == 412) { return Result<DocumentInfo>.Failure(new DocumentStoreConflictError(ex.Message)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentInfo>.Failure(new DocumentStoreProviderError("Azure Blob metadata update failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(DocumentTypeIdentity type, DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var blob = (await this.GetContainerAsync(type, cancellationToken)).GetBlobClient(Name(key));
            var conditions = string.IsNullOrWhiteSpace(options?.IfMatchETag) ? null : new BlobRequestConditions { IfMatch = new ETag(options.IfMatchETag) };
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, conditions, cancellationToken);
            return Result.Success();
        }
        catch (RequestFailedException ex) when (ex.Status == 412) { return Result.Failure(new DocumentStoreConflictError(ex.Message)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result.Failure(new DocumentStoreProviderError("Azure Blob document delete failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentRetentionSweepResult>> SweepExpiredAsync(DocumentRetentionSweepRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await this.GetContainerAsync(request.DocumentType, cancellationToken);
            var deleted = 0L;
            var deletedKeys = new List<DocumentKey>();
            var batches = 0;
            var hasMore = false;
            for (; batches < request.MaxBatches; batches++)
            {
                var candidates = new List<BlobItem>(request.BatchSize);
                await foreach (var item in container.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, prefix: null, cancellationToken))
                {
                    if (ReadDate(item.Metadata, ExpiresKey) is DateTimeOffset expiresAt && expiresAt <= request.VisibilityCutoff)
                    {
                        candidates.Add(item);
                        if (candidates.Count == request.BatchSize) break;
                    }
                }
                foreach (var item in candidates)
                {
                    try
                    {
                        await container.GetBlobClient(item.Name).DeleteIfExistsAsync(
                            DeleteSnapshotsOption.IncludeSnapshots,
                            new BlobRequestConditions { IfMatch = item.Properties.ETag },
                            cancellationToken);
                        deleted++;
                        deletedKeys.Add(ParseName(item.Name));
                    }
                    catch (RequestFailedException ex) when (ex.Status is 404 or 412) { }
                }
                hasMore = candidates.Count == request.BatchSize;
                if (!hasMore) { batches++; break; }
                if (request.BatchDelay > TimeSpan.Zero) await Task.Delay(request.BatchDelay, cancellationToken);
            }
            return Result<DocumentRetentionSweepResult>.Success(new() { DocumentType = request.DocumentType, DeletedCount = deleted, DeletedKeys = deletedKeys, BatchCount = batches, HasMore = hasMore });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentRetentionSweepResult>.Failure(new DocumentStoreProviderError("Azure Blob document retention failed.", ex)); }
    }

    private async Task<BlobContainerClient> GetContainerAsync(DocumentTypeIdentity type, CancellationToken cancellationToken)
    {
        var name = $"{this.prefix}bdk-doc-{HashHelper.ComputeSha256(this.clientName + "\n" + type.Value)[..32]}";
        var container = this.serviceClient.GetBlobContainerClient(name);
        AsyncInitializationGate gate;
        lock (this.gateLock) gate = this.gates.GetValueOrDefault(name) ?? (this.gates[name] = new());
        await gate.EnsureInitializedAsync(async ct => { await container.CreateIfNotExistsAsync(cancellationToken: ct); }, cancellationToken);
        return container;
    }

    private async Task<Result<StoredDocument>> GetPhysicalAsync(BlobClient blob, DocumentKey key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await blob.DownloadContentAsync(cancellationToken);
            return Result<StoredDocument>.Success(Map(key, response.Value.Content.ToArray(), response.Value.Details.Metadata, response.Value.Details.ETag.ToString(), response.Value.Details.LastModified));
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return Result<StoredDocument>.Failure(new DocumentStoreNotFoundError()); }
    }

    private static StoredDocument Map(DocumentKey key, byte[] content, IDictionary<string, string> metadata, string etag, DateTimeOffset modified) => new()
    {
        Key = key, Content = content, ETag = etag, ContentHash = metadata.GetValueOrDefault(ContentHashKey), StoredContentHash = metadata.GetValueOrDefault(StoredHashKey),
        CreatedAt = ReadDate(metadata, CreatedKey) ?? modified, LastModifiedAt = ReadDate(metadata, ModifiedKey) ?? modified, ExpiresAt = ReadDate(metadata, ExpiresKey),
        Properties = DecodeBag(metadata.GetValueOrDefault(PropertiesKey)), TransformMetadata = DecodeBag(metadata.GetValueOrDefault(TransformsKey))
    };

    private static Dictionary<string, string> Metadata(StoredDocumentWrite write, StoredDocument current, DateTimeOffset now)
    {
        var metadata = new Dictionary<string, string> { [ContentHashKey] = write.ContentHash, [StoredHashKey] = write.StoredContentHash, [CreatedKey] = (current?.CreatedAt ?? now).ToString("O", CultureInfo.InvariantCulture), [ModifiedKey] = now.ToString("O", CultureInfo.InvariantCulture), [PropertiesKey] = EncodeBag(write.Properties ?? current?.Properties), [TransformsKey] = EncodeBag(write.TransformMetadata) };
        SetDate(metadata, ExpiresKey, write.PreserveExpiration ? current?.ExpiresAt : write.ExpiresAt);
        return metadata;
    }
    private static string EncodeBag(PropertyBag bag) => Base64UrlHelper.Encode(JsonSerializer.SerializeToUtf8Bytes((bag ?? new()).ToDictionary(x => x.Key, x => PropertyBagScalarCodec.Encode(x.Value))));
    private static PropertyBag DecodeBag(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new();
        var encoded = JsonSerializer.Deserialize<Dictionary<string, string>>(Base64UrlHelper.Decode(value));
        return new PropertyBag(encoded.ToDictionary(x => x.Key, x => PropertyBagScalarCodec.Decode(x.Value)));
    }
    private static void SetDate(IDictionary<string, string> metadata, string key, DateTimeOffset? value) { if (value is null) metadata.Remove(key); else metadata[key] = value.Value.ToString("O", CultureInfo.InvariantCulture); }
    private static DateTimeOffset? ReadDate(IDictionary<string, string> metadata, string key) => metadata.TryGetValue(key, out var value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;
    private static string Name(DocumentKey key) => $"{Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(key.PartitionKey))}/{Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(key.RowKey))}";
    private static DocumentKey ParseName(string name) { var parts = name.Split('/', 2); return new(Encoding.UTF8.GetString(Base64UrlHelper.Decode(parts[0])), Encoding.UTF8.GetString(Base64UrlHelper.Decode(parts[1]))); }
    private static string QueryPrefix(DocumentQuery query) => query?.DocumentKey is DocumentKey key
        ? $"{Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(key.PartitionKey))}/"
        : null;
    private static bool Matches(DocumentKey key, DocumentQuery query) => query?.DocumentKey is not DocumentKey match || query.Filter switch { DocumentKeyFilter.FullMatch => key == match, DocumentKeyFilter.RowKeyPrefixMatch => key.PartitionKey == match.PartitionKey && key.RowKey.StartsWith(match.RowKey ?? string.Empty, StringComparison.Ordinal), DocumentKeyFilter.RowKeySuffixMatch => key.PartitionKey == match.PartitionKey && key.RowKey.EndsWith(match.RowKey ?? string.Empty, StringComparison.Ordinal), _ => true };
    private static string CreateToken(string operation, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, string native) { var result = DocumentContinuationTokenSerializer.Serialize(new() { Provider = type.Value, QueryHash = DocumentQueryHash.Compute(operation, type, query, query?.Take ?? 100), VisibilityCutoff = visibilityCutoff, NativeToken = native }); return result.IsSuccess ? result.Value : null; }
    private static string RebindToken(string operation, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, string token) => string.IsNullOrWhiteSpace(token) ? null : CreateToken(operation, type, query, visibilityCutoff, ReadNativeToken(token));
    private static string ReadNativeToken(string token) { if (string.IsNullOrWhiteSpace(token)) return null; var result = DocumentContinuationTokenSerializer.Deserialize(token); return result.IsSuccess ? result.Value.NativeToken : null; }
    private static string NormalizePrefix(string value) { if (string.IsNullOrWhiteSpace(value)) return string.Empty; var normalized = value.Trim().ToLowerInvariant(); if (!normalized.All(x => char.IsAsciiLetterOrDigit(x) || x == '-')) throw new ArgumentException("Container prefix contains invalid characters.", nameof(value)); return normalized.EndsWith('-') ? normalized : normalized + "-"; }
    private static string NormalizeClientName(string value) => string.IsNullOrWhiteSpace(value) ? "default" : value.Trim().ToLowerInvariant();
}
