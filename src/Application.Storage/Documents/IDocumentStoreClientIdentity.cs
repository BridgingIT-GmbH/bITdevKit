// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Exposes the normalized identity of a configured Document Storage client.</summary>
/// <remarks>
/// Behaviors use this identity to isolate client-specific state such as cache entries. The identity contains no provider
/// credentials or document data.
/// </remarks>
/// <example><code>var name = ((IDocumentStoreClientIdentity)client).ClientName;</code></example>
public interface IDocumentStoreClientIdentity
{
    /// <summary>Gets the normalized named-client identity used by keyed dependency injection.</summary>
    /// <example><code>var name = identity.ClientName;</code></example>
    string ClientName { get; }
}
