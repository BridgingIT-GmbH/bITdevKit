// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Optional provider capability that applies many serialized document writes or deletes with a bounded number of round trips.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DocumentStoreClient{T}" /> uses this contract for <c>UpsertManyAsync</c> and <c>DeleteManyAsync</c> when the
/// provider implements it and otherwise applies the operations one by one.
/// </para>
/// <para>
/// Implementations must keep the semantics of the per-operation path: operations apply in input order, an operation sees the
/// effect of the earlier operations of the same call (including duplicate keys), the call stops at the first failing operation,
/// and the operations before it stay committed. The failing key is reported in <see cref="DocumentBatchResult{T}.FailedKey" />
/// and the failure messages are carried on the returned result.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyProvider : IDocumentStoreProvider, IDocumentStoreBulkWriteProvider
/// {
///     public Task&lt;Result&lt;DocumentBatchResult&lt;DocumentInfo&gt;&gt;&gt; UpsertManyAsync(
///         DocumentTypeIdentity type,
///         IReadOnlyList&lt;StoredDocumentWrite&gt; writes,
///         CancellationToken cancellationToken = default) => ...;
/// }
/// </code>
/// </example>
public interface IDocumentStoreBulkWriteProvider
{
    /// <summary>Writes the supplied documents in order and stops at the first failing write.</summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="writes">The validated serialized writes to apply in order.</param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>The metadata of the committed writes and, when a write failed, its key.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.UpsertManyAsync(type, writes, cancellationToken);
    /// var failed = result.Value.FailedKey;
    /// </code>
    /// </example>
    Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(
        DocumentTypeIdentity type,
        IReadOnlyList<StoredDocumentWrite> writes,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the supplied documents in order and stops at the first failing delete.</summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="deletes">The validated deletes to apply in order.</param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>The keys of the completed deletes and, when a delete failed, its key.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.DeleteManyAsync(type, deletes, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(
        DocumentTypeIdentity type,
        IReadOnlyList<DocumentDelete> deletes,
        CancellationToken cancellationToken = default);
}
