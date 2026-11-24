// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Application.Storage;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>A scoped, database-generated identity for an immutable document dimension name.</summary>
/// <example><code>var names = await context.Set&lt;StorageDocumentDimensionName&gt;().AsNoTracking().Take(10).ToListAsync();</code></example>
[Table("__Storage_DocumentDimensionNames")]
[Index(nameof(ScopeHash), nameof(NameHash), IsUnique = true)]
public sealed class StorageDocumentDimensionName
{
    /// <summary>Gets or sets the database-generated identity.</summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>Gets or sets the full persisted-scope SHA-256 digest.</summary>
    [Required, MaxLength(32)]
    public byte[] ScopeHash { get; set; }

    /// <summary>Gets or sets the original persisted namespace for collision verification.</summary>
    [Required, MaxLength(256)]
    public string Scope { get; set; }

    /// <summary>Gets or sets the full canonical-name SHA-256 digest.</summary>
    [Required, MaxLength(32)]
    public byte[] NameHash { get; set; }

    /// <summary>Gets or sets original name spelling for identity verification.</summary>
    [Required, MaxLength(128)]
    public string Name { get; set; }

    /// <summary>Gets or sets values referring to this retained name.</summary>
    public ICollection<StorageDocumentDimension> Values { get; set; } = [];
}
