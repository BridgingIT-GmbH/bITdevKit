// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;

/// <summary>
/// Injects synthetic faults into document-store operations for resilience testing.
/// </summary>
/// <typeparam name="T">The document type handled by the decorated client.</typeparam>
/// <example>
/// <code>
/// services.AddDocumentStoreClient&lt;Person&gt;(sp => client)
///     .WithBehavior((inner, sp) => new ChaosDocumentStoreClientBehavior&lt;Person&gt;(
///         sp.GetRequiredService&lt;ILoggerFactory&gt;(),
///         inner,
///         new ChaosDocumentStoreClientBehaviorOptions
///         {
///             InjectionRate = 0.1
///         }));
/// </code>
/// </example>
public class ChaosDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChaosDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the behavior logger.</param>
    /// <param name="inner">The inner client to decorate.</param>
    /// <param name="options">The chaos behavior options.</param>
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
    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .Execute(async context => await this.Inner.DeleteAsync(documentKey, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async context => await this.Inner.FindAsync(cancellationToken), cancellationToken)
            .Result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async context => await this.Inner.FindAsync(documentKey, cancellationToken),
                cancellationToken)
            .Result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async context => await this.Inner.FindAsync(documentKey, filter, cancellationToken),
                cancellationToken)
            .Result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async context => await this.Inner.ListAsync(cancellationToken), cancellationToken)
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
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async context => await this.Inner.ListAsync(documentKey, filter, cancellationToken),
                cancellationToken)
            .Result;
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async context => await this.Inner.CountAsync(cancellationToken), cancellationToken)
            .Result;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async context => await this.Inner.ExistsAsync(documentKey, cancellationToken),
                cancellationToken)
            .Result;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .Execute(async context => await this.Inner.UpsertAsync(documentKey, entity, cancellationToken),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .Execute(async context => await this.Inner.UpsertAsync(entities, cancellationToken), cancellationToken);
    }

    private InjectOutcomePolicy PolicyFactory(ChaosDocumentStoreClientBehaviorOptions options)
    {
        return MonkeyPolicy.InjectException(with => // https://github.com/Polly-Contrib/Simmy#Inject-exception
            with.Fault(options.Fault ?? new ChaosException()).InjectionRate(options.InjectionRate).Enabled());
    }
}
