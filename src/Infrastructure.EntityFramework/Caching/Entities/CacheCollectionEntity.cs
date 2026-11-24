// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores the logical collection identity, invalidation epoch and shared quota accounting.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheCollectionEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Collections")]
public sealed class CacheCollectionEntity
{
    /// <summary>Gets or sets the canonical identifier of this row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string Id { get; set; }

    /// <summary>Gets or sets the canonical namespace and collection identity used to detect digest collisions.</summary>
    [Required]
    [MaxLength(1024)]
    [Unicode(true)]
    public string CanonicalIdentity { get; set; }

    /// <summary>Gets or sets the configured maximum accounted storage bytes.</summary>
    [Required]
    public long MaximumBytes { get; set; }

    /// <summary>Gets or sets the configured maximum current entry count.</summary>
    [Required]
    public long MaximumEntries { get; set; }

    /// <summary>Gets or sets the invalidation epoch observed by publication fences.</summary>
    [Required]
    public long Epoch { get; set; }

    /// <summary>Gets or sets the mutation revision used by the native store authority.</summary>
    [Required]
    public long Revision { get; set; }

    /// <summary>Gets or sets the accounted bytes belonging to current payload generations.</summary>
    [Required]
    public long CurrentBytes { get; set; }

    /// <summary>Gets or sets the accounted bytes awaiting retired-generation reclamation.</summary>
    [Required]
    public long RetiredBytes { get; set; }

    /// <summary>Gets or sets the accounted bytes reserved by unfinished staging operations.</summary>
    [Required]
    public long StagingBytes { get; set; }

    /// <summary>Gets or sets the accounted metadata bytes for this row or scope.</summary>
    [Required]
    public long MetadataBytes { get; set; }

    /// <summary>Gets or sets the current published entry count.</summary>
    [Required]
    public long CurrentEntries { get; set; }
}