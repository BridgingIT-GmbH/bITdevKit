// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Represents one ordered content chunk for an Entity Framework backed blob.
/// </summary>
/// <example>
/// <code>
/// var chunk = new StorageBlobChunk
/// {
///     BlobId = blob.Id,
///     Index = 0,
///     Content = new byte[] { 1, 2, 3 },
///     Length = 3
/// };
/// </code>
/// </example>
[Table("__Storage_BlobChunks")]
[PrimaryKey(nameof(BlobId), nameof(Index))]
[Index(nameof(BlobId), nameof(Index), IsUnique = true)]
public sealed class StorageBlobChunk
{
    /// <summary>
    /// Gets or sets the blob identifier that owns this chunk.
    /// </summary>
    /// <example>
    /// <code>
    /// chunk.BlobId = blob.Id;
    /// </code>
    /// </example>
    [Required]
    [MaxLength(64)]
    public string BlobId { get; set; }

    /// <summary>
    /// Gets or sets the zero-based chunk index within the owning blob.
    /// </summary>
    /// <example>
    /// <code>
    /// chunk.Index = 0;
    /// </code>
    /// </example>
    [Required]
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the binary chunk payload.
    /// </summary>
    /// <example>
    /// <code>
    /// chunk.Content = new byte[] { 1, 2, 3 };
    /// </code>
    /// </example>
    [Required]
    public byte[] Content { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of valid bytes in <see cref="Content" />.
    /// </summary>
    /// <example>
    /// <code>
    /// chunk.Length = chunk.Content.Length;
    /// </code>
    /// </example>
    [Required]
    public int Length { get; set; }

    /// <summary>
    /// Gets or sets the metadata row that owns this chunk.
    /// </summary>
    /// <example>
    /// <code>
    /// var blob = chunk.Blob;
    /// </code>
    /// </example>
    [Required]
    [ForeignKey(nameof(BlobId))]
    [InverseProperty(nameof(StorageBlob.Chunks))]
    public StorageBlob Blob { get; set; }
}
