// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

using BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents provider-neutral document metadata returned from maintenance HTTP endpoints.
/// </summary>
/// <example><code>var model = new DocumentStorageDocumentInfoModel { Key = new DocumentKey("orders", "42"), ETag = "version-token" };</code></example>
public class DocumentStorageDocumentInfoModel
{
    /// <summary>Gets or sets the exact partition and row key identifying the document.</summary>
    /// <example><code>var key = model.Key;</code></example>
    public DocumentKey Key { get; set; }

    /// <summary>Gets or sets the opaque version token returned by the committed mutation.</summary>
    /// <example><code>var condition = model.ETag;</code></example>
    public string ETag { get; set; }

    /// <summary>Gets or sets the logical document content hash.</summary>
    /// <example><code>var hash = model.ContentHash;</code></example>
    public string ContentHash { get; set; }

    /// <summary>Gets or sets the logical serialized document length in bytes.</summary>
    /// <example><code>var length = model.ContentLength;</code></example>
    public long ContentLength { get; set; }

    /// <summary>Gets or sets the document creation timestamp.</summary>
    /// <example><code>var createdAt = model.CreatedAt;</code></example>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the document last-modified timestamp.</summary>
    /// <example><code>var modifiedAt = model.LastModifiedAt;</code></example>
    public DateTimeOffset LastModifiedAt { get; set; }

    /// <summary>Gets or sets the expiration timestamp, or null when the document does not expire.</summary>
    /// <example><code>var expiresAt = model.ExpiresAt;</code></example>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Gets or sets the scalar application properties represented as a JSON object.</summary>
    /// <example><code>model.Properties["status"] = "approved";</code></example>
    public Dictionary<string, object> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
