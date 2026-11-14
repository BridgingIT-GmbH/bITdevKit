// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores an immutable payload generation and its staged, current or retired lifecycle accounting.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CachePayloadEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Payloads")]
[Index(nameof(EntryId), nameof(State), nameof(Id), Name = "IX___Caching_Payloads_EntryId_State_Id")]
[Index(nameof(OperationId), Name = "IX___Caching_Payloads_OperationId", IsUnique = true)]
[Index(nameof(State), nameof(DeadlineTicks), nameof(Id), Name = "IX___Caching_Payloads_State_DeadlineTicks_Id")]
public sealed class CachePayloadEntity
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

    /// <summary>Gets or sets the canonical collection digest.</summary>
    [Required]
    [MaxLength(64)]
    [CacheCanonicalToken]
    public string CollectionId { get; set; }

    /// <summary>Gets or sets the accounted metadata bytes for this row or scope.</summary>
    [Required]
    public long MetadataBytes { get; set; }

    /// <summary>Gets or sets the unique staging or publication operation identifier.</summary>
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string OperationId { get; set; }

    /// <summary>Gets or sets the persisted lifecycle state code interpreted by the provider.</summary>
    [Required]
    public int State { get; set; }

    /// <summary>Gets or sets the complete immutable payload length in bytes.</summary>
    [Required]
    public long SizeBytes { get; set; }

    /// <summary>Gets or sets the storage reservation held until publication or proven reclamation.</summary>
    [Required]
    public long ReservedBytes { get; set; }

    /// <summary>Gets or sets the UTC creation timestamp in ticks.</summary>
    [Required]
    public long CreatedTicks { get; set; }

    /// <summary>Gets or sets the UTC ownership or retention deadline in ticks.</summary>
    [Required]
    public long DeadlineTicks { get; set; }

    /// <summary>Gets or sets the opaque owner token of the payload staging operation.</summary>
    [Required]
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string StagingOwner { get; set; }

    /// <summary>Gets or sets the optional bounded inline representation of a small payload.</summary>
    [MaxLength(65536)]
    public byte[] InlineBytes { get; set; }

    /// <summary>Gets or sets the optional canonical digest of the complete stored payload.</summary>
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string StoredHash { get; set; }
}