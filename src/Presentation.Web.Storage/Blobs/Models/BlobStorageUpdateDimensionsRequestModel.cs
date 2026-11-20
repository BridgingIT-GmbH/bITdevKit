// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

using Application.Storage;

/// <summary>Supplies an atomic replacement of blob Dimensions with an optional whole-item condition.</summary>
/// <example><code>var request = new BlobStorageUpdateDimensionsRequestModel { Dimensions = BlobDimensions.Empty };</code></example>
public sealed class BlobStorageUpdateDimensionsRequestModel
{
    /// <summary>Gets or sets the exact container.</summary>
    public string Container { get; set; }

    /// <summary>Gets or sets the exact blob name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets replacement Dimensions; empty clears and null is invalid for this operation.</summary>
    public BlobDimensions Dimensions { get; set; }

    /// <summary>Gets or sets the originally read opaque ETag; a conflicting If-Match header is invalid.</summary>
    public string IfMatchETag { get; set; }
}
