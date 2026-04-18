// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Implements <see cref="IDocumentStoreProvider" /> using an in-memory context.
/// </summary>
/// <remarks>
/// This provider is useful for tests, local development, and other scenarios where persistence is not required beyond the
/// current process.
/// </remarks>
/// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
/// <param name="context">The optional shared in-memory context backing the provider.</param>
/// <example>
/// <code>
/// services.AddDocumentStoreClient&lt;Person&gt;(
///     sp => new DocumentStoreClient&lt;Person&gt;(
///         new InMemoryDocumentStoreProvider(sp.GetRequiredService&lt;ILoggerFactory&gt;())));
/// </code>
/// </example>
public class InMemoryDocumentStoreProvider( // TODO: add Options ctor
    ILoggerFactory loggerFactory,
    InMemoryDocumentStoreContext context = null) : IDocumentStoreProvider
{
    /// <summary>
    /// Gets the logger used by the provider.
    /// </summary>
    protected ILogger<InMemoryDocumentStoreProvider> Logger { get; } =
        loggerFactory?.CreateLogger<InMemoryDocumentStoreProvider>() ??
        NullLoggerFactory.Instance.CreateLogger<InMemoryDocumentStoreProvider>();

    /// <summary>
    /// Gets the in-memory context backing the provider.
    /// </summary>
    protected InMemoryDocumentStoreContext Context { get; } = context ?? new InMemoryDocumentStoreContext();

    /// <inheritdoc />
    public Task<IEnumerable<T>> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Task.FromResult(this.Context.Find<T>());
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.FindAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        return Task.FromResult(this.Context.Find<T>(new DocumentKey(documentKey.PartitionKey, documentKey.RowKey),
            filter));
    }

    /// <inheritdoc />
    public Task<IEnumerable<DocumentKey>> ListAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Task.FromResult(this.Context.List<T>());
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.ListAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IEnumerable<DocumentKey>> ListAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));

        return Task.FromResult(this.Context.List<T>(documentKey, filter));
    }

    /// <inheritdoc />
    public Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Task.FromResult(this.Context.Find<T>().LongCount());
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        return Task.FromResult(
            this.Context.Find<T>(new DocumentKey(documentKey.PartitionKey, documentKey.RowKey)).Any());
    }

    /// <inheritdoc />
    public Task UpsertAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        return Task.Run(() =>
                this.Context.AddOrUpdate(entity.Clone(), new DocumentKey(documentKey.PartitionKey, documentKey.RowKey)),
            cancellationToken);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task DeleteAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        return Task.Run(() => this.Context.Delete<T>(new DocumentKey(documentKey.PartitionKey, documentKey.RowKey)),
            cancellationToken);
    }
}
