// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Represents the persisted payload row for a virtual file in the Entity Framework file storage provider.
/// </summary>
[Table("__Storage_FileContents")]
public class FileStorageFileContentEntity
{
    /// <summary>
    /// Gets or sets the file identifier.
    /// This value is both the primary key of the payload row and the foreign key to <see cref="FileStorageFileEntity" />.
    /// </summary>
    [Key]
    [ForeignKey(nameof(File))]
    public Guid FileId { get; set; }

    /// <summary>
    /// Gets or sets the text payload when the file is stored as text.
    /// </summary>
    public string ContentText { get; set; }

    /// <summary>
    /// Gets or sets the encoding name used for a text payload.
    /// </summary>
    [MaxLength(256)]
    public string TextEncodingName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the original text payload contained a byte-order mark.
    /// </summary>
    [Required]
    public bool TextHasByteOrderMark { get; set; }

    /// <summary>
    /// Gets or sets the binary payload when the file is stored as binary.
    /// </summary>
    public byte[] ContentBinary { get; set; }

    /// <summary>
    /// Gets or sets the principal file row that owns this content row.
    /// </summary>
    [Required]
    [InverseProperty(nameof(FileStorageFileEntity.Content))]
    public FileStorageFileEntity File { get; set; }
}
