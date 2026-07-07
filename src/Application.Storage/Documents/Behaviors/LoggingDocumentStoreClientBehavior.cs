// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Logs document-store operations before forwarding them to the inner client.
/// </summary>
/// <typeparam name="T">The document type handled by the decorated client.</typeparam>
public class LoggingDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    private readonly string type;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
    public LoggingDocumentStoreClientBehavior(ILoggerFactory loggerFactory, IDocumentStoreClient<T> inner)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<LoggingDocumentStoreClientBehavior<T>>() ??
            NullLoggerFactory.Instance.CreateLogger<LoggingDocumentStoreClientBehavior<T>>();
        this.Inner = inner;
        this.type = typeof(T).Name;
    }

    /// <summary>
    /// Gets the logger used by the behavior.
    /// </summary>
    protected ILogger<LoggingDocumentStoreClientBehavior<T>> Logger { get; }

    /// <summary>
    /// Gets the decorated inner client.
    /// </summary>
    protected IDocumentStoreClient<T> Inner { get; }

    /// <inheritdoc />
    public async Task<Result<T>> GetResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        this.Logger.LogInformation("{LogKey} documentclient: get (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})",
            Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        return await this.Inner.GetResultAsync(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<DocumentPage<T>>> FindPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default)
    {
        var result = await this.Inner.FindPageResultAsync(query, cancellationToken);
        this.Logger.LogDebug(
            "{LogKey} documentclient: find-page completed (type={DocumentType}, success={Success}, filter={Filter}, take={Take}, items={Items}, hasMore={HasMore}, fullScan={FullScan}, hasContinuation={HasContinuation})",
            Constants.LogKey,
            this.type,
            result.IsSuccess,
            query?.Filter,
            query?.Take,
            result.IsSuccess ? result.Value.Items.Count : 0,
            result.IsSuccess && result.Value.HasMore,
            query?.AllowFullScan,
            !string.IsNullOrWhiteSpace(query?.ContinuationToken));

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<DocumentKeyPage>> ListPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default)
    {
        var result = await this.Inner.ListPageResultAsync(query, cancellationToken);
        this.Logger.LogDebug(
            "{LogKey} documentclient: list-page completed (type={DocumentType}, success={Success}, filter={Filter}, take={Take}, keys={Keys}, hasMore={HasMore}, fullScan={FullScan}, hasContinuation={HasContinuation})",
            Constants.LogKey,
            this.type,
            result.IsSuccess,
            query?.Filter,
            query?.Take,
            result.IsSuccess ? result.Value.Items.Count : 0,
            result.IsSuccess && result.Value.HasMore,
            query?.AllowFullScan,
            !string.IsNullOrWhiteSpace(query?.ContinuationToken));

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<long>> CountResultAsync(DocumentCountQuery query, CancellationToken cancellationToken = default)
    {
        var result = await this.Inner.CountResultAsync(query, cancellationToken);
        this.Logger.LogInformation("{LogKey} documentclient: count (type={DocumentType}, success={Success}, count={Count})",
            Constants.LogKey, this.type, result.IsSuccess, result.IsSuccess ? result.Value : 0);

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        this.Logger.LogInformation("{LogKey} documentclient: exists (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})",
            Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        return await this.Inner.ExistsResultAsync(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        this.Logger.LogInformation("{LogKey} documentclient: upsert (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})",
            Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        return await this.Inner.UpsertResultAsync(documentKey, entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        this.Logger.LogInformation("{LogKey} documentclient: upsert-batch (type={DocumentType})", Constants.LogKey, this.type);

        return await this.Inner.UpsertResultAsync(entities, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        this.Logger.LogInformation("{LogKey} documentclient: delete (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})",
            Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        return await this.Inner.DeleteResultAsync(documentKey, cancellationToken);
    }
}
