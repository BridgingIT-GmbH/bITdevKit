// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores a bounded reader lease protecting an immutable payload from reclamation.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheReaderEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Readers")]
[Index(nameof(PayloadId), nameof(ExpiryTicks), Name = "IX___Caching_Readers_PayloadId_ExpiryTicks")]
[Index(nameof(ExpiryTicks), nameof(Id), Name = "IX___Caching_Readers_ExpiryTicks_Id")]
public sealed class CacheReaderEntity
{
    /// <summary>Gets or sets the canonical identifier of this row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string Id { get; set; }

    /// <summary>Gets or sets the immutable payload generation identifier.</summary>
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string PayloadId { get; set; }

    /// <summary>Gets or sets the UTC expiration timestamp in ticks.</summary>
    [Required]
    public long ExpiryTicks { get; set; }

    /// <summary>Gets or sets the referenced payload row.</summary>
    [ForeignKey(nameof(PayloadId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CachePayloadEntity Payload { get; set; }
}