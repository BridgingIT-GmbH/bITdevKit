// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Optional provider capability that reads many serialized documents by exact key with a bounded number of round trips.
/// </summary>
/// <remarks>
/// <see cref="DocumentStoreClient{T}" /> uses this contract when the provider implements it and otherwise reads key by key
/// with <see cref="IDocumentStoreProvider.GetAsync" />. Verification, transforms and deserialization stay in the client.
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyProvider : IDocumentStoreProvider, IDocumentStoreBatchGetProvider
/// {
///     public Task&lt;Result&lt;IReadOnlyList&lt;StoredDocument&gt;&gt;&gt; GetManyAsync(
///         DocumentTypeIdentity type,
///         IReadOnlyList&lt;DocumentKey&gt; keys,
///         DateTimeOffset visibilityCutoff,
///         CancellationToken cancellationToken = default) => ...;
/// }
/// </code>
/// </example>
public interface IDocumentStoreBatchGetProvider
{
    /// <summary>
    /// Gets the stored documents for the supplied distinct keys that are logically visible at the cutoff.
    /// </summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="keys">The distinct, valid exact keys to read.</param>
    /// <param name="visibilityCutoff">The UTC instant used to exclude documents whose expiration is due.</param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>
    /// The found documents in any order. Missing and expired documents are absent, the client maps them to not-found
    /// results. A failure means the whole batch could not be read.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await provider.GetManyAsync(type, keys, timeProvider.GetUtcNow(), cancellationToken);
    /// </code>
    /// </example>
    Task<Result<IReadOnlyList<StoredDocument>>> GetManyAsync(
        DocumentTypeIdentity type,
        IReadOnlyList<DocumentKey> keys,
        DateTimeOffset visibilityCutoff,
        CancellationToken cancellationToken = default);
}
