// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents a metadata-only blob property update request.
/// </summary>
/// <example>
/// <code>
/// var request = new BlobStorageUpdatePropertiesRequestModel
/// {
///     Container = "reports",
///     Name = "2026/report.pdf",
///     ContentType = "application/pdf"
/// };
/// </code>
/// </example>
public class BlobStorageUpdatePropertiesRequestModel
{
    /// <summary>
    /// Gets or sets the blob container.
    /// </summary>
    /// <example>
    /// <code>
    /// var container = request.Container;
    /// </code>
    /// </example>
    public string Container { get; set; }

    /// <summary>
    /// Gets or sets the blob name.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = request.Name;
    /// </code>
    /// </example>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the replacement MIME content type. A null value clears the stored content type.
    /// </summary>
    /// <example>
    /// <code>
    /// request.ContentType = "text/plain";
    /// </code>
    /// </example>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the replacement UTC expiration timestamp. A null value clears expiration.
    /// </summary>
    /// <example>
    /// <code>
    /// request.ExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the replacement property bag.
    /// </summary>
    /// <example>
    /// <code>
    /// request.Properties["source"] = "maintenance";
    /// </code>
    /// </example>
    public Dictionary<string, object> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the optional entity tag required for optimistic updates.
    /// </summary>
    /// <example>
    /// <code>
    /// request.IfMatchETag = "\"abc\"";
    /// </code>
    /// </example>
    public string IfMatchETag { get; set; }
}
