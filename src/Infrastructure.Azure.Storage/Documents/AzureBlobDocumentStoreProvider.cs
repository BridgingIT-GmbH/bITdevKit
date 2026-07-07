// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using global::Azure.Storage;
using global::Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Implements <see cref="IDocumentStoreProvider" /> using Azure Blob Storage.
/// </summary>
/// <example>
/// <code>
/// var provider = new AzureBlobDocumentStoreProvider(loggerFactory, blobServiceClient);
/// await provider.UpsertResultAsync(new DocumentKey("people", "42"), person, cancellationToken);
/// </code>
/// </example>
public class AzureBlobDocumentStoreProvider : IDocumentStoreProvider
{
    private const string ProviderName = "azure-blob";
    private readonly BlobServiceClient serviceClient;
    private readonly string containerNamePrefix;
    private readonly ISerializer serializer;
    private readonly DocumentStoreOptions options;
    private readonly string blobNameSeperator = "__";
    //private bool isLocal;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobDocumentStoreProvider" /> class from a connection string.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
    /// <param name="connectionString">The Azure Blob Storage connection string.</param>
    /// <param name="containerNamePrefix">The optional prefix added to generated container names.</param>
    /// <param name="clientOptions">The optional blob client options.</param>
    /// <param name="serializer">The optional serializer used for document payloads.</param>
    /// <param name="options">The optional document-store query safety options.</param>
    public AzureBlobDocumentStoreProvider( // TODO: add Options ctor
        ILoggerFactory loggerFactory,
        string connectionString,
        string containerNamePrefix = null,
        BlobClientOptions clientOptions = null,
        ISerializer serializer = null,
        DocumentStoreOptions options = null)
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureBlobDocumentStoreProvider>() ??
            NullLoggerFactory.Instance.CreateLogger<AzureBlobDocumentStoreProvider>();
        this.serviceClient = new BlobServiceClient(connectionString,
            clientOptions ?? new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_01_03));
        //this.isLocal = this.serviceClient.Uri.ToString().Contains("127.0.0.1");
        this.containerNamePrefix = containerNamePrefix;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
        this.options = options ?? new DocumentStoreOptions();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobDocumentStoreProvider" /> class from shared key credentials.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
    /// <param name="storageUri">The Azure Blob Storage account URI.</param>
    /// <param name="accountName">The storage account name.</param>
    /// <param name="storageAccountKey">The storage account key.</param>
    /// <param name="tableNamePrefix">The optional prefix added to generated container names.</param>
    /// <param name="clientOptions">The optional blob client options.</param>
    /// <param name="serializer">The optional serializer used for document payloads.</param>
    /// <param name="options">The optional document-store query safety options.</param>
    public AzureBlobDocumentStoreProvider(
        ILoggerFactory loggerFactory,
        string storageUri,
        string accountName,
        string storageAccountKey,
        string tableNamePrefix = null,
        BlobClientOptions clientOptions = null,
        ISerializer serializer = null,
        DocumentStoreOptions options = null)
    {
        EnsureArg.IsNotNullOrEmpty(storageUri, nameof(storageUri));
        EnsureArg.IsNotNullOrEmpty(accountName, nameof(accountName));
        EnsureArg.IsNotNullOrEmpty(storageAccountKey, nameof(storageAccountKey));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureBlobDocumentStoreProvider>() ??
            NullLoggerFactory.Instance.CreateLogger<AzureBlobDocumentStoreProvider>();
        this.serviceClient = new BlobServiceClient(new Uri(storageUri),
            new StorageSharedKeyCredential(accountName, storageAccountKey),
            clientOptions);
        //this.isLocal = this.serviceClient.Uri.ToString().Contains("127.0.0.0");
        this.containerNamePrefix = tableNamePrefix;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
        this.options = options ?? new DocumentStoreOptions();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobDocumentStoreProvider" /> class from an existing blob service client.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
    /// <param name="serviceClient">The Azure Blob Storage service client.</param>
    /// <param name="tableNamePrefix">The optional prefix added to generated container names.</param>
    /// <param name="serializer">The optional serializer used for document payloads.</param>
    /// <param name="options">The optional document-store query safety options.</param>
    public AzureBlobDocumentStoreProvider(
        ILoggerFactory loggerFactory,
        BlobServiceClient serviceClient,
        string tableNamePrefix = null,
        ISerializer serializer = null,
        DocumentStoreOptions options = null)
    {
        EnsureArg.IsNotNull(serviceClient, nameof(serviceClient));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureBlobDocumentStoreProvider>() ??
            NullLoggerFactory.Instance.CreateLogger<AzureBlobDocumentStoreProvider>();
        this.serviceClient = serviceClient;
        //this.isLocal = this.serviceClient.Uri.ToString().Contains("127.0.0.0");
        this.containerNamePrefix = tableNamePrefix;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
        this.options = options ?? new DocumentStoreOptions();
    }

    /// <summary>
    /// Gets the logger used by the provider.
    /// </summary>
    protected ILogger<AzureBlobDocumentStoreProvider> Logger { get; }

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedClientSide,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedEfficiently,
        SupportsContinuationPaging = true,
        SupportsServerSideCount = false,
        SupportsKeyOnlyProjection = true
    };

    /// <inheritdoc />
    public async Task<Result<T>> GetResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var keyValidation = ValidateBlobDocumentKey(documentKey);
        if (keyValidation.IsFailure)
        {
            return Result<T>.Failure(keyValidation);
        }

        try
        {
            var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
            var blobClient = containerClient.GetBlobClient(this.CreateBlobName(documentKey));
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result<T>.Failure(new DocumentStoreNotFoundError($"Document '{documentKey.PartitionKey}/{documentKey.RowKey}' was not found."));
            }

            await using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
            var entity = this.serializer.Deserialize<T>(stream);
            return entity is null
                ? Result<T>.Failure(new DocumentStoreNotFoundError($"Document '{documentKey.PartitionKey}/{documentKey.RowKey}' was not found."))
                : Result<T>.Success(entity);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<T>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentPage<T>>> FindPageResultAsync<T>(DocumentQuery query, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var validation = DocumentQueryValidator.ValidatePage<T>("find", ProviderName, query, this.Capabilities, this.options);
        if (validation.IsFailure)
        {
            return Result<DocumentPage<T>>.Failure(validation);
        }

        var keyValidation = ValidateBlobDocumentQuery(query);
        if (keyValidation.IsFailure)
        {
            return Result<DocumentPage<T>>.Failure(keyValidation);
        }

        try
        {
            var page = await this.ReadBlobKeyPageAsync<T>(
                query,
                validation.Value.ContinuationToken?.NativeToken,
                validation.Value.Take,
                cancellationToken);
            var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
            var items = new List<T>();
            foreach (var key in page.Items)
            {
                var item = await this.ReadBlobAsync<T>(containerClient, key, cancellationToken);
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            return Result<DocumentPage<T>>.Success(new DocumentPage<T>
            {
                Items = items,
                ContinuationToken = CreateContinuationToken(validation.Value.QueryHash, page.ContinuationToken)
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentPage<T>>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentKeyPage>> ListPageResultAsync<T>(DocumentQuery query, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var validation = DocumentQueryValidator.ValidatePage<T>("list", ProviderName, query, this.Capabilities, this.options);
        if (validation.IsFailure)
        {
            return Result<DocumentKeyPage>.Failure(validation);
        }

        var keyValidation = ValidateBlobDocumentQuery(query);
        if (keyValidation.IsFailure)
        {
            return Result<DocumentKeyPage>.Failure(keyValidation);
        }

        try
        {
            var page = await this.ReadBlobKeyPageAsync<T>(
                query,
                validation.Value.ContinuationToken?.NativeToken,
                validation.Value.Take,
                cancellationToken);

            return Result<DocumentKeyPage>.Success(new DocumentKeyPage
            {
                Items = page.Items.ToList(),
                ContinuationToken = CreateContinuationToken(validation.Value.QueryHash, page.ContinuationToken)
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<DocumentKeyPage>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<long>> CountResultAsync<T>(DocumentCountQuery query, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var validation = DocumentQueryValidator.ValidateCount<T>("count", query, this.Capabilities, this.options);
        if (validation.IsFailure)
        {
            return Result<long>.Failure(validation);
        }

        var keyValidation = ValidateBlobDocumentCountQuery(query);
        if (keyValidation.IsFailure)
        {
            return Result<long>.Failure(keyValidation);
        }

        try { return Result<long>.Success(await this.CountKeysAsync<T>(query, cancellationToken)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<long>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var keyValidation = ValidateBlobDocumentKey(documentKey);
        if (keyValidation.IsFailure)
        {
            return Result<bool>.Failure()
                .WithErrors(keyValidation.Errors);
        }

        try
        {
            var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
            return Result<bool>.Success(await containerClient.GetBlobClient(this.CreateBlobName(documentKey)).ExistsAsync(cancellationToken));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<bool>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var keyValidation = ValidateBlobDocumentKey(documentKey);
        if (keyValidation.IsFailure)
        {
            return keyValidation;
        }

        try { await this.UpsertAsync(documentKey, entity, cancellationToken); return Result.Success(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (entities is null)
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("Document entities must not be null."));
        }

        var materialized = entities.SafeNull().ToList();
        foreach (var (documentKey, _) in materialized)
        {
            var keyValidation = ValidateBlobDocumentKey(documentKey);
            if (keyValidation.IsFailure)
            {
                return keyValidation;
            }
        }

        try { await this.UpsertAsync(materialized, cancellationToken); return Result.Success(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var keyValidation = ValidateBlobDocumentKey(documentKey);
        if (keyValidation.IsFailure)
        {
            return keyValidation;
        }

        try { await this.DeleteAsync<T>(documentKey, cancellationToken); return Result.Success(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <summary>
    ///     Inserts or updates an entity of type T in the document store
    /// </summary>
    private async Task UpsertAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
        var blobName = $"{documentKey.PartitionKey}{this.blobNameSeperator}{documentKey.RowKey}";
        var blobClient = containerClient.GetBlobClient(blobName);

        using var stream = new MemoryStream();
        this.serializer.Serialize(entity, stream);
        stream.Position = 0;
        //TODO: where to place the ContentHash? >> HashHelper.Compute(entity);

        await blobClient.UploadAsync(stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = ContentType.JSON.MimeType(),
                    ContentEncoding = Encoding.UTF8.ToString()
                }
            },
            cancellationToken);

        //if (!this.isLocal)
        //{
        //    // WARN: currently azurite (local) does not support index tags https://github.com/Azure/Azurite/issues/647
        //    await blobClient.SetTagsAsync(
        //        new Dictionary<string, string>
        //        {
        //            [nameof(IDocumentEntity.PartitionKey)] = partitionKey,
        //            [nameof(IDocumentEntity.RowKey)] = rowKey
        //        },
        //        cancellationToken: cancellationToken);
        //}
    }

    /// <summary>
    ///     Inserts or updates multiple entities of type T in the document store
    /// </summary>
    private async Task UpsertAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entities, nameof(entities));

        foreach (var (documentKey, entity) in entities)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            await this.UpsertAsync(documentKey, entity, cancellationToken);
        }
    }

    /// <summary>
    ///     Deletes an entity of type T with the specified whole partitionKey and whole rowKey from the document store
    /// </summary>
    private async Task DeleteAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
        var blobName = $"{documentKey.PartitionKey}{this.blobNameSeperator}{documentKey.RowKey}";

        await containerClient.DeleteBlobIfExistsAsync(blobName, cancellationToken: cancellationToken);
    }

    private async Task<BlobContainerClient> GetBlobContainerClientAsync<T>(CancellationToken cancellationToken)
        where T : class, new()
    {
        var containerClient = this.serviceClient.GetBlobContainerClient(this.GetContainerName<T>());
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        return containerClient;
    }

    private async Task<T> ReadBlobAsync<T>(
        BlobContainerClient containerClient,
        DocumentKey documentKey,
        CancellationToken cancellationToken)
        where T : class, new()
    {
        var blobClient = containerClient.GetBlobClient(this.CreateBlobName(documentKey));
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        await using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        return this.serializer.Deserialize<T>(stream);
    }

    private string GetContainerName<T>()
    {
        return $"{this.containerNamePrefix}-{typeof(T).Name}".ToLowerInvariant().Trim('-').TruncateLeft(63);
    }

    private async Task<DocumentBlobKeyPage> ReadBlobKeyPageAsync<T>(
        DocumentQuery query,
        string nativeToken,
        int take,
        CancellationToken cancellationToken)
        where T : class, new()
    {
        query ??= new DocumentQuery();

        if (query.DocumentKey is { } key && query.Filter == DocumentKeyFilter.FullMatch)
        {
            var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
            var blobName = this.CreateBlobName(key);
            return await containerClient.GetBlobClient(blobName).ExistsAsync(cancellationToken)
                ? new DocumentBlobKeyPage([key], null)
                : new DocumentBlobKeyPage([], null);
        }

        var page = await this.ReadBlobNamePageAsync<T>(
            query.DocumentKey,
            query.Filter,
            nativeToken,
            take,
            cancellationToken);

        return new DocumentBlobKeyPage(page.Names.Select(this.ParseDocumentKey).Where(e => e.HasValue).Select(e => e.Value).ToList(), page.ContinuationToken);
    }

    private async Task<long> CountKeysAsync<T>(DocumentCountQuery query, CancellationToken cancellationToken)
        where T : class, new()
    {
        query ??= new DocumentCountQuery();

        if (query.DocumentKey is { } key && query.Filter == DocumentKeyFilter.FullMatch)
        {
            var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
            return await containerClient.GetBlobClient(this.CreateBlobName(key)).ExistsAsync(cancellationToken) ? 1 : 0;
        }

        var count = 0L;
        string nativeToken = null;
        do
        {
            var page = await this.ReadBlobNamePageAsync<T>(
                query.DocumentKey,
                query.Filter,
                nativeToken,
                this.options.MaxTake,
                cancellationToken);

            count += page.Names.Count;
            nativeToken = page.ContinuationToken;
        }
        while (!string.IsNullOrWhiteSpace(nativeToken));

        return count;
    }

    private async Task<DocumentBlobNamePage> ReadBlobNamePageAsync<T>(
        DocumentKey? documentKey,
        DocumentKeyFilter filter,
        string nativeToken,
        int take,
        CancellationToken cancellationToken)
        where T : class, new()
    {
        var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
        var options = documentKey is { } key && filter == DocumentKeyFilter.RowKeyPrefixMatch
            ? new GetBlobsOptions { Prefix = this.CreateBlobName(key) }
            : null;
        var pages = options is null
            ? containerClient.GetBlobsAsync(cancellationToken: cancellationToken).AsPages(nativeToken, take)
            : containerClient.GetBlobsAsync(options, cancellationToken: cancellationToken).AsPages(nativeToken, take);

        await foreach (var page in pages.WithCancellation(cancellationToken))
        {
            var names = page.Values.Select(e => e.Name);
            if (documentKey is { } suffixKey && filter == DocumentKeyFilter.RowKeySuffixMatch)
            {
                names = names.Where(name =>
                {
                    var key = this.ParseDocumentKey(name);
                    return key.HasValue &&
                        key.Value.PartitionKey == suffixKey.PartitionKey &&
                        key.Value.RowKey.EndsWith(suffixKey.RowKey);
                });
            }

            return new DocumentBlobNamePage(names.ToList(), page.ContinuationToken);
        }

        return new DocumentBlobNamePage([], null);
    }

    private string CreateBlobName(DocumentKey documentKey)
    {
        return $"{documentKey.PartitionKey}{this.blobNameSeperator}{documentKey.RowKey}";
    }

    private DocumentKey? ParseDocumentKey(string blobName)
    {
        var separatorIndex = blobName.IndexOf(this.blobNameSeperator, StringComparison.Ordinal);
        return separatorIndex < 0
            ? null
            : new DocumentKey(
                blobName[..separatorIndex],
                blobName[(separatorIndex + this.blobNameSeperator.Length)..]);
    }

    private Result ValidateBlobDocumentQuery(DocumentQuery query)
    {
        return query?.DocumentKey is { } documentKey
            ? ValidateBlobDocumentKey(documentKey)
            : Result.Success();
    }

    private Result ValidateBlobDocumentCountQuery(DocumentCountQuery query)
    {
        return query?.DocumentKey is { } documentKey
            ? ValidateBlobDocumentKey(documentKey)
            : Result.Success();
    }

    private Result ValidateBlobDocumentKey(DocumentKey documentKey)
    {
        if (documentKey.PartitionKey?.Contains(this.blobNameSeperator, StringComparison.Ordinal) == true ||
            documentKey.RowKey?.Contains(this.blobNameSeperator, StringComparison.Ordinal) == true)
        {
            return Result.Failure(new DocumentStoreInvalidQueryError(
                $"Azure Blob document keys must not contain the reserved separator '{this.blobNameSeperator}'."));
        }

        return Result.Success();
    }

    private static string CreateContinuationToken(string queryHash, string nativeToken)
    {
        if (string.IsNullOrWhiteSpace(nativeToken))
        {
            return null;
        }

        var result = DocumentContinuationTokenSerializer.Serialize(new DocumentContinuationToken
        {
            Provider = ProviderName,
            QueryHash = queryHash,
            NativeToken = nativeToken
        });

        return result.IsSuccess ? result.Value : null;
    }

    private sealed record DocumentBlobKeyPage(IReadOnlyCollection<DocumentKey> Items, string ContinuationToken);

    private sealed record DocumentBlobNamePage(IReadOnlyCollection<string> Names, string ContinuationToken);
}
