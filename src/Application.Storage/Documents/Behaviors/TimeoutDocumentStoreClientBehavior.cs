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
/// <example>
/// <code>
/// services.AddEntityFrameworkDocumentStoreClient&lt;Person, AppDbContext&gt;()
///     .WithBehavior((inner, sp) => new TimeoutDocumentStoreClientBehavior&lt;Person&gt;(
///         sp.GetRequiredService&lt;ILoggerFactory&gt;(),
///         inner,
///         new TimeoutDocumentStoreClientBehaviorOptions
///         {
///             Timeout = TimeSpan.FromSeconds(10)
///         }));
/// </code>
/// </example>
public class TimeoutDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the behavior logger.</param>
    /// <param name="inner">The inner client to decorate.</param>
    /// <param name="options">The timeout behavior options.</param>
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
    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async context => await this.Inner.DeleteAsync(documentKey, cancellationToken),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken)
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
    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken)
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

    private AsyncTimeoutPolicy PolicyFactory(TimeoutDocumentStoreClientBehaviorOptions options)
    {
        return Policy.TimeoutAsync(options.Timeout,
            TimeoutStrategy.Pessimistic,
            async (context, timeout, task) => await Task.Run(() =>
                this.Logger.LogError(
                    $"{{LogKey}} behavior timeout reached (timeout={timeout.Humanize()}, type={this.GetType().Name})",
                    Constants.LogKey)));
    }
}
