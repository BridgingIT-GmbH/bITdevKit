// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores the canonical logical-key identity and invalidation epoch across contract versions.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheKeyEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Keys")]
[Index(nameof(CollectionId), nameof(Id), Name = "IX___Caching_Keys_CollectionId_Id")]
public sealed class CacheKeyEntity
{
    /// <summary>Gets or sets the canonical identifier of this row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string Id { get; set; }

    /// <summary>Gets or sets the canonical collection digest.</summary>
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string CollectionId { get; set; }

    /// <summary>Gets or sets the canonical logical key used to verify digest collisions.</summary>
    [Required]
    [MaxLength(4096)]
    [Unicode(true)]
    public string CanonicalKey { get; set; }

    /// <summary>Gets or sets the invalidation epoch observed by publication fences.</summary>
    [Required]
    public long Epoch { get; set; }

}