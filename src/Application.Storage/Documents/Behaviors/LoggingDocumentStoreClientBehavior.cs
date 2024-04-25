// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public partial class LoggingDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    private readonly string type;

    public LoggingDocumentStoreClientBehavior(
        ILoggerFactory loggerFactory,
        IDocumentStoreClient<T> inner)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<LoggingDocumentStoreClientBehavior<T>>() ?? NullLoggerFactory.Instance.CreateLogger<LoggingDocumentStoreClientBehavior<T>>();
        this.Inner = inner;
        this.type = typeof(T).Name;
    }

    protected ILogger<LoggingDocumentStoreClientBehavior<T>> Logger { get; }

    protected ChaosDocumentStoreClientBehaviorOptions Options { get; }

    protected IDocumentStoreClient<T> Inner { get; }

    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogDelete(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        await this.Inner.DeleteAsync(documentKey, cancellationToken);
    }

    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken)
    {
        TypedLogger.LogFind(this.Logger, Constants.LogKey, this.type);

        return await this.Inner.FindAsync(cancellationToken);
    }

    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindKey(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        return await this.Inner.FindAsync(documentKey, cancellationToken);
    }

    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindKeyFilter(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey, filter);

        return await this.Inner.FindAsync(documentKey, filter, cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken)
    {
        TypedLogger.LogList(this.Logger, Constants.LogKey, this.type);

        return await this.Inner.ListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogListKey(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        return await this.Inner.ListAsync(documentKey, cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogListKeyFilter(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey, filter);

        return await this.Inner.ListAsync(documentKey, filter, cancellationToken);
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        TypedLogger.LogCount(this.Logger, Constants.LogKey, this.type);

        return await this.Inner.CountAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExistsKey(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        return await this.Inner.ExistsAsync(documentKey, cancellationToken);
    }

    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogUpsert(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);

        await this.Inner.UpsertAsync(documentKey, entity, cancellationToken);
    }

    public async Task UpsertAsync(IEnumerable<(DocumentKey DocumentKey, T Entity)> entities, CancellationToken cancellationToken = default)
    {
        foreach (var (documentKey, entity) in entities)
        {
            TypedLogger.LogUpsert(this.Logger, Constants.LogKey, this.type, documentKey.PartitionKey, documentKey.RowKey);
        }

        await this.Inner.UpsertAsync(entities, cancellationToken);
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} documentclient: delete (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogDelete(ILogger logger, string logKey, string documentType, string partitionKey, string rowKey);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} documentclient: find (type={DocumentType})")]
        public static partial void LogFind(ILogger logger, string logKey, string documentType);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} documentclient: find (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogFindKey(ILogger logger, string logKey, string documentType, string partitionKey, string rowKey);

        [LoggerMessage(3, LogLevel.Information, "{LogKey} documentclient: find (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey}, filter={DocumentFilter})")]
        public static partial void LogFindKeyFilter(ILogger logger, string logKey, string documentType, string partitionKey, string rowKey, DocumentKeyFilter documentFilter);

        [LoggerMessage(4, LogLevel.Information, "{LogKey} documentclient: list (type={DocumentType})")]
        public static partial void LogList(ILogger logger, string logKey, string documentType);

        [LoggerMessage(5, LogLevel.Information, "{LogKey} documentclient: list (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogListKey(ILogger logger, string logKey, string documentType, string partitionKey, string rowKey);

        [LoggerMessage(6, LogLevel.Information, "{LogKey} documentclient: list (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey}, filter={DocumentFilter})")]
        public static partial void LogListKeyFilter(ILogger logger, string logKey, string documentType, string partitionKey, string rowKey, DocumentKeyFilter documentFilter);

        [LoggerMessage(7, LogLevel.Information, "{LogKey} documentclient: count (type={DocumentType})")]
        public static partial void LogCount(ILogger logger, string logKey, string documentType);

        [LoggerMessage(8, LogLevel.Information, "{LogKey} documentclient: exists (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogExistsKey(ILogger logger, string logKey, string documentType, string partitionKey, string rowKey);

        [LoggerMessage(9, LogLevel.Information, "{LogKey} documentclient: upsert (type={DocumentType}, partitionKey={PartitionKey}, rowKey={RowKey})")]
        public static partial void LogUpsert(ILogger logger, string logKey, string documentType, string partitionKey, string rowKey);
    }
}