// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Replaces or clears Dimensions under the existing whole-item version.</summary>
/// <example><code>var update = new BlobDimensionsUpdate { Key = key, Dimensions = BlobDimensions.Empty, IfMatchETag = etag };</code></example>
public sealed record BlobDimensionsUpdate
{
    /// <summary>Gets the exact owning record key.</summary>
    public BlobKey Key { get; init; }

    /// <summary>Gets the replacement collection; null is invalid and empty clears.</summary>
    public BlobDimensions Dimensions { get; init; }

    /// <summary>Gets the optional exact whole-item condition.</summary>
    public string IfMatchETag { get; init; }
}
