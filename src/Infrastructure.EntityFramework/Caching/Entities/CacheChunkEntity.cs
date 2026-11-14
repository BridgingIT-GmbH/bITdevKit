// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores one bounded immutable payload chunk and its integrity digest.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheChunkEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Chunks")]
[PrimaryKey(nameof(PayloadId), nameof(Ordinal))]
[Index(nameof(PayloadId), nameof(Offset), Name = "IX___Caching_Chunks_PayloadId_Offset")]
public sealed class CacheChunkEntity
{
    /// <summary>Gets or sets the immutable payload generation identifier.</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string PayloadId { get; set; }

    /// <summary>Gets or sets the zero-based ordinal of this immutable chunk.</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    public int Ordinal { get; set; }

    /// <summary>Gets or sets the absolute byte offset of this immutable chunk.</summary>
    [Required]
    public long Offset { get; set; }

    /// <summary>Gets or sets the bounded immutable chunk bytes.</summary>
    [Required]
    [MaxLength(1048576)]
    public byte[] Data { get; set; }

    /// <summary>Gets or sets the canonical integrity digest of the chunk bytes.</summary>
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string Hash { get; set; }

    /// <summary>Gets or sets the referenced payload row.</summary>
    [ForeignKey(nameof(PayloadId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public CachePayloadEntity Payload { get; set; }
}