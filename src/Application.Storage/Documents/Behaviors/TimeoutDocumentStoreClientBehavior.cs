// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Humanizer;
using Polly;
using Polly.Timeout;

/// <summary>
/// Enforces a maximum execution time for document-store operations.
/// </summary>
/// <typeparam name="T">The document type handled by the decorated client.</typeparam>
public class TimeoutDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
    public TimeoutDocumentStoreClientBehavior(
        ILoggerFactory loggerFactory,
        IDocumentStoreClient<T> inner,
        TimeoutDocumentStoreClientBehaviorOptions options = null)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<TimeoutDocumentStoreClientBehavior<T>>() ??
            NullLoggerFactory.Instance.CreateLogger<TimeoutDocumentStoreClientBehavior<T>>();
        this.Inner = inner;
        this.Options = options ?? new TimeoutDocumentStoreClientBehaviorOptions();
    }

    /// <summary>
    /// Gets the logger used by the behavior.
    /// </summary>
    protected ILogger<TimeoutDocumentStoreClientBehavior<T>> Logger { get; }

    /// <summary>
    /// Gets the timeout settings used by the behavior.
    /// </summary>
    protected TimeoutDocumentStoreClientBehaviorOptions Options { get; }

    /// <summary>
    /// Gets the decorated inner client.
    /// </summary>
    protected IDocumentStoreClient<T> Inner { get; }

    /// <inheritdoc />
    public Task<Result<T>> GetResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.PolicyFactory(this.Options).ExecuteAsync(_ => this.Inner.GetResultAsync(documentKey, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<DocumentPage<T>>> FindPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        this.PolicyFactory(this.Options).ExecuteAsync(_ => this.Inner.FindPageResultAsync(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        this.PolicyFactory(this.Options).ExecuteAsync(_ => this.Inner.ListPageResultAsync(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> CountResultAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) =>
        this.PolicyFactory(this.Options).ExecuteAsync(_ => this.Inner.CountResultAsync(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> ExistsResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.PolicyFactory(this.Options).ExecuteAsync(_ => this.Inner.ExistsResultAsync(documentKey, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> UpsertResultAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default) =>
        this.PolicyFactory(this.Options).ExecuteAsync(_ => this.Inner.UpsertResultAsync(documentKey, entity, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> UpsertResultAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default) =>
        this.PolicyFactory(this.Options).ExecuteAsync(_ => this.Inner.UpsertResultAsync(entities, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.PolicyFactory(this.Options).ExecuteAsync(_ => this.Inner.DeleteResultAsync(documentKey, cancellationToken), cancellationToken);

    private AsyncTimeoutPolicy PolicyFactory(TimeoutDocumentStoreClientBehaviorOptions options)
    {
        return Policy.TimeoutAsync(options.Timeout,
            TimeoutStrategy.Pessimistic,
            async (_, timeout, _) => await Task.Run(() =>
                this.Logger.LogError(
                    "{LogKey} behavior timeout reached (timeout={Timeout}, type={Type})",
                    Constants.LogKey,
                    timeout.Humanize(),
                    this.GetType().Name)));
    }
}
