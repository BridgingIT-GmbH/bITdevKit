// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores the captured authority required to reject obsolete cache publications.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheFenceEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Fences")]
[Index(nameof(EntryId), Name = "IX___Caching_Fences_EntryId")]
[Index(nameof(KeyId), nameof(Id), Name = "IX___Caching_Fences_KeyId_Id")]
[Index(nameof(CollectionId), nameof(DeadlineTicks), nameof(Id), Name = "IX___Caching_Fences_CollectionId_DeadlineTicks_Id")]
public sealed class CacheFenceEntity
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

    /// <summary>Gets or sets the logical-key invalidation epoch captured by the publication fence.</summary>
    [Required]
    public long KeyEpoch { get; set; }

    /// <summary>Gets or sets a value indicating whether a competing publication superseded the captured state.</summary>
    [Required]
    public bool PublishedSinceCapture { get; set; }

    /// <summary>Gets or sets the collection invalidation epoch captured by the publication fence.</summary>
    [Required]
    public long CollectionEpoch { get; set; }

    /// <summary>Gets or sets the optional publication version observed when the fence was captured.</summary>
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string ObservedVersion { get; set; }

    /// <summary>Gets or sets the observed UTC publication timestamp in ticks.</summary>
    [Required]
    public long ObservedPublicationTicks { get; set; }

    /// <summary>Gets or sets the UTC ownership or retention deadline in ticks.</summary>
    [Required]
    public long DeadlineTicks { get; set; }
}