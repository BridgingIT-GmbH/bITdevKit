// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents provider-neutral blob metadata returned from HTTP endpoints.
/// </summary>
/// <example>
/// <code>
/// var model = new BlobStorageBlobInfoModel { Container = "reports", Name = "2026/report.pdf" };
/// </code>
/// </example>
public class BlobStorageBlobInfoModel
{
    /// <summary>
    /// Gets or sets the blob container.
    /// </summary>
    /// <example>
    /// <code>
    /// var container = model.Container;
    /// </code>
    /// </example>
    public string Container { get; set; }

    /// <summary>
    /// Gets or sets the blob name.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = model.Name;
    /// </code>
    /// </example>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the blob content length in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var length = model.Length;
    /// </code>
    /// </example>
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var mimeType = model.ContentType;
    /// </code>
    /// </example>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral content hash when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var hash = model.ContentHash;
    /// </code>
    /// </example>
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the provider-dependent entity tag when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var etag = model.ETag;
    /// </code>
    /// </example>
    public string ETag { get; set; }

    /// <summary>
    /// Gets or sets the provider creation timestamp when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var createdAt = model.CreatedAt;
    /// </code>
    /// </example>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the provider last-modified timestamp when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var lastModifiedAt = model.LastModifiedAt;
    /// </code>
    /// </example>
    public DateTimeOffset? LastModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC expiration timestamp when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var expiresAt = model.ExpiresAt;
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the custom blob properties.
    /// </summary>
    /// <example>
    /// <code>
    /// var source = model.Properties["source"];
    /// </code>
    /// </example>
    public Dictionary<string, object> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
