// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>Converts HTTP JSON metadata into scalar properties and expiration changes.</summary>
/// <example><code>var properties = HttpMetadata.ParseProperties("{\"reviewed\":true}");</code></example>
public static class HttpMetadata
{
    /// <summary>Parses a scalar property object; omitted metadata is empty unless preservation is requested.</summary>
    /// <example><code>var properties = HttpMetadata.ParseProperties(header);</code></example>
    public static Result<PropertyBag> ParseProperties(string json, bool preserveMissing = false) =>
        ScalarPropertyBagJson.Parse(json, preserveMissing);

    /// <summary>Resolves preserve, set, and clear expiration requests without ambiguous combinations.</summary>
    /// <example><code>var expiration = HttpMetadata.ResolveExpiration(null, true);</code></example>
    public static Result<ExpirationChange> ResolveExpiration(DateTimeOffset? expiresAt, bool clearExpiration) =>
        clearExpiration && expiresAt.HasValue
            ? Result<ExpirationChange>.Failure(new ValidationError("ClearExpiration and ExpiresAt cannot be combined."))
            : Result<ExpirationChange>.Success(clearExpiration ? ExpirationChange.Clear : expiresAt.HasValue ? ExpirationChange.At(expiresAt.Value) : ExpirationChange.Preserve);
}
