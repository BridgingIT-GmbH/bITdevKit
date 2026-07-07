// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

/// <summary>
/// Implements <see cref="IDocumentStoreProvider" /> using Azure Table Storage.
/// </summary>
/// <example>
/// <code>
/// var provider = new AzureTableDocumentStoreProvider(loggerFactory, tableServiceClient);
/// await provider.UpsertResultAsync(new DocumentKey("people", "42"), person, cancellationToken);
/// </code>
/// </example>
public class AzureTableDocumentStoreProvider : IDocumentStoreProvider
{
    private const string ProviderName = "azure-table";
    private readonly TableServiceClient serviceClient;
    private readonly string tableNamePrefix;
    private readonly DocumentStoreOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTableDocumentStoreProvider" /> class from a connection string.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
    /// <param name="connectionString">The Azure Table Storage connection string.</param>
    /// <param name="tableNamePrefix">The optional prefix added to generated table names.</param>
    /// <param name="clientOptions">The optional table client options.</param>
    /// <param name="options">The optional document-store query safety options.</param>
    public AzureTableDocumentStoreProvider( // TODO: add Options ctor
        ILoggerFactory loggerFactory,
        string connectionString,
        string tableNamePrefix = null,
        TableClientOptions clientOptions = null,
        DocumentStoreOptions options = null)
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureTableDocumentStoreProvider>() ??
            NullLoggerFactory.Instance.CreateLogger<AzureTableDocumentStoreProvider>();
        this.serviceClient = new TableServiceClient(connectionString, clientOptions);
        this.tableNamePrefix = tableNamePrefix;
        this.options = options ?? new DocumentStoreOptions();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTableDocumentStoreProvider" /> class from shared key credentials.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
    /// <param name="storageUri">The Azure Table Storage account URI.</param>
    /// <param name="accountName">The storage account name.</param>
    /// <param name="storageAccountKey">The storage account key.</param>
    /// <param name="tableNamePrefix">The optional prefix added to generated table names.</param>
    /// <param name="clientOptions">The optional table client options.</param>
    /// <param name="options">The optional document-store query safety options.</param>
    public AzureTableDocumentStoreProvider(
        ILoggerFactory loggerFactory,
        string storageUri,
        string accountName,
        string storageAccountKey,
        string tableNamePrefix = null,
        TableClientOptions clientOptions = null,
        DocumentStoreOptions options = null)
    {
        EnsureArg.IsNotNullOrEmpty(storageUri, nameof(storageUri));
        EnsureArg.IsNotNullOrEmpty(accountName, nameof(accountName));
        EnsureArg.IsNotNullOrEmpty(storageAccountKey, nameof(storageAccountKey));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureTableDocumentStoreProvider>() ??
            NullLoggerFactory.Instance.CreateLogger<AzureTableDocumentStoreProvider>();
        this.serviceClient = new TableServiceClient(new Uri(storageUri),
            new TableSharedKeyCredential(accountName, storageAccountKey),
            clientOptions);
        this.tableNamePrefix = tableNamePrefix;
        this.options = options ?? new DocumentStoreOptions();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTableDocumentStoreProvider" /> class from an existing table service client.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
    /// <param name="serviceClient">The Azure Table Storage service client.</param>
    /// <param name="tableNamePrefix">The optional prefix added to generated table names.</param>
    /// <param name="options">The optional document-store query safety options.</param>
    public AzureTableDocumentStoreProvider(
        ILoggerFactory loggerFactory,
        TableServiceClient serviceClient,
        string tableNamePrefix = null,
        DocumentStoreOptions options = null)
    {
        EnsureArg.IsNotNull(serviceClient, nameof(serviceClient));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureTableDocumentStoreProvider>() ??
            NullLoggerFactory.Instance.CreateLogger<AzureTableDocumentStoreProvider>();
        this.serviceClient = serviceClient;
        this.tableNamePrefix = tableNamePrefix;
        this.options = options ?? new DocumentStoreOptions();
    }

    /// <summary>
    /// Gets the logger used by the provider.
    /// </summary>
    protected ILogger<AzureTableDocumentStoreProvider> Logger { get; }

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeySuffixMatch = DocumentQuerySupport.Unsupported,
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
        try
        {
            var key = NormalizeDocumentKey(documentKey);
            var tableName = this.GetTableName<T>();
            this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
            var tableClient = this.serviceClient.GetTableClient(tableName);
            var response = await tableClient.GetEntityIfExistsAsync<TableEntity>(
                key.PartitionKey,
                key.RowKey,
                cancellationToken: cancellationToken);
            var entity = response.HasValue ? response.Value.FromTableEntity<T>() : null;

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

        try
        {
            var page = await this.ReadTablePageAsync<T>(
                query,
                validation.Value.ContinuationToken?.NativeToken,
                validation.Value.Take,
                cancellationToken);

            return Result<DocumentPage<T>>.Success(new DocumentPage<T>
            {
                Items = page.Items.Select(e => e.FromTableEntity<T>()).Where(e => e is not null).ToList(),
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

        try
        {
            var page = await this.ReadTablePageAsync<T>(
                query,
                validation.Value.ContinuationToken?.NativeToken,
                validation.Value.Take,
                cancellationToken);

            return Result<DocumentKeyPage>.Success(new DocumentKeyPage
            {
                Items = page.Items.Select(e => new DocumentKey(e.PartitionKey, e.RowKey)).ToList(),
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

        try
        {
            return Result<long>.Success(await this.CountKeysAsync<T>(query, cancellationToken));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<long>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        try
        {
            var key = NormalizeDocumentKey(documentKey);
            var tableName = this.GetTableName<T>();
            this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
            var tableClient = this.serviceClient.GetTableClient(tableName);

            var response = await tableClient.GetEntityIfExistsAsync<TableEntity>(
                key.PartitionKey,
                key.RowKey,
                cancellationToken: cancellationToken);

            return Result<bool>.Success(response.HasValue);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result<bool>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
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
        try { await this.UpsertAsync(entities, cancellationToken); return Result.Success(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Result.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex)); }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
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
        documentKey = NormalizeDocumentKey(documentKey);

        var tableName = this.GetTableName<T>();
        this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
        var tableEntity = entity.ToTableEntity(documentKey.PartitionKey, documentKey.RowKey);
        tableEntity.Add("ContentHash", HashHelper.Compute(entity));
        var tableClient = this.serviceClient.GetTableClient(tableName);

        await tableClient.UpsertEntityAsync(tableEntity, cancellationToken: cancellationToken);
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
        documentKey = NormalizeDocumentKey(documentKey);

        var tableName = this.GetTableName<T>();
        this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
        var tableClient = this.serviceClient.GetTableClient(tableName);

        await tableClient.DeleteEntityAsync(documentKey.PartitionKey,
            documentKey.RowKey,
            cancellationToken: cancellationToken);
    }

    private string GetTableName<T>()
    {
        return (this.tableNamePrefix + typeof(T).Name).ToLowerInvariant().TruncateLeft(63);
    }

    private async Task<DocumentTablePage> ReadTablePageAsync<T>(
        DocumentQuery query,
        string nativeToken,
        int take,
        CancellationToken cancellationToken)
        where T : class, new()
    {
        var tableName = this.GetTableName<T>();
        this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
        var tableClient = this.serviceClient.GetTableClient(tableName);
        var queryFilter = CreateQueryFilter(query);
        var pages = string.IsNullOrWhiteSpace(queryFilter)
            ? tableClient.QueryAsync<TableEntity>(maxPerPage: take, cancellationToken: cancellationToken)
            : tableClient.QueryAsync<TableEntity>(queryFilter, maxPerPage: take, cancellationToken: cancellationToken);

        await foreach (var page in pages.AsPages(nativeToken, take).WithCancellation(cancellationToken))
        {
            return new DocumentTablePage(page.Values.ToList(), page.ContinuationToken);
        }

        return new DocumentTablePage([], null);
    }

    private async Task<long> CountKeysAsync<T>(DocumentCountQuery query, CancellationToken cancellationToken)
        where T : class, new()
    {
        var tableName = this.GetTableName<T>();
        this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
        var tableClient = this.serviceClient.GetTableClient(tableName);
        var queryFilter = CreateQueryFilter(query);
        var pages = string.IsNullOrWhiteSpace(queryFilter)
            ? tableClient.QueryAsync<TableEntity>(cancellationToken: cancellationToken)
            : tableClient.QueryAsync<TableEntity>(queryFilter, cancellationToken: cancellationToken);
        var count = 0L;

        await foreach (var page in pages.AsPages().WithCancellation(cancellationToken))
        {
            count += page.Values.Count;
        }

        return count;
    }

    private static string CreateQueryFilter(DocumentQuery query)
    {
        query ??= new DocumentQuery();

        return query.DocumentKey is null ? null : CreateQueryFilter(query.DocumentKey.Value, query.Filter);
    }

    private static string CreateQueryFilter(DocumentCountQuery query)
    {
        query ??= new DocumentCountQuery();

        return query.DocumentKey is null ? null : CreateQueryFilter(query.DocumentKey.Value, query.Filter);
    }

    private static string CreateQueryFilter(DocumentKey documentKey, DocumentKeyFilter filter)
    {
        documentKey = NormalizeDocumentKey(documentKey, allowEmptyRowKey: filter == DocumentKeyFilter.RowKeyPrefixMatch);
        var partitionKey = EscapeODataStringValue(documentKey.PartitionKey);
        var rowKey = EscapeODataStringValue(documentKey.RowKey);

        if (filter == DocumentKeyFilter.FullMatch)
        {
            return $"PartitionKey eq '{partitionKey}' and RowKey eq '{rowKey}'";
        }

        if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
        {
            if (documentKey.RowKey.IsNullOrEmpty())
            {
                return $"PartitionKey eq '{partitionKey}'";
            }

            var lastChar = documentKey.RowKey[documentKey.RowKey.Length - 1];
            var nextChar = (char)(lastChar + 1);
            var rowKeyNext = documentKey.RowKey.Substring(0, documentKey.RowKey.Length - 1) + nextChar;

            return $"PartitionKey eq '{partitionKey}' and RowKey ge '{rowKey}' and RowKey lt '{EscapeODataStringValue(rowKeyNext)}'";
        }

        if (filter == DocumentKeyFilter.RowKeySuffixMatch)
        {
            throw new NotSupportedException();
        }

        return default;
    }

    private static DocumentKey NormalizeDocumentKey(DocumentKey documentKey, bool allowEmptyRowKey = false) =>
        new(
            NormalizeKey(documentKey.PartitionKey),
            allowEmptyRowKey && documentKey.RowKey.IsNullOrEmpty()
                ? documentKey.RowKey
                : NormalizeKey(documentKey.RowKey));

    private static string NormalizeKey(string key)
    {
        if (key is null)
        {
            return null;
        }

        var builder = new StringBuilder(key.Length);
        foreach (var character in key)
        {
            if (!IsIllegalTableKeyCharacter(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool IsIllegalTableKeyCharacter(char character) =>
        character is '/' or '\\' or '#' or '?' or '\'' ||
        char.IsControl(character);

    private static string EscapeODataStringValue(string value) =>
        value?.Replace("'", "''", StringComparison.Ordinal);

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

    private sealed record DocumentTablePage(IReadOnlyCollection<TableEntity> Items, string ContinuationToken);
}
