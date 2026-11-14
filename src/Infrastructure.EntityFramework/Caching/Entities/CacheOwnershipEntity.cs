// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores a durable expensive-load ownership attempt shared by participating application nodes.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheOwnershipEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Ownerships")]
[Index(nameof(EntryId), nameof(Independent), nameof(State), nameof(LeaseExpiryTicks), Name = "IX___Caching_Ownerships_EntryId_Independent_State__e0bbc98d7e0c")]
[Index(nameof(CollectionId), nameof(DeadlineTicks), nameof(Id), Name = "IX___Caching_Ownerships_CollectionId_DeadlineTicks_Id")]
public sealed class CacheOwnershipEntity
{
    /// <summary>Gets or sets the canonical identifier of this row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string Id { get; set; }

    /// <summary>Gets or sets the canonical entry digest.</summary>
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string EntryId { get; set; }

    /// <summary>Gets or sets the canonical logical-key digest shared across contract versions.</summary>
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string KeyId { get; set; }

    /// <summary>Gets or sets the canonical collection digest.</summary>
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string CollectionId { get; set; }

    /// <summary>Gets or sets the participating application node identifier.</summary>
    [Required]
    [MaxLength(128)]
    [Unicode(true)]
    public string NodeId { get; set; }

    /// <summary>Gets or sets the opaque token proving ownership of the expensive load.</summary>
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string OwnerToken { get; set; }

    /// <summary>Gets or sets the publication fence associated with this ownership attempt.</summary>
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string FenceId { get; set; }

    /// <summary>Gets or sets the persisted lifecycle state code interpreted by the provider.</summary>
    [Required]
    public int State { get; set; }

    /// <summary>Gets or sets the UTC lease-expiration timestamp in ticks.</summary>
    [Required]
    public long LeaseExpiryTicks { get; set; }

    /// <summary>Gets or sets the UTC ownership or retention deadline in ticks.</summary>
    [Required]
    public long DeadlineTicks { get; set; }

    /// <summary>Gets or sets the optional version published by a completed ownership attempt.</summary>
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string PublishedVersion { get; set; }

    /// <summary>Gets or sets the optional terminal cache error code shared with waiting callers.</summary>
    public int? FailureCode { get; set; }

    /// <summary>Gets or sets a value indicating whether the publication represents an explicit negative result.</summary>
    [Required]
    public bool IsNotFound { get; set; }

    /// <summary>Gets or sets a value indicating whether this attempt loads independently of a shared flight.</summary>
    [Required]
    public bool Independent { get; set; }

    /// <summary>Gets or sets the finite ASCII JSON array of node observation-group interests owned by this flight.</summary>
    /// <remarks>Children retain individual deadlines and conservative metadata charges. All changes use the same bounded native authority transaction as the owning flight.</remarks>
    [Required]
    [MaxLength(CacheOwnershipInterestState.MaximumJsonBytes)]
    [Unicode(false)]
    public string InterestsJson { get; set; } = "[]";
}