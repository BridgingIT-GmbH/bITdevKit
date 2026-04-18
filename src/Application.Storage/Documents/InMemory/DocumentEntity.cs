// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents a document persisted in the in-memory document-store provider.
/// </summary>
public class DocumentEntity
{
    /// <summary>
    /// Gets or sets the partition key of the document.
    /// </summary>
    public string PartitionKey { get; set; }

    /// <summary>
    /// Gets or sets the row key of the document.
    /// </summary>
    public string RowKey { get; set; }

    /// <summary>
    /// Gets or sets the stored document type name.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the document payload.
    /// </summary>
    public object Content { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the document was first created.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the document was last updated.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; set; } = DateTime.UtcNow;
}
