// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Replaces or clears Dimensions under the existing whole-item version.</summary>
/// <example><code>var update = new DocumentDimensionsUpdate { Key = key, Dimensions = DocumentDimensions.Empty, IfMatchETag = etag };</code></example>
public sealed record DocumentDimensionsUpdate
{
    /// <summary>Gets the exact owning record key.</summary>
    public DocumentKey Key { get; init; }

    /// <summary>Gets the replacement collection; null is invalid and empty clears.</summary>
    public DocumentDimensions Dimensions { get; init; }

    /// <summary>Gets the optional exact whole-item condition.</summary>
    public string IfMatchETag { get; init; }
}
