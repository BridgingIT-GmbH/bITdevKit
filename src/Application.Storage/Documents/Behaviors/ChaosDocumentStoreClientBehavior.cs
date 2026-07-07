// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Injects synthetic faults into document-store operations for resilience testing.
/// </summary>
/// <typeparam name="T">The document type handled by the decorated client.</typeparam>
public class ChaosDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChaosDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
    public ChaosDocumentStoreClientBehavior(
        ILoggerFactory loggerFactory,
        IDocumentStoreClient<T> inner,
        ChaosDocumentStoreClientBehaviorOptions options = null)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<ChaosDocumentStoreClientBehavior<T>>() ??
            NullLoggerFactory.Instance.CreateLogger<ChaosDocumentStoreClientBehavior<T>>();
        this.Inner = inner;
        this.Options = options ?? new ChaosDocumentStoreClientBehaviorOptions();
    }

    /// <summary>
    /// Gets the logger used by the behavior.
    /// </summary>
    protected ILogger<ChaosDocumentStoreClientBehavior<T>> Logger { get; }

    /// <summary>
    /// Gets the chaos settings used by the behavior.
    /// </summary>
    protected ChaosDocumentStoreClientBehaviorOptions Options { get; }

    /// <summary>
    /// Gets the decorated inner client.
    /// </summary>
    protected IDocumentStoreClient<T> Inner { get; }

    /// <inheritdoc />
    public Task<Result<T>> GetResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(() => this.Inner.GetResultAsync(documentKey, cancellationToken));

    /// <inheritdoc />
    public Task<Result<DocumentPage<T>>> FindPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(() => this.Inner.FindPageResultAsync(query, cancellationToken));

    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(() => this.Inner.ListPageResultAsync(query, cancellationToken));

    /// <inheritdoc />
    public Task<Result<long>> CountResultAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(() => this.Inner.CountResultAsync(query, cancellationToken));

    /// <inheritdoc />
    public Task<Result<bool>> ExistsResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(() => this.Inner.ExistsResultAsync(documentKey, cancellationToken));

    /// <inheritdoc />
    public Task<Result> UpsertResultAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(() => this.Inner.UpsertResultAsync(documentKey, entity, cancellationToken));

    /// <inheritdoc />
    public Task<Result> UpsertResultAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(() => this.Inner.UpsertResultAsync(entities, cancellationToken));

    /// <inheritdoc />
    public Task<Result> DeleteResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(() => this.Inner.DeleteResultAsync(documentKey, cancellationToken));

    private Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation)
    {
        if (this.Options.InjectionRate > 0 && Random.Shared.NextDouble() < this.Options.InjectionRate)
        {
            throw this.Options.Fault ?? new ChaosException();
        }

        return operation();
    }
}
