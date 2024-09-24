// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Storage;

using Application.Storage;
using Common;
using global::Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class AzureTableDocumentStoreProvider : IDocumentStoreProvider
{
    private readonly TableServiceClient serviceClient;
    private readonly string tableNamePrefix;

    public AzureTableDocumentStoreProvider( // TODO: add Options ctor
        ILoggerFactory loggerFactory,
        string connectionString,
        string tableNamePrefix = null,
        TableClientOptions clientOptions = null)
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureTableDocumentStoreProvider>() ??
            NullLoggerFactory.Instance.CreateLogger<AzureTableDocumentStoreProvider>();
        this.serviceClient = new TableServiceClient(connectionString, clientOptions);
        this.tableNamePrefix = tableNamePrefix;
    }

    public AzureTableDocumentStoreProvider(
        ILoggerFactory loggerFactory,
        string storageUri,
        string accountName,
        string storageAccountKey,
        string tableNamePrefix = null,
        TableClientOptions clientOptions = null)
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
    }

    public AzureTableDocumentStoreProvider(
        ILoggerFactory loggerFactory,
        TableServiceClient serviceClient,
        string tableNamePrefix = null)
    {
        EnsureArg.IsNotNull(serviceClient, nameof(serviceClient));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureTableDocumentStoreProvider>() ??
            NullLoggerFactory.Instance.CreateLogger<AzureTableDocumentStoreProvider>();
        this.serviceClient = serviceClient;
        this.tableNamePrefix = tableNamePrefix;
    }

    protected ILogger<AzureTableDocumentStoreProvider> Logger { get; }

    /// <summary>
    ///     Retrieves entities of type T from document store asynchronously
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var tableName = this.GetTableName<T>();
        this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
        var tableClient = this.serviceClient.GetTableClient(tableName);
        var results = new HashSet<T>();

        await foreach (var page in tableClient.QueryAsync<TableEntity>(cancellationToken: cancellationToken)
                           .AsPages()
                           .WithCancellation(cancellationToken))
        {
            foreach (var tableEntity in page.Values)
            {
                var entity = tableEntity.FromTableEntity<T>();
                if (entity is not null)
                {
                    results.Add(entity);
                }
            }
        }

        return results;
    }

    /// <summary>
    ///     Retrieves entities of type T filtered by the whole partitionKey and whole rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.FindAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    /// <summary>
    ///     Searches for entities of type T by the whole partitionKey and startswith rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var tableName = this.GetTableName<T>();
        this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
        var tableClient = this.serviceClient.GetTableClient(tableName);
        var results = new HashSet<T>();
        var queryFilter = CreateQueryFilter(documentKey, filter);

        if (string.IsNullOrEmpty(queryFilter))
        {
            return results;
        }

        await foreach (var page in tableClient
                           .QueryAsync<TableEntity>(queryFilter, cancellationToken: cancellationToken)
                           .AsPages()
                           .WithCancellation(cancellationToken))
        {
            foreach (var tableEntity in page.Values)
            {
                var entity = tableEntity.FromTableEntity<T>();
                if (entity is not null)
                {
                    results.Add(entity);
                }
            }
        }

        return results;

        // TODO: refactor to private method
        static string
            CreateQueryFilter(
                DocumentKey documentKey,
                DocumentKeyFilter
                    filter) // https://medienstudio.net/development-en/startswith-filter-on-azure-table-storage-columns-in-c-javascript/
        {
            if (filter == DocumentKeyFilter.FullMatch)
            {
                return $"PartitionKey eq '{documentKey.PartitionKey}' and RowKey eq '{documentKey.RowKey}'";
            }

            if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
            {
                var lastChar = documentKey.RowKey[documentKey.RowKey.Length - 1];
                var nextChar = (char)(lastChar + 1);
                var rowKeyNext = documentKey.RowKey.Substring(0, documentKey.RowKey.Length - 1) + nextChar;

                // this filter matches the whole paritionKey and startsWith on the rowKey
                return
                    $"PartitionKey eq '{documentKey.PartitionKey}' and RowKey ge '{documentKey.RowKey}' and RowKey lt '{rowKeyNext}'";
            }

            if (filter == DocumentKeyFilter.RowKeySuffixMatch)
            {
                // this filter matches the whole paritionKey and endsWith on the rowKey
                throw new NotSupportedException();
            }

            return default;
        }
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var tableName = this.GetTableName<T>();
        this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
        var tableClient = this.serviceClient.GetTableClient(tableName);
        var results = new HashSet<DocumentKey>();

        await foreach (var page in tableClient.QueryAsync<TableEntity>(cancellationToken: cancellationToken)
                           .AsPages()
                           .WithCancellation(cancellationToken))
        {
            foreach (var tableEntity in page.Values)
            {
                results.Add(new DocumentKey(tableEntity.PartitionKey, tableEntity.RowKey));
            }
        }

        return results;
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.ListAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));

        var tableName = this.GetTableName<T>();
        this.serviceClient.CreateTableIfNotExists(tableName, cancellationToken);
        var tableClient = this.serviceClient.GetTableClient(tableName);
        var results = new HashSet<DocumentKey>();
        var queryFilter = CreateQueryFilter(documentKey, filter);

        if (string.IsNullOrEmpty(queryFilter))
        {
            return results;
        }

        await foreach (var page in tableClient
                           .QueryAsync<TableEntity>(queryFilter, cancellationToken: cancellationToken)
                           .AsPages()
                           .WithCancellation(cancellationToken))
        {
            foreach (var tableEntity in page.Values)
            {
                results.Add(new DocumentKey(tableEntity.PartitionKey, tableEntity.RowKey));
            }
        }

        return results;

        // TODO: refactor to private method
        static string
            CreateQueryFilter(
                DocumentKey documentKey,
                DocumentKeyFilter
                    filter) // https://medienstudio.net/development-en/startswith-filter-on-azure-table-storage-columns-in-c-javascript/
        {
            if (filter == DocumentKeyFilter.FullMatch)
            {
                return $"PartitionKey eq '{documentKey.PartitionKey}' and RowKey eq '{documentKey.RowKey}'";
            }

            if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
            {
                if (documentKey.RowKey.IsNullOrEmpty())
                {
                    // ignore the rowkey as it is empty/null
                    return $"PartitionKey eq '{documentKey.PartitionKey}'";
                }

                var lastChar = documentKey.RowKey[documentKey.RowKey.Length - 1];
                var nextChar = (char)(lastChar + 1);
                var rowKeyNext = documentKey.RowKey.Substring(0, documentKey.RowKey.Length - 1) + nextChar;

                // this filter matches the whole paritionKey and startsWith on the rowKey
                return
                    $"PartitionKey eq '{documentKey.PartitionKey}' and RowKey ge '{documentKey.RowKey}' and RowKey lt '{rowKeyNext}'";
            }

            if (filter == DocumentKeyFilter.RowKeySuffixMatch)
            {
                // this filter matches the whole paritionKey and endsWith on the rowKey
                throw new NotSupportedException();
            }

            return default;
        }
    }

    /// <summary>
    ///     Counts the number of entities of type T in the document store
    /// </summary>
    public async Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return (await this.FindAsync<T>(cancellationToken)).LongCount();
    }

    /// <summary>
    ///     Checks if an entity of type T with given whole partitionKey and whole rowKey exists in the document store
    /// </summary>
    public async Task<bool> ExistsAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return (await this.FindAsync<T>(documentKey, cancellationToken)).Any();
    }

    /// <summary>
    ///     Inserts or updates an entity of type T in the document store
    /// </summary>
    public async Task UpsertAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

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
    public async Task UpsertAsync<T>(
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
    public async Task DeleteAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

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
}