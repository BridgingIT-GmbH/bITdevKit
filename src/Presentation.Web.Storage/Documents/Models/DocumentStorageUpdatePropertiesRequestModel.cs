// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>Changes document properties and expiration while retaining document content.</summary>
/// <example><code>var request = new DocumentStorageUpdatePropertiesRequestModel { Properties = new() { ["reviewed"] = true } };</code></example>
public class DocumentStorageUpdatePropertiesRequestModel
{
    /// <summary>Gets or sets replacement scalar properties; null preserves the current bag.</summary>
    /// <example><code>request.Properties = new() { ["reviewed"] = true };</code></example>
    public Dictionary<string, object> Properties { get; set; }

    /// <summary>Gets or sets an absolute expiration; omission preserves the current expiration.</summary>
    /// <example><code>request.ExpiresAt = DateTimeOffset.UtcNow.AddDays(1);</code></example>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Gets or sets whether to clear expiration, mutually exclusive with ExpiresAt.</summary>
    /// <example><code>request.ClearExpiration = true;</code></example>
    public bool ClearExpiration { get; set; }
}
