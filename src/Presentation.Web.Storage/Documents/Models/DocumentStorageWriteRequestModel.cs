// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;

/// <summary>Supplies typed document JSON and optional metadata for a maintenance content write.</summary>
/// <example><code>var request = new DocumentStorageWriteRequestModel { Content = JsonSerializer.SerializeToElement(new { Name = "Ada" }) };</code></example>
public class DocumentStorageWriteRequestModel
{
    /// <summary>Gets or sets optional Dimensions. Omission/null preserves; an empty object clears.</summary>
    public DocumentDimensions Dimensions { get; set; }

    /// <summary>Gets or sets the document payload; it must deserialize as the selected registered document type.</summary>
    /// <example><code>request.Content = JsonSerializer.SerializeToElement(new { Name = "Ada" });</code></example>
    public JsonElement Content { get; set; }

    /// <summary>Gets or sets replacement scalar properties; null preserves existing properties.</summary>
    /// <example><code>request.Properties = new() { ["reviewed"] = true };</code></example>
    public Dictionary<string, object> Properties { get; set; }

    /// <summary>Gets or sets an absolute expiration; omission preserves the existing expiration.</summary>
    /// <example><code>request.ExpiresAt = DateTimeOffset.UtcNow.AddDays(1);</code></example>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Gets or sets whether to clear expiration, mutually exclusive with ExpiresAt.</summary>
    /// <example><code>request.ClearExpiration = true;</code></example>
    public bool ClearExpiration { get; set; }
}
