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
/// <example>
/// <code>
/// services.AddEntityFrameworkDocumentStoreClient&lt;Person, AppDbContext&gt;()
///     .WithBehavior((inner, sp) => new RetryDocumentStoreClientBehavior&lt;Person&gt;(
///         sp.GetRequiredService&lt;ILoggerFactory&gt;(),
///         inner,
///         new RetryDocumentStoreClientBehaviorOptions
///         {
///             Attempts = 5,
///             Backoff = TimeSpan.FromMilliseconds(100),
///             BackoffExponential = true
///         }));
/// </code>
/// </example>
public class RetryDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the behavior logger.</param>
    /// <param name="inner">The inner client to decorate.</param>
    /// <param name="options">The retry behavior options.</param>
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
    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async context => await this.Inner.DeleteAsync(documentKey, cancellationToken),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
                .ExecuteAndCaptureAsync(async context => await this.Inner.FindAsync(cancellationToken),
                    cancellationToken))
            .Result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async context => await this.Inner.FindAsync(documentKey, cancellationToken),
                cancellationToken)).Result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async context => await this.Inner.FindAsync(documentKey, filter, cancellationToken),
                cancellationToken)).Result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken)
    {
        return (await this.PolicyFactory(this.Options)
                .ExecuteAndCaptureAsync(async context => await this.Inner.ListAsync(cancellationToken),
                    cancellationToken))
            .Result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ListAsync(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async context => await this.Inner.ListAsync(documentKey, filter, cancellationToken),
                cancellationToken)).Result;
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
                .ExecuteAndCaptureAsync(async context => await this.Inner.CountAsync(cancellationToken),
                    cancellationToken))
            .Result;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async context => await this.Inner.ExistsAsync(documentKey, cancellationToken),
                cancellationToken)).Result;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async context => await this.Inner.UpsertAsync(documentKey, entity, cancellationToken),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async context => await this.Inner.UpsertAsync(entities, cancellationToken),
                cancellationToken);
    }

    private AsyncRetryPolicy PolicyFactory(RetryDocumentStoreClientBehaviorOptions options)
    {
        var attempts = 1;
        if (!options.BackoffExponential)
        {
            return Policy.Handle<Exception>()
                .WaitAndRetryAsync(options.Attempts,
                    attempt => TimeSpan.FromMilliseconds(options.Backoff != default ? options.Backoff.Milliseconds : 0),
                    (ex, wait) =>
                    {
                        Activity.Current?.AddEvent(
                            new ActivityEvent($"Retry (attempt=#{attempts}, type={this.GetType().Name}) {ex.Message}"));
                        this.Logger.LogError(ex,
                            $"{{LogKey}} behavior retry (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}",
                            Constants.LogKey);
                        attempts++;
                    });
        }

        return Policy.Handle<Exception>()
            .WaitAndRetryAsync(options.Attempts,
                attempt => TimeSpan.FromMilliseconds(options.Backoff != default
                    ? options.Backoff.Milliseconds
                    : 0 * Math.Pow(2, attempt)),
                (ex, wait) =>
                {
                    Activity.Current?.AddEvent(
                        new ActivityEvent($"Retry (attempt=#{attempts}, type={this.GetType().Name}) {ex.Message}"));
                    this.Logger.LogError(ex,
                        $"{{LogKey}} behavior retry (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}",
                        Constants.LogKey);
                    attempts++;
                });
    }
}
