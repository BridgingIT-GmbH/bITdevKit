// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Logs document-store operations before forwarding them to the inner client.
/// </summary>
/// <typeparam name="T">The document type handled by the decorated client.</typeparam>
/// <example>
/// <code>
/// services.AddEntityFrameworkDocumentStoreClient&lt;Person, AppDbContext&gt;()
///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;();
/// </code>
/// </example>
public partial class LoggingDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    private readonly string type;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the behavior logger.</param>
    /// <param name="inner">The inner client to decorate.</param>
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
    /// Gets reserved behavior options for derived logging decorators.
    /// </summary>
    protected ChaosDocumentStoreClientBehaviorOptions Options { get; }

    /// <summary>
    /// Gets the decorated inner client.
    /// </summary>
    protected IDocumentStoreClient<T> Inner { get; }

    /// <inheritdoc />
    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogDelete(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        await this.Inner.DeleteAsync(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken)
    {
        TypedLogger.LogFind(this.Logger, Constants.LogKey, this.type);

        return await this.Inner.FindAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindKey(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        return await this.Inner.FindAsync(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindKeyFilter(this.Logger,
            Constants.LogKey,
            this.type,
            documentKey.PartitionKey,
            documentKey.RowKey,
            filter);

        return await this.Inner.FindAsync(documentKey, filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken)
    {
        TypedLogger.LogList(this.Logger, Constants.LogKey, this.type);

        return await this.Inner.ListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogListKey(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        return await this.Inner.ListAsync(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogListKeyFilter(this.Logger,
            Constants.LogKey,
            this.type,
            documentKey.PartitionKey,
            documentKey.RowKey,
            filter);

        return await this.Inner.ListAsync(documentKey, filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        TypedLogger.LogCount(this.Logger, Constants.LogKey, this.type);

        return await this.Inner.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExistsKey(this.Logger,
            Constants.LogKey,
            this.type,
            documentKey.PartitionKey,
            documentKey.RowKey);

        return await this.Inner.ExistsAsync(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogUpsert(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        await this.Inner.UpsertAsync(documentKey, entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        foreach (var (documentKey, entity) in entities)
        {
            TypedLogger.LogUpsert(this.Logger,
                Constants.LogKey,
                this.type,
                documentKey.PartitionKey,
                documentKey.RowKey);
        }

        await this.Inner.UpsertAsync(entities, cancellationToken);
    }

    /// <summary>
    /// Provides strongly typed logger-message helpers for document-store operations.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Logs a delete operation.
        /// </summary>
        [LoggerMessage(0,
            LogLevel.Information,
            "{LogKey} documentclient: delete (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogDelete(
            ILogger logger,
            string logKey,
            string documentType,
            string partitionKey,
            string rowKey);

        /// <summary>
        /// Logs an unfiltered find operation.
        /// </summary>
        [LoggerMessage(1, LogLevel.Information, "{LogKey} documentclient: find (type={DocumentType})")]
        public static partial void LogFind(ILogger logger, string logKey, string documentType);

        /// <summary>
        /// Logs an exact-key find operation.
        /// </summary>
        [LoggerMessage(2,
            LogLevel.Information,
            "{LogKey} documentclient: find (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogFindKey(
            ILogger logger,
            string logKey,
            string documentType,
            string partitionKey,
            string rowKey);

        /// <summary>
        /// Logs a filtered find operation.
        /// </summary>
        [LoggerMessage(3,
            LogLevel.Information,
            "{LogKey} documentclient: find (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey}, filter={DocumentFilter})")]
        public static partial void LogFindKeyFilter(
            ILogger logger,
            string logKey,
            string documentType,
            string partitionKey,
            string rowKey,
            DocumentKeyFilter documentFilter);

        /// <summary>
        /// Logs an unfiltered list operation.
        /// </summary>
        [LoggerMessage(4, LogLevel.Information, "{LogKey} documentclient: list (type={DocumentType})")]
        public static partial void LogList(ILogger logger, string logKey, string documentType);

        /// <summary>
        /// Logs an exact-key list operation.
        /// </summary>
        [LoggerMessage(5,
            LogLevel.Information,
            "{LogKey} documentclient: list (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogListKey(
            ILogger logger,
            string logKey,
            string documentType,
            string partitionKey,
            string rowKey);

        /// <summary>
        /// Logs a filtered list operation.
        /// </summary>
        [LoggerMessage(6,
            LogLevel.Information,
            "{LogKey} documentclient: list (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey}, filter={DocumentFilter})")]
        public static partial void LogListKeyFilter(
            ILogger logger,
            string logKey,
            string documentType,
            string partitionKey,
            string rowKey,
            DocumentKeyFilter documentFilter);

        /// <summary>
        /// Logs a count operation.
        /// </summary>
        [LoggerMessage(7, LogLevel.Information, "{LogKey} documentclient: count (type={DocumentType})")]
        public static partial void LogCount(ILogger logger, string logKey, string documentType);

        /// <summary>
        /// Logs an exact-key existence check.
        /// </summary>
        [LoggerMessage(8,
            LogLevel.Information,
            "{LogKey} documentclient: exists (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogExistsKey(
            ILogger logger,
            string logKey,
            string documentType,
            string partitionKey,
            string rowKey);

        /// <summary>
        /// Logs an upsert operation.
        /// </summary>
        [LoggerMessage(9,
            LogLevel.Information,
            "{LogKey} documentclient: upsert (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogUpsert(
            ILogger logger,
            string logKey,
            string documentType,
            string partitionKey,
            string rowKey);
    }
}
