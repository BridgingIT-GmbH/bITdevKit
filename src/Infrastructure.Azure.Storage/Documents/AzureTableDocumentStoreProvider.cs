// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.Azure;

using global::Azure;
using System.Globalization;
using System.Text;
using System.Text.Json;

/// <summary>Persists chunked serialized documents in deterministic, provider-managed Azure Tables.</summary>
/// <remarks>
/// The provider requires only storage-account connectivity. It derives one table name per normalized client name and
/// document type identity, creates or reuses the table through an asynchronous initialization gate, and never deletes it
/// during disposal. Payloads are split into deterministic 60 KiB properties and the complete entity is validated against
/// Azure Table's 1 MiB entity limit before submission.
/// </remarks>
/// <example><code>var provider = new AzureTableDocumentStoreProvider(loggerFactory, tableServiceClient);</code></example>
public class AzureTableDocumentStoreProvider : IDocumentStoreProvider, IDocumentStoreRetentionProvider
{
    private static readonly int ChunkSize = checked((int)ByteSize.Kilobytes(60));
    private static readonly long MaximumEntitySize = ByteSize.Megabytes(1);
    private readonly TableServiceClient serviceClient;
    private readonly string prefix;
    private readonly string clientName;
    private readonly DocumentStoreOptions options;
    private readonly Dictionary<string, AsyncInitializationGate> gates = [];
    private readonly object gateLock = new();

    /// <summary>Initializes a provider from an existing storage-account service client.</summary>
    /// <param name="loggerFactory">The optional logger factory used for typed operational logging.</param>
    /// <param name="serviceClient">The account-level Table service client; callers do not create or pass table clients.</param>
    /// <param name="tableNamePrefix">An optional validated prefix prepended to deterministic table names.</param>
    /// <param name="options">Optional paging and stored-document size safety limits.</param>
    /// <param name="clientName">The normalized named Document Storage client identity used for resource isolation.</param>
    /// <example><code>var provider = new AzureTableDocumentStoreProvider(loggerFactory, tableServiceClient, clientName: "archive");</code></example>
    public AzureTableDocumentStoreProvider(ILoggerFactory loggerFactory, TableServiceClient serviceClient, string tableNamePrefix = null, DocumentStoreOptions options = null, string clientName = "default")
    {
        this.serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
        this.prefix = NormalizePrefix(tableNamePrefix);
        this.clientName = NormalizeClientName(clientName);
        this.options = options ?? new();
        this.Logger = loggerFactory?.CreateLogger<AzureTableDocumentStoreProvider>() ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<AzureTableDocumentStoreProvider>();
    }

    /// <summary>Initializes a provider from a storage-account connection string.</summary>
    /// <param name="loggerFactory">The optional logger factory used for typed operational logging.</param>
    /// <param name="connectionString">The existing Azure Storage account connection string.</param>
    /// <param name="tableNamePrefix">An optional validated prefix prepended to deterministic table names.</param>
    /// <param name="clientOptions">Optional Azure Table transport client options.</param>
    /// <param name="options">Optional paging and stored-document size safety limits.</param>
    /// <param name="clientName">The normalized named Document Storage client identity used for resource isolation.</param>
    /// <example><code>var provider = new AzureTableDocumentStoreProvider(loggerFactory, connectionString, clientName: "archive");</code></example>
    public AzureTableDocumentStoreProvider(ILoggerFactory loggerFactory, string connectionString, string tableNamePrefix = null, TableClientOptions clientOptions = null, DocumentStoreOptions options = null, string clientName = "default")
        : this(loggerFactory, new TableServiceClient(connectionString, clientOptions), tableNamePrefix, options, clientName) { }

    /// <summary>Gets the typed provider logger used for non-sensitive resource and operation diagnostics.</summary>
    /// <example><code>protected ILogger ProviderLogger =&gt; this.Logger;</code></example>
    protected ILogger<AzureTableDocumentStoreProvider> Logger { get; }

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
        SupportsRetention = true,
        MaxStoredDocumentSize = ByteSize.Megabytes(1)
    };

    /// <inheritdoc />
    public async Task<Result<StoredDocument>> GetAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            var table = await this.GetTableAsync(type, cancellationToken);
            var response = await table.GetEntityAsync<TableEntity>(Encode(key.PartitionKey), Encode(key.RowKey), cancellationToken: cancellationToken);
            var document = Map(response.Value);
            return document.ExpiresAt is not null && document.ExpiresAt <= visibilityCutoff ? Result<StoredDocument>.Failure(new DocumentStoreNotFoundError()) : Result<StoredDocument>.Success(document);
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return Result<StoredDocument>.Failure(new DocumentStoreNotFoundError()); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<StoredDocument>.Failure(new DocumentStoreProviderError("Azure Table document read failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<StoredDocumentPage>> FindPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            var table = await this.GetTableAsync(type, cancellationToken);
            var take = query?.Take ?? this.options.DefaultTake;
            var filter = query?.DocumentKey is DocumentKey key
                ? query.Filter == DocumentKeyFilter.FullMatch
                    ? $"PartitionKey eq '{Encode(key.PartitionKey)}' and RowKey eq '{Encode(key.RowKey)}'"
                    : $"PartitionKey eq '{Encode(key.PartitionKey)}'"
                : null;
            var native = ReadNativeToken(query?.ContinuationToken);
            var items = new List<StoredDocument>(take);
            string next = null;
            await foreach (var page in table.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).AsPages(native, take).WithCancellation(cancellationToken))
            {
                items.AddRange(page.Values.Select(Map).Where(x => Matches(x.Key, query) && (x.ExpiresAt is null || x.ExpiresAt > visibilityCutoff)));
                next = page.ContinuationToken;
                if (items.Count >= take || string.IsNullOrWhiteSpace(next)) break;
            }
            return Result<StoredDocumentPage>.Success(new() { Items = items.Take(take).ToArray(), ContinuationToken = string.IsNullOrWhiteSpace(next) ? null : CreateToken("find", type, query, visibilityCutoff, next) });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<StoredDocumentPage>.Failure(new DocumentStoreProviderError("Azure Table document page failed.", ex)); }
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
        var result = await this.GetAsync(type, key, visibilityCutoff, cancellationToken);
        return result.IsSuccess ? Result<bool>.Success(true) : result.Errors.Any(x => x is DocumentStoreNotFoundError) ? Result<bool>.Success(false) : result.Wrap<bool>();
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpsertAsync(DocumentTypeIdentity type, StoredDocumentWrite write, CancellationToken cancellationToken = default)
    {
        try
        {
            var table = await this.GetTableAsync(type, cancellationToken);
            StoredDocument current = null;
            var physical = await this.GetPhysicalAsync(table, write.Key, cancellationToken);
            if (physical.IsSuccess) current = physical.Value;
            if (write.Options.CreateOnly && current is not null) return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("A physical document already exists."));
            var now = DateTimeOffset.UtcNow;
            var entity = ToEntity(write, current, now);
            if (EstimateEntitySize(entity) > MaximumEntitySize)
            {
                return Result<DocumentInfo>.Failure(new DocumentStoreSizeLimitError("The complete Azure Table entity exceeds the 1 MiB service limit."));
            }
            if (current is null)
            {
                await table.AddEntityAsync(entity, cancellationToken);
            }
            else
            {
                var etag = string.IsNullOrWhiteSpace(write.Options.IfMatchETag) ? ETag.All : new ETag(write.Options.IfMatchETag);
                await table.UpdateEntityAsync(entity, etag, TableUpdateMode.Replace, cancellationToken);
            }
            var saved = await table.GetEntityAsync<TableEntity>(entity.PartitionKey, entity.RowKey, cancellationToken: cancellationToken);
            return Result<DocumentInfo>.Success(ToInfo(Map(saved.Value)));
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412) { return Result<DocumentInfo>.Failure(new DocumentStoreConflictError(ex.Message)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentInfo>.Failure(new DocumentStoreProviderError("Azure Table document write failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentTypeIdentity type, DocumentPropertiesUpdate update, DateTimeOffset? resolvedExpiresAt, bool preserveExpiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var table = await this.GetTableAsync(type, cancellationToken);
            var response = await table.GetEntityAsync<TableEntity>(Encode(update.Key.PartitionKey), Encode(update.Key.RowKey), cancellationToken: cancellationToken);
            var entity = response.Value;
            var current = Map(entity);
            entity["bdk_properties"] = EncodeBag(update.Properties ?? current.Properties);
            entity["bdk_modified_at"] = DateTimeOffset.UtcNow;
            if (!preserveExpiration) entity["bdk_expires_at"] = resolvedExpiresAt;
            if (EstimateEntitySize(entity) > MaximumEntitySize)
            {
                return Result<DocumentInfo>.Failure(new DocumentStoreSizeLimitError("The complete Azure Table entity exceeds the 1 MiB service limit."));
            }
            var etag = string.IsNullOrWhiteSpace(update.IfMatchETag) ? response.Value.ETag : new ETag(update.IfMatchETag);
            await table.UpdateEntityAsync(entity, etag, TableUpdateMode.Replace, cancellationToken);
            return Result<DocumentInfo>.Success(ToInfo(Map(entity)));
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return Result<DocumentInfo>.Failure(new DocumentStoreNotFoundError()); }
        catch (RequestFailedException ex) when (ex.Status == 412) { return Result<DocumentInfo>.Failure(new DocumentStoreConflictError(ex.Message)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentInfo>.Failure(new DocumentStoreProviderError("Azure Table metadata update failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(DocumentTypeIdentity type, DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var table = await this.GetTableAsync(type, cancellationToken);
            await table.DeleteEntityAsync(Encode(key.PartitionKey), Encode(key.RowKey), string.IsNullOrWhiteSpace(options?.IfMatchETag) ? ETag.All : new ETag(options.IfMatchETag), cancellationToken);
            return Result.Success();
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return Result.Success(); }
        catch (RequestFailedException ex) when (ex.Status == 412) { return Result.Failure(new DocumentStoreConflictError(ex.Message)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result.Failure(new DocumentStoreProviderError("Azure Table document delete failed.", ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentRetentionSweepResult>> SweepExpiredAsync(DocumentRetentionSweepRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var table = await this.GetTableAsync(request.DocumentType, cancellationToken);
            var filter = TableClient.CreateQueryFilter($"bdk_expires_at le {request.VisibilityCutoff}");
            var deleted = 0L;
            var deletedKeys = new List<DocumentKey>();
            var batches = 0;
            var hasMore = false;
            for (; batches < request.MaxBatches; batches++)
            {
                var candidates = new List<TableEntity>(request.BatchSize);
                await foreach (var entity in table.QueryAsync<TableEntity>(filter, request.BatchSize, cancellationToken: cancellationToken))
                {
                    candidates.Add(entity);
                    if (candidates.Count == request.BatchSize) break;
                }
                foreach (var entity in candidates)
                {
                    try
                    {
                        await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, entity.ETag, cancellationToken);
                        deleted++;
                        deletedKeys.Add(new((string)entity["bdk_partition_key"], (string)entity["bdk_row_key"]));
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
        catch (Exception ex) { return Result<DocumentRetentionSweepResult>.Failure(new DocumentStoreProviderError("Azure Table document retention failed.", ex)); }
    }

    private async Task<TableClient> GetTableAsync(DocumentTypeIdentity type, CancellationToken cancellationToken)
    {
        var name = $"{this.prefix}BdkDoc{HashHelper.ComputeSha256(this.clientName + "\n" + type.Value)[..32]}";
        var table = this.serviceClient.GetTableClient(name);
        AsyncInitializationGate gate;
        lock (this.gateLock) gate = this.gates.GetValueOrDefault(name) ?? (this.gates[name] = new());
        await gate.EnsureInitializedAsync(async ct => { await table.CreateIfNotExistsAsync(ct); }, cancellationToken);
        return table;
    }

    private async Task<Result<StoredDocument>> GetPhysicalAsync(TableClient table, DocumentKey key, CancellationToken cancellationToken)
    {
        try { return Result<StoredDocument>.Success(Map((await table.GetEntityAsync<TableEntity>(Encode(key.PartitionKey), Encode(key.RowKey), cancellationToken: cancellationToken)).Value)); }
        catch (RequestFailedException ex) when (ex.Status == 404) { return Result<StoredDocument>.Failure(new DocumentStoreNotFoundError()); }
    }

    private static TableEntity ToEntity(StoredDocumentWrite write, StoredDocument current, DateTimeOffset now)
    {
        var entity = new TableEntity(Encode(write.Key.PartitionKey), Encode(write.Key.RowKey))
        {
            ["bdk_partition_key"] = write.Key.PartitionKey, ["bdk_row_key"] = write.Key.RowKey,
            ["bdk_content_hash"] = write.ContentHash, ["bdk_stored_content_hash"] = write.StoredContentHash,
            ["bdk_created_at"] = current?.CreatedAt ?? now, ["bdk_modified_at"] = now,
            ["bdk_expires_at"] = write.PreserveExpiration ? current?.ExpiresAt : write.ExpiresAt,
            ["bdk_properties"] = EncodeBag(write.Properties ?? current?.Properties), ["bdk_transforms"] = EncodeBag(write.TransformMetadata),
            ["bdk_content_chunk_count"] = (write.Content.Length + ChunkSize - 1) / ChunkSize
        };
        for (var offset = 0; offset < write.Content.Length; offset += ChunkSize)
        {
            var length = Math.Min(ChunkSize, write.Content.Length - offset);
            entity[$"bdk_content_{offset / ChunkSize:0000}"] = write.Content.AsSpan(offset, length).ToArray();
        }
        return entity;
    }

    private static StoredDocument Map(TableEntity entity)
    {
        var chunks = Convert.ToInt32(entity.GetValueOrDefault("bdk_content_chunk_count") ?? 0, CultureInfo.InvariantCulture);
        using var stream = new MemoryStream();
        for (var index = 0; index < chunks; index++) { var bytes = (byte[])entity[$"bdk_content_{index:0000}"]; stream.Write(bytes); }
        return new() { Key = new((string)entity["bdk_partition_key"], (string)entity["bdk_row_key"]), Content = stream.ToArray(), ETag = entity.ETag.ToString(), ContentHash = entity.GetValueOrDefault("bdk_content_hash") as string, StoredContentHash = entity.GetValueOrDefault("bdk_stored_content_hash") as string, CreatedAt = (DateTimeOffset)entity["bdk_created_at"], LastModifiedAt = (DateTimeOffset)entity["bdk_modified_at"], ExpiresAt = entity.GetValueOrDefault("bdk_expires_at") as DateTimeOffset?, Properties = DecodeBag(entity.GetValueOrDefault("bdk_properties") as string), TransformMetadata = DecodeBag(entity.GetValueOrDefault("bdk_transforms") as string) };
    }
    private static DocumentInfo ToInfo(StoredDocument x) => new() { Key = x.Key, ETag = x.ETag, ContentHash = x.ContentHash, CreatedAt = x.CreatedAt, LastModifiedAt = x.LastModifiedAt, ExpiresAt = x.ExpiresAt, Properties = x.Properties.Clone() };
    private static string EncodeBag(PropertyBag bag) => Base64UrlHelper.Encode(JsonSerializer.SerializeToUtf8Bytes((bag ?? new()).ToDictionary(x => x.Key, x => PropertyBagScalarCodec.Encode(x.Value))));
    private static PropertyBag DecodeBag(string value) { if (string.IsNullOrWhiteSpace(value)) return new(); var values = JsonSerializer.Deserialize<Dictionary<string, string>>(Base64UrlHelper.Decode(value)); return new(values.ToDictionary(x => x.Key, x => PropertyBagScalarCodec.Decode(x.Value))); }
    private static string Encode(string value) => Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(value));
    private static bool Matches(DocumentKey key, DocumentQuery query) => query?.DocumentKey is not DocumentKey match || query.Filter switch { DocumentKeyFilter.FullMatch => key == match, DocumentKeyFilter.RowKeyPrefixMatch => key.PartitionKey == match.PartitionKey && key.RowKey.StartsWith(match.RowKey ?? string.Empty, StringComparison.Ordinal), DocumentKeyFilter.RowKeySuffixMatch => key.PartitionKey == match.PartitionKey && key.RowKey.EndsWith(match.RowKey ?? string.Empty, StringComparison.Ordinal), _ => true };
    private static string CreateToken(string operation, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, string native) { var result = DocumentContinuationTokenSerializer.Serialize(new() { Provider = type.Value, QueryHash = DocumentQueryHash.Compute(operation, type, query, query?.Take ?? 100), VisibilityCutoff = visibilityCutoff, NativeToken = native }); return result.IsSuccess ? result.Value : null; }
    private static string RebindToken(string operation, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, string token) => string.IsNullOrWhiteSpace(token) ? null : CreateToken(operation, type, query, visibilityCutoff, ReadNativeToken(token));
    private static string ReadNativeToken(string token) { if (string.IsNullOrWhiteSpace(token)) return null; var result = DocumentContinuationTokenSerializer.Deserialize(token); return result.IsSuccess ? result.Value.NativeToken : null; }
    private static string NormalizePrefix(string value) { if (string.IsNullOrWhiteSpace(value)) return string.Empty; var normalized = new string(value.Where(char.IsAsciiLetterOrDigit).ToArray()); if (normalized.Length > 20 || normalized.Length == 0) throw new ArgumentException("Table prefix is invalid.", nameof(value)); return char.ToUpperInvariant(normalized[0]) + normalized[1..]; }
    private static string NormalizeClientName(string value) => string.IsNullOrWhiteSpace(value) ? "default" : value.Trim().ToLowerInvariant();

    private static long EstimateEntitySize(TableEntity entity)
    {
        // Include conservative property framing so oversized replacements are rejected before provider I/O.
        var size = EstimateString(entity.PartitionKey) + EstimateString(entity.RowKey) + 64;
        foreach (var property in entity)
        {
            size += EstimateString(property.Key) + 16 + property.Value switch
            {
                null => 0,
                string value => EstimateString(value),
                byte[] value => value.LongLength,
                bool => sizeof(bool),
                int => sizeof(int),
                long => sizeof(long),
                double => sizeof(double),
                Guid => 16,
                DateTimeOffset => 16,
                DateTime => 8,
                _ => EstimateString(Convert.ToString(property.Value, CultureInfo.InvariantCulture))
            };
        }

        return size;
    }

    private static long EstimateString(string value) => value is null ? 0 : (long)value.Length * sizeof(char);
}
