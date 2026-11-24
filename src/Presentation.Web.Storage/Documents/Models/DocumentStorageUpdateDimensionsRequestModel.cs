// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

using Application.Storage;

/// <summary>Supplies an atomic replacement of document Dimensions with an optional whole-item condition.</summary>
/// <example><code>var request = new DocumentStorageUpdateDimensionsRequestModel { Dimensions = DocumentDimensions.Empty };</code></example>
public sealed class DocumentStorageUpdateDimensionsRequestModel
{
    /// <summary>Gets or sets the exact partition key.</summary>
    public string PartitionKey { get; set; }

    /// <summary>Gets or sets the exact row key.</summary>
    public string RowKey { get; set; }

    /// <summary>Gets or sets replacement Dimensions; empty clears and null is invalid for this operation.</summary>
    public DocumentDimensions Dimensions { get; set; }

    /// <summary>Gets or sets the originally read opaque ETag; a conflicting If-Match header is invalid.</summary>
    public string IfMatchETag { get; set; }
}
