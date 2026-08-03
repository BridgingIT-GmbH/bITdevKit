// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Provides complete forwarding behavior for document client decorators.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>public sealed class AuditBehavior&lt;T&gt;(IDocumentStoreClient&lt;T&gt; inner) : DocumentStoreClientBehaviorBase&lt;T&gt;(inner) where T : class, new();</code></example>
public abstract class DocumentStoreClientBehaviorBase<T>(IDocumentStoreClient<T> inner) : IDocumentStoreClient<T>, IDocumentStoreProviderAccessor, IDocumentStoreClientIdentity, IDocumentStoreClientDecorator<T> where T : class, new()
{
    /// <summary>Gets the decorated client.</summary>
    protected IDocumentStoreClient<T> Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));
    /// <inheritdoc />
    public IDocumentStoreClient<T> InnerClient => this.Inner;
    IDocumentStoreProvider IDocumentStoreProviderAccessor.Provider =>
        (this.Inner as IDocumentStoreProviderAccessor)?.Provider
        ?? throw new InvalidOperationException("The decorated document client does not expose its provider.");
    string IDocumentStoreClientIdentity.ClientName =>
        (this.Inner as IDocumentStoreClientIdentity)?.ClientName ?? "default";
    /// <inheritdoc />
    public virtual Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.Inner.GetAsync(key, cancellationToken);
    /// <inheritdoc />
    public virtual Task<Result<DocumentPage<T>>> FindPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.Inner.FindPageAsync(query, cancellationToken);
    /// <inheritdoc />
    public virtual Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.Inner.ListPageAsync(query, cancellationToken);
    /// <inheritdoc />
    public virtual Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) => this.Inner.CountAsync(query, cancellationToken);
    /// <inheritdoc />
    public virtual Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.Inner.ExistsAsync(key, cancellationToken);
    /// <inheritdoc />
    public virtual Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default) => this.Inner.UpsertAsync(key, value, options, cancellationToken);
    /// <inheritdoc />
    public virtual Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(IReadOnlyCollection<DocumentWrite<T>> writes, CancellationToken cancellationToken = default) => this.Inner.UpsertManyAsync(writes, cancellationToken);
    /// <inheritdoc />
    public virtual Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default) => this.Inner.UpdatePropertiesAsync(update, cancellationToken);
    /// <inheritdoc />
    public virtual Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => this.Inner.DeleteAsync(key, options, cancellationToken);
    /// <inheritdoc />
    public virtual Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(IReadOnlyCollection<DocumentDelete> deletes, CancellationToken cancellationToken = default) => this.Inner.DeleteManyAsync(deletes, cancellationToken);
}
