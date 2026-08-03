// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Exposes the dependency-injection-owned persistence provider associated with a resolved document client graph.
/// </summary>
/// <remarks>
/// This infrastructure contract allows retention and diagnostics to discover optional provider capabilities without
/// constructing a second provider or bypassing the configured client lifetime. Behaviors that decorate a client preserve
/// this accessor by forwarding the same provider instance. Application code should normally depend on
/// <see cref="IDocumentStoreClient{T}" /> instead.
/// </remarks>
/// <example>
/// <code>
/// if (client is IDocumentStoreProviderAccessor accessor &amp;&amp;
///     accessor.Provider is IDocumentStoreRetentionProvider retentionProvider)
/// {
///     await retentionProvider.SweepExpiredAsync(request, cancellationToken);
/// }
/// </code>
/// </example>
public interface IDocumentStoreProviderAccessor
{
    /// <summary>
    /// Gets the container-owned provider used by the resolved document client and its behavior pipeline.
    /// </summary>
    /// <remarks>The accessor does not transfer ownership; callers must not dispose the returned provider.</remarks>
    /// <example><code>var capabilities = accessor.Provider.Capabilities;</code></example>
    IDocumentStoreProvider Provider { get; }
}
