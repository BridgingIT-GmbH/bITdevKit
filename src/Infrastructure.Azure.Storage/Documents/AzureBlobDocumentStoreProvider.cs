// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Storage;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using global::Azure.Storage;
using global::Azure.Storage.Blobs;
using global::Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class AzureBlobDocumentStoreProvider : IDocumentStoreProvider
{
    private readonly BlobServiceClient serviceClient;
    private readonly string containerNamePrefix;
    private readonly ISerializer serializer;
    private readonly string blobNameSeperator = "__";
    //private bool isLocal;

    public AzureBlobDocumentStoreProvider( // TODO: add Options ctor
        ILoggerFactory loggerFactory,
        string connectionString,
        string containerNamePrefix = null,
        BlobClientOptions clientOptions = null,
        ISerializer serializer = null)
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureBlobDocumentStoreProvider>() ?? NullLoggerFactory.Instance.CreateLogger<AzureBlobDocumentStoreProvider>();
        this.serviceClient = new BlobServiceClient(connectionString, clientOptions ?? new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_01_03));
        //this.isLocal = this.serviceClient.Uri.ToString().Contains("127.0.0.1");
        this.containerNamePrefix = containerNamePrefix;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
    }

    public AzureBlobDocumentStoreProvider(
        ILoggerFactory loggerFactory,
        string storageUri,
        string accountName,
        string storageAccountKey,
        string tableNamePrefix = null,
        BlobClientOptions clientOptions = null,
        ISerializer serializer = null)
    {
        EnsureArg.IsNotNullOrEmpty(storageUri, nameof(storageUri));
        EnsureArg.IsNotNullOrEmpty(accountName, nameof(accountName));
        EnsureArg.IsNotNullOrEmpty(storageAccountKey, nameof(storageAccountKey));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureBlobDocumentStoreProvider>() ?? NullLoggerFactory.Instance.CreateLogger<AzureBlobDocumentStoreProvider>();
        this.serviceClient = new BlobServiceClient(
            new Uri(storageUri),
            new StorageSharedKeyCredential(accountName, storageAccountKey),
            clientOptions);
        //this.isLocal = this.serviceClient.Uri.ToString().Contains("127.0.0.0");
        this.containerNamePrefix = tableNamePrefix;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
    }

    public AzureBlobDocumentStoreProvider(
        ILoggerFactory loggerFactory,
        BlobServiceClient serviceClient,
        string tableNamePrefix = null,
        ISerializer serializer = null)
    {
        EnsureArg.IsNotNull(serviceClient, nameof(serviceClient));

        // TODO: use options+builder
        this.Logger = loggerFactory?.CreateLogger<AzureBlobDocumentStoreProvider>() ?? NullLoggerFactory.Instance.CreateLogger<AzureBlobDocumentStoreProvider>();
        this.serviceClient = serviceClient;
        //this.isLocal = this.serviceClient.Uri.ToString().Contains("127.0.0.0");
        this.containerNamePrefix = tableNamePrefix;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
    }

    protected ILogger<AzureBlobDocumentStoreProvider> Logger { get; }

    /// <summary>
    /// Retrieves entities of type T from document store asynchronously
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
        var results = new HashSet<T>();

        await foreach (var page in containerClient.GetBlobsAsync(cancellationToken: cancellationToken).AsPages().WithCancellation(cancellationToken))
        {
            foreach (var blobItem in page.Values)
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var blob = await blobClient.DownloadContentAsync(cancellationToken);

                using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
                var entity = this.serializer.Deserialize<T>(stream);

                if (entity is not null)
                {
                    results.Add(entity);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Retrieves entities of type T filtered by the whole partitionKey and whole rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.FindAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    /// <summary>
    /// Searches for entities of type T by the whole partitionKey and startswith rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(
    DocumentKey documentKey,
    DocumentKeyFilter filter,
    CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
        var results = new HashSet<T>();

        //if (!this.isLocal)
        //{
        //    // WARN: currently azurite (local) does not support index tags https://github.com/Azure/Azurite/issues/647
        //    var filter = $"\"{nameof(IDocumentEntity.PartitionKey)}\"='{partitionKey}' AND \"{nameof(IDocumentEntity.RowKey)}\"='{rowKey}'"; // https://learn.microsoft.com/en-us/rest/api/storageservices/find-blobs-by-tags#remarks
        //    await foreach (var blobItem in containerClient.FindBlobsByTagsAsync(filter, cancellationToken))
        //    {
        //        var blobName = blobItem.BlobName;
        //        var blobClient = containerClient.GetBlobClient(blobName);

        //        using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        //        var entity = this.serializer.Deserialize<T>(stream);
        //        if (entity is not null)
        //        {
        //            results.Add(entity);
        //        }
        //    }
        //}
        //else
        //{
        if (filter == DocumentKeyFilter.FullMatch)
        {
            var blobName = $"{documentKey.PartitionKey}{this.blobNameSeperator}{documentKey.RowKey}";
            var blobClient = containerClient.GetBlobClient(blobName);
            if (await blobClient.ExistsAsync(cancellationToken))
            {
                using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
                var entity = this.serializer.Deserialize<T>(stream);
                if (entity is not null)
                {
                    results.Add(entity);
                }
            }
        }
        else if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
        {
            await foreach (var page in containerClient.GetBlobsAsync(prefix: $"{documentKey.PartitionKey}{this.blobNameSeperator}{documentKey.RowKey}", cancellationToken: cancellationToken).AsPages().WithCancellation(cancellationToken))
            {
                foreach (var blobItem in page.Values)
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);

                    if (await blobClient.ExistsAsync(cancellationToken))
                    {
                        using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
                        var entity = this.serializer.Deserialize<T>(stream);
                        if (entity is not null)
                        {
                            results.Add(entity);
                        }
                    }
                }
            }
        }
        else if (filter == DocumentKeyFilter.RowKeySuffixMatch)
        {
            await foreach (var page in containerClient.GetBlobsAsync(cancellationToken: cancellationToken).AsPages().WithCancellation(cancellationToken))
            {
                foreach (var blobItem in page.Values)
                {
                    var keys = blobItem.Name.Split(this.blobNameSeperator);
                    if (keys.Length > 1 && keys[0] == documentKey.PartitionKey && keys[1].EndsWith(documentKey.RowKey))
                    {
                        var blobClient = containerClient.GetBlobClient(blobItem.Name);

                        if (await blobClient.ExistsAsync(cancellationToken))
                        {
                            using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
                            var entity = this.serializer.Deserialize<T>(stream);
                            if (entity is not null)
                            {
                                results.Add(entity);
                            }
                        }
                    }
                }
            }
        }

        //}

        return results;
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
        var results = new HashSet<DocumentKey>();

        await foreach (var page in containerClient.GetBlobsAsync(cancellationToken: cancellationToken).AsPages().WithCancellation(cancellationToken))
        {
            foreach (var blobItem in page.Values)
            {
                var keys = blobItem.Name.Split(this.blobNameSeperator);
                if (keys.Length > 1)
                {
                    results.Add(new(keys[0], keys[1]));
                }
            }
        }

        return results;
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.ListAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
    where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        //EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var containerClient = await this.GetBlobContainerClientAsync<T>(cancellationToken);
        var results = new HashSet<DocumentKey>();

        //if (!this.isLocal)
        //{
        //    // WARN: currently azurite (local) does not support index tags https://github.com/Azure/Azurite/issues/647
        //    var filter = $"\"{nameof(IDocumentEntity.PartitionKey)}\"='{partitionKey}' AND \"{nameof(IDocumentEntity.RowKey)}\"='{rowKey}'"; // https://learn.microsoft.com/en-us/rest/api/storageservices/find-blobs-by-tags#remarks
        //    await foreach (var blobItem in containerClient.FindBlobsByTagsAsync(filter, cancellationToken))
        //    {
        //        var blobName = blobItem.BlobName;
        //        var blobClient = containerClient.GetBlobClient(blobName);

        //        using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        //        var entity = this.serializer.Deserialize<T>(stream);
        //        if (entity is not null)
        //        {
        //            results.Add(entity);
        //        }
        //    }
        //}
        //else
        //{
        if (filter == DocumentKeyFilter.FullMatch)
        {
            await foreach (var page in containerClient.GetBlobsAsync(cancellationToken: cancellationToken).AsPages().WithCancellation(cancellationToken))
            {
                foreach (var blobItem in page.Values)
                {
                    var keys = blobItem.Name.Split(this.blobNameSeperator);
                    if (keys.Length > 1 && keys[0] == documentKey.PartitionKey && keys[1] == documentKey.RowKey)
                    {
                        results.Add(new(keys[0], keys[1]));
                    }
                }
            }
        }
        else if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
        {
            await foreach (var page in containerClient.GetBlobsAsync(prefix: $"{documentKey.PartitionKey}{this.blobNameSeperator}{documentKey.RowKey}", cancellationToken: cancellationToken).AsPages().WithCancellation(cancellationToken))
            {
                foreach (var blobItem in page.Values)
                {
                    var keys = blobItem.Name.Split(this.blobNameSeperator);
                    if (keys.Length > 1)
                    {
                        results.Add(new(keys[0], keys[1]));
                    }
                }
            }
        }
        else if (filter == DocumentKeyFilter.RowKeySuffixMatch)
        {
            await foreach (var page in containerClient.GetBlobsAsync(cancellationToken: cancellationToken).AsPages().WithCancellation(cancellationToken))
            {
                foreach (var blobItem in page.Values)
                {
                    var keys = blobItem.Name.Split(this.blobNameSeperator);
                    if (keys.Length > 1 && keys[0] == documentKey.PartitionKey && keys[1].EndsWith(documentKey.RowKey))
                    {
                        results.Add(new(keys[0], keys[1]));
                    }
                }
            }
        }

        //}

        return results;
    }

    /// <summary>
    /// Counts the number of entities of type T in the document store
    /// </summary>
    public async Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
    where T : class, new()
    {
        return (await this.FindAsync<T>(cancellationToken)).LongCount();
    }

    public async Task<bool> ExistsAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return (await this.FindAsync<T>(documentKey, cancellationToken)).Any();
    }

    /// <summary>
    /// Inserts or updates an entity of type T in the document store
    /// </summary>
    public async Task UpsertAsync<T>(
        DocumentKey documentKey,
        T entity,
        CancellationToken cancellationToken = default)
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

        await blobClient.UploadAsync(
            stream,
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
    /// Inserts or updates multiple entities of type T in the document store
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
    /// Deletes an entity of type T with the specified whole partitionKey and whole rowKey from the document store
    /// </summary>
    public async Task DeleteAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
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

    private string GetContainerName<T>() =>
        $"{this.containerNamePrefix}-{typeof(T).Name}".ToLowerInvariant().Trim('-').TruncateLeft(63);
}