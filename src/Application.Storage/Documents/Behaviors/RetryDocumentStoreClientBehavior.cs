// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;
using Humanizer;
using Polly;
using Polly.Retry;

/// <summary>
/// Retries failed document-store operations according to a Polly-based retry policy.
/// </summary>
/// <typeparam name="T">The document type handled by the decorated client.</typeparam>
public class RetryDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
    public RetryDocumentStoreClientBehavior(
        ILoggerFactory loggerFactory,
        IDocumentStoreClient<T> inner,
        RetryDocumentStoreClientBehaviorOptions options = null)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<RetryDocumentStoreClientBehavior<T>>() ??
            NullLoggerFactory.Instance.CreateLogger<RetryDocumentStoreClientBehavior<T>>();
        this.Inner = inner;
        this.Options = options ?? new RetryDocumentStoreClientBehaviorOptions();

        if (this.Options.Attempts <= 0)
        {
            this.Options.Attempts = 1;
        }
    }

    /// <summary>
    /// Gets the logger used by the behavior.
    /// </summary>
    protected ILogger<RetryDocumentStoreClientBehavior<T>> Logger { get; }

    /// <summary>
    /// Gets the decorated inner client.
    /// </summary>
    protected IDocumentStoreClient<T> Inner { get; }

    /// <summary>
    /// Gets the retry settings used by the behavior.
    /// </summary>
    protected RetryDocumentStoreClientBehaviorOptions Options { get; }

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

    private AsyncRetryPolicy PolicyFactory(RetryDocumentStoreClientBehaviorOptions options)
    {
        var attempts = 1;
        var retryCount = Math.Max(0, options.Attempts - 1);

        return Policy.Handle<Exception>()
            .WaitAndRetryAsync(retryCount,
                attempt => options.BackoffExponential
                    ? TimeSpan.FromMilliseconds((options.Backoff != default ? options.Backoff.TotalMilliseconds : 0) * Math.Pow(2, attempt - 1))
                    : TimeSpan.FromMilliseconds(options.Backoff != default ? options.Backoff.TotalMilliseconds : 0),
                (ex, wait) =>
                {
                    Activity.Current?.AddEvent(new ActivityEvent($"Retry (attempt=#{attempts}, type={this.GetType().Name}) {ex.Message}"));
                    this.Logger.LogError(ex,
                        "{LogKey} behavior retry (attempt={Attempt}, wait={Wait}, type={Type}) {Message}",
                        Constants.LogKey,
                        attempts,
                        wait.Humanize(),
                        this.GetType().Name,
                        ex.Message);
                    attempts++;
                });
    }
}
