// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Represents a persisted virtual directory row for the Entity Framework file storage provider.
/// </summary>
[Table("__Storage_Directories")]
[Index(nameof(LocationName), nameof(NormalizedPathHash), IsUnique = true)]
[Index(nameof(LocationName), nameof(ParentPathHash), nameof(Name))]
[Index(nameof(LocationName), nameof(LockedUntil))]
[Index(nameof(LocationName), nameof(IsExplicit), nameof(LastModified))]
public class FileStorageDirectoryEntity
{
    /// <summary>
    /// Gets or sets the unique identifier of the directory row.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the logical storage location name that owns the directory.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string LocationName { get; set; }

    /// <summary>
    /// Gets or sets the normalized logical path of the directory.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string NormalizedPath { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the normalized directory path.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string NormalizedPathHash { get; set; }

    /// <summary>
    /// Gets or sets the normalized parent directory path.
    /// </summary>
    [MaxLength(2048)]
    public string ParentPath { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the normalized parent directory path.
    /// </summary>
    [MaxLength(64)]
    public string ParentPathHash { get; set; }

    /// <summary>
    /// Gets or sets the final directory name segment.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the directory was created explicitly by a caller.
    /// </summary>
    [Required]
    public bool IsExplicit { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp tracked for the directory.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the logical owner of the active lease for the directory row.
    /// </summary>
    [MaxLength(256)]
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the timestamp until which the directory lease is valid.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the optimistic concurrency token for the directory row.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();
}
