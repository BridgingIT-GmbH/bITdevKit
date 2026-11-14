// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores a resumable bounded maintenance or invalidation operation.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheOperationEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Operations")]
[Index(nameof(CollectionId), nameof(State), nameof(UpdatedTicks), nameof(Id), Name = "IX___Caching_Operations_CollectionId_State_UpdatedTicks_Id")]
[Index(nameof(State), nameof(ClaimUntilTicks), nameof(Id), Name = "IX___Caching_Operations_State_ClaimUntilTicks_Id")]
public sealed class CacheOperationEntity
{
    /// <summary>Gets or sets the canonical identifier of this row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string Id { get; set; }

    /// <summary>Gets or sets the canonical collection digest.</summary>
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string CollectionId { get; set; }

    /// <summary>Gets or sets the mutation revision used by the native store authority.</summary>
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string Revision { get; set; }

    /// <summary>Gets or sets the serialized provider-neutral metadata snapshot.</summary>
    [Required]
    public string MetadataJson { get; set; }

    /// <summary>Gets or sets the persisted lifecycle state code interpreted by the provider.</summary>
    [Required]
    public int State { get; set; }

    /// <summary>Gets or sets the UTC creation timestamp in ticks.</summary>
    [Required]
    public long CreatedTicks { get; set; }

    /// <summary>Gets or sets the UTC timestamp in ticks of the last operation-state update.</summary>
    [Required]
    public long UpdatedTicks { get; set; }

    /// <summary>Gets or sets the optional maintenance node holding the operation claim.</summary>
    [MaxLength(128)]
    [Unicode(true)]
    public string ClaimNode { get; set; }

    /// <summary>Gets or sets the optional UTC claim-expiration timestamp in ticks.</summary>
    public long? ClaimUntilTicks { get; set; }
}