// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Application.Storage;
using Common;
using System.Linq.Expressions;

/// <summary>
/// Implements <see cref="IDocumentStoreProvider" /> using Azure Cosmos DB SQL API.
/// </summary>
/// <example>
/// <code>
/// var provider = new CosmosDocumentStoreProvider(cosmosSqlProvider);
/// await provider.UpsertResultAsync(new DocumentKey("people", "42"), person, cancellationToken);
/// </code>
/// </example>
public class CosmosDocumentStoreProvider : IDocumentStoreProvider
{
    private const string ProviderName = "cosmos";
    private readonly ICosmosSqlProvider<CosmosStorageDocument> provider;
    private readonly ISerializer serializer;
    private readonly DocumentStoreOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDocumentStoreProvider" /> class.
    /// </summary>
    /// <param name="provider">The Cosmos SQL provider used to read and write storage documents.</param>
    /// <param name="serializer">The optional serializer used for document payloads.</param>
    /// <param name="options">The optional document-store query safety options.</param>
    public CosmosDocumentStoreProvider( // TODO: add ctor which accepts Options
        ICosmosSqlProvider<CosmosStorageDocument> provider,
        ISerializer serializer = null,
        DocumentStoreOptions options = null)
    {
        EnsureArg.IsNotNull(provider, nameof(provider));

        this.provider = provider;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
        this.options = options ?? new DocumentStoreOptions();
    }

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedServerSide,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedServerSide,
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
            var pageResult = await this.provider.ReadItemsPageResultAsync(
                this.CreateExpressions<T>(new DocumentQuery
                {
                    DocumentKey = documentKey,
                    Filter = DocumentKeyFilter.FullMatch,
                    Take = 1
                }),
                1,
                e => e.RowKey,
                partitionKeyValue: this.GetTypeName<T>(),
                cancellationToken: cancellationToken);
            if (pageResult.IsFailure)
            {
                return Result<T>.Failure(pageResult);
            }

            var document = pageResult.Value.Items.FirstOrDefault();
            return document is null
                ? Result<T>.Failure(new DocumentStoreNotFoundError($"Document '{documentKey.PartitionKey}/{documentKey.RowKey}' was not found."))
                : Result<T>.Success(this.serializer.Deserialize<T>(document.Content));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex));
        }
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
            var pageResult = await this.provider.ReadItemsPageResultAsync(
                this.CreateExpressions<T>(query),
                validation.Value.Take,
                e => e.RowKey,
                partitionKeyValue: this.GetTypeName<T>(),
                continuationToken: validation.Value.ContinuationToken?.NativeToken,
                cancellationToken: cancellationToken);
            if (pageResult.IsFailure)
            {
                return Result<DocumentPage<T>>.Failure(pageResult);
            }

            return Result<DocumentPage<T>>.Success(new DocumentPage<T>
            {
                Items = pageResult.Value.Items.Select(e => this.serializer.Deserialize<T>(e.Content)).ToList(),
                ContinuationToken = CreateContinuationToken(validation.Value.QueryHash, pageResult.Value.ContinuationToken)
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<DocumentPage<T>>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex));
        }
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
            var pageResult = await this.provider.ReadItemsPageResultAsync(
                this.CreateExpressions<T>(query),
                e => new CosmosDocumentKeyProjection
                {
                    PartitionKey = e.PartitionKey,
                    RowKey = e.RowKey
                },
                validation.Value.Take,
                e => e.RowKey,
                partitionKeyValue: this.GetTypeName<T>(),
                continuationToken: validation.Value.ContinuationToken?.NativeToken,
                cancellationToken: cancellationToken);
            if (pageResult.IsFailure)
            {
                return Result<DocumentKeyPage>.Failure(pageResult);
            }

            return Result<DocumentKeyPage>.Success(new DocumentKeyPage
            {
                Items = pageResult.Value.Items.Select(e => new DocumentKey(e.PartitionKey, e.RowKey)).ToList(),
                ContinuationToken = CreateContinuationToken(validation.Value.QueryHash, pageResult.Value.ContinuationToken)
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<DocumentKeyPage>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex));
        }
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
            var count = 0L;
            string nativeToken = null;
            var expressions = this.CreateExpressions<T>(query);
            do
            {
                var pageResult = await this.provider.ReadItemsPageResultAsync(
                    expressions,
                    e => new CosmosDocumentKeyProjection
                    {
                        PartitionKey = e.PartitionKey,
                        RowKey = e.RowKey
                    },
                    this.options.MaxTake,
                    e => e.RowKey,
                    partitionKeyValue: this.GetTypeName<T>(),
                    continuationToken: nativeToken,
                    cancellationToken: cancellationToken);
                if (pageResult.IsFailure)
                {
                    return Result<long>.Failure(pageResult);
                }

                count += pageResult.Value.Items.Count;
                nativeToken = pageResult.Value.ContinuationToken;
            }
            while (!string.IsNullOrWhiteSpace(nativeToken));

            return Result<long>.Success(count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<long>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        try
        {
            var pageResult = await this.provider.ReadItemsPageResultAsync(
                this.CreateExpressions<T>(new DocumentCountQuery
                {
                    DocumentKey = documentKey,
                    Filter = DocumentKeyFilter.FullMatch
                }),
                e => new CosmosDocumentKeyProjection
                {
                    PartitionKey = e.PartitionKey,
                    RowKey = e.RowKey
                },
                1,
                e => e.RowKey,
                partitionKeyValue: this.GetTypeName<T>(),
                cancellationToken: cancellationToken);
            if (pageResult.IsFailure)
            {
                return Result<bool>.Failure()
                    .WithErrors(pageResult.Errors);
            }

            return Result<bool>.Success(pageResult.Value.Items.Any());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        try
        {
            await this.UpsertAsync(documentKey, entity, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        try
        {
            await this.UpsertAsync(entities, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        try
        {
            await this.DeleteAsync<T>(documentKey, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Failure(new DocumentStoreProviderError(ex.GetFullMessage(), ex));
        }
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

        var type = this.GetTypeName<T>();
        var document =
            (await this.provider.ReadItemsAsync(
                e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey,
                partitionKeyValue: type,
                cancellationToken: cancellationToken)).FirstOrDefault();

        if (document is null)
        {
            document = new CosmosStorageDocument
            {
                Id = GuidGenerator.Create($"{documentKey.PartitionKey}-{documentKey.RowKey}").ToString(),
                Type = type,
                PartitionKey = documentKey.PartitionKey,
                RowKey = documentKey.RowKey
            };
        }
        else
        {
            document.UpdatedDate = DateTime.UtcNow;
        }

        document.Content = this.serializer.SerializeToString(entity);
        document.ContentHash = HashHelper.Compute(entity);

        await this.provider.UpsertItemAsync(document, type, cancellationToken);
    }

    private async Task UpsertAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entities, nameof(entities));

        if (entities.SafeAny())
        {
            var type = this.GetTypeName<T>();
            foreach (var (documentKey, entity) in entities.SafeNull())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var document =
                    (await this.provider.ReadItemsAsync(
                        e => e.Type == type &&
                            e.PartitionKey == documentKey.PartitionKey &&
                            e.RowKey == documentKey.RowKey,
                        partitionKeyValue: type,
                        cancellationToken: cancellationToken)).FirstOrDefault();
                if (document is null)
                {
                    document = new CosmosStorageDocument
                    {
                        Id = GuidGenerator.Create($"{documentKey.PartitionKey}-{documentKey.RowKey}").ToString(),
                        Type = type,
                        PartitionKey = documentKey.PartitionKey,
                        RowKey = documentKey.RowKey
                    };
                }
                else
                {
                    document.UpdatedDate = DateTime.UtcNow;
                }

                document.Content = this.serializer.SerializeToString(entity);
                document.ContentHash = HashHelper.Compute(entity);

                await this.provider.UpsertItemAsync(document, type, cancellationToken);
            }
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

        var type = this.GetTypeName<T>();
        var documents = await this.provider.ReadItemsAsync(
            e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey,
            partitionKeyValue: type,
            cancellationToken: cancellationToken);

        if (documents.SafeAny())
        {
            foreach (var documentEntity in documents)
            {
                await this.provider.DeleteItemAsync(documentEntity.Id, type, cancellationToken);
            }
        }
    }

    private string GetTypeName<T>()
    {
        return typeof(T).FullName.ToLowerInvariant().TruncateLeft(1024);
    }

    private IEnumerable<Expression<Func<CosmosStorageDocument, bool>>> CreateExpressions<T>(DocumentQuery query)
        where T : class, new()
    {
        query ??= new DocumentQuery();

        return this.CreateExpressions<T>(new DocumentCountQuery
        {
            DocumentKey = query.DocumentKey,
            Filter = query.Filter,
            AllowFullScan = query.AllowFullScan
        });
    }

    private IEnumerable<Expression<Func<CosmosStorageDocument, bool>>> CreateExpressions<T>(DocumentCountQuery query)
        where T : class, new()
    {
        query ??= new DocumentCountQuery();
        var type = this.GetTypeName<T>();

        if (query.DocumentKey is null)
        {
            return [e => e.Type == type];
        }

        var key = query.DocumentKey.Value;
        return query.Filter switch
        {
            DocumentKeyFilter.RowKeyPrefixMatch =>
            [
                e => e.Type == type &&
                    e.PartitionKey == key.PartitionKey &&
                    e.RowKey.StartsWith(key.RowKey)
            ],
            DocumentKeyFilter.RowKeySuffixMatch =>
            [
                e => e.Type == type &&
                    e.PartitionKey == key.PartitionKey &&
                    e.RowKey.EndsWith(key.RowKey)
            ],
            _ =>
            [
                e => e.Type == type &&
                    e.PartitionKey == key.PartitionKey &&
                    e.RowKey == key.RowKey
            ]
        };
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

    private sealed class CosmosDocumentKeyProjection
    {
        /// <summary>
        /// Gets the document partition key.
        /// </summary>
        public string PartitionKey { get; init; }

        /// <summary>
        /// Gets the document row key.
        /// </summary>
        public string RowKey { get; init; }
    }
}
