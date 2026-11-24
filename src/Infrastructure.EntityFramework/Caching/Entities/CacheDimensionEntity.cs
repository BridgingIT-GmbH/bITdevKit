// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores an indexed dimension candidate associated with one cache entry.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheDimensionEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Dimensions")]
[PrimaryKey(nameof(EntryId), nameof(NameHash), nameof(Ordinal))]
[Index(nameof(CollectionId), nameof(NameHash), nameof(Kind), nameof(Form), nameof(ValueHash), nameof(EntryId), Name = "IX___Caching_Dimensions_CollectionId_NameHash_Kind_0e56c8669181")]
[Index(nameof(CollectionId), nameof(NameHash), nameof(Kind), nameof(NumericValue), nameof(IntervalEnd), nameof(EntryId), Name = "IX___Caching_Dimensions_CollectionId_NameHash_Kind_8a342904a7e2")]
public sealed class CacheDimensionEntity
{
    /// <summary>Gets or sets the canonical entry digest.</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string EntryId { get; set; }

    /// <summary>Gets or sets the canonical collection digest.</summary>
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string CollectionId { get; set; }

    /// <summary>Gets or sets the canonical digest of the dimension name.</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string NameHash { get; set; }

    /// <summary>Gets or sets the zero-based item ordinal, or minus one for a dimension-presence marker.</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    public int Ordinal { get; set; }

    /// <summary>Gets or sets the optional canonical digest of a dimension value.</summary>
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string ValueHash { get; set; }

    /// <summary>Gets or sets the persisted dimension value-kind code.</summary>
    [Required]
    public int Kind { get; set; }

    /// <summary>Gets or sets the persisted scalar, set or interval form code.</summary>
    [Required]
    public int Form { get; set; }

    /// <summary>Gets or sets a value indicating whether this dimension value explicitly represents null.</summary>
    [Required]
    public bool IsNull { get; set; }

    /// <summary>Gets or sets the optional scalar value or inclusive interval start.</summary>
    public long? NumericValue { get; set; }

    /// <summary>Gets or sets the optional exclusive interval end.</summary>
    public long? IntervalEnd { get; set; }

    /// <summary>Gets or sets the referenced entry row.</summary>
    [ForeignKey(nameof(EntryId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public CacheEntryEntity Entry { get; set; }
}