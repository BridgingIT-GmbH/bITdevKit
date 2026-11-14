// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores the current published cache metadata and its immutable payload reference.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheEntryEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Entries")]
[Index(nameof(PayloadId), Name = "IX___Caching_Entries_PayloadId")]
[Index(nameof(CollectionId), nameof(PublicationSequence), nameof(Id), Name = "IX___Caching_Entries_CollectionId_PublicationSequence_Id")]
[Index(nameof(PurgeAfterTicks), nameof(Id), Name = "IX___Caching_Entries_PurgeAfterTicks_Id")]
[Index(nameof(CollectionId), nameof(Priority), nameof(LastAccessTicks), nameof(Id), Name = "IX___Caching_Entries_CollectionId_Priority_LastAccessTicks_Id")]
[Index(nameof(CollectionId), nameof(KeyId), Name = "IX___Caching_Entries_CollectionId_KeyId")]
[Index(nameof(CollectionId), nameof(ContractId), nameof(KeyPrefix), nameof(Id), Name = "IX___Caching_Entries_CollectionId_ContractId_KeyPrefix_Id")]
[Index(nameof(CollectionId), nameof(PublishedTicks), nameof(Id), Name = "IX___Caching_Entries_CollectionId_PublishedTicks_Id")]
[Index(nameof(CollectionId), nameof(ExpiryTicks), nameof(Id), Name = "IX___Caching_Entries_CollectionId_ExpiryTicks_Id")]
[Index(nameof(CollectionId), nameof(StoredBytes), nameof(Id), Name = "IX___Caching_Entries_CollectionId_StoredBytes_Id")]
[Index(nameof(CollectionId), nameof(HasForegroundAccess), nameof(RefreshDueTicks), nameof(Id), Name = "IX___Caching_Entries_CollectionId_HasForegroundAcc_927582618536")]
[Index(nameof(CollectionId), nameof(ContractId), nameof(HasForegroundAccess), nameof(AbsoluteExpiryTicks), nameof(Id), Name = "IX___Caching_Entries_CollectionId_ContractId_HasFo_7d9c3f4ac379")]
public sealed class CacheEntryEntity
{
    /// <summary>Gets or sets the deterministic SHA-256 digest of the canonical namespace, collection, contract version and key.</summary>
    /// <remarks>All nodes derive this logical address without a surrogate-key lookup. This digest is not a generated GUID. The canonical token annotation stores ASCII bytes so database collation cannot change equality.</remarks>
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

    /// <summary>Gets or sets the canonical collection and contract digest.</summary>
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string ContractId { get; set; }

    /// <summary>Gets or sets the canonical logical-key digest shared across contract versions.</summary>
    [Required]
    [MaxLength(64)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string KeyId { get; set; }

    /// <summary>Gets or sets the immutable payload generation identifier used as the opaque safe-read and conditional-write token.</summary>
    [MaxLength(32)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string PayloadId { get; set; }

    /// <summary>Gets or sets the bounded ASCII hexadecimal key prefix used for binary candidate selection.</summary>
    [Required]
    [MaxLength(512)]
    [Unicode(false)]
    [CacheCanonicalToken]
    public string KeyPrefix { get; set; }

    /// <summary>Gets or sets the serialized provider-neutral metadata snapshot.</summary>
    [Required]
    public string MetadataJson { get; set; }

    /// <summary>Gets or sets the UTC publication timestamp in ticks.</summary>
    [Required]
    public long PublishedTicks { get; set; }

    /// <summary>Gets or sets the monotonic collection publication sequence.</summary>
    [Required]
    public long PublicationSequence { get; set; }

    /// <summary>Gets or sets the UTC expiration timestamp in ticks.</summary>
    [Required]
    public long ExpiryTicks { get; set; }

    /// <summary>Gets or sets the stale-retention grace duration in ticks.</summary>
    [Required]
    public long RetentionGraceTicks { get; set; }

    /// <summary>Gets or sets the UTC timestamp in ticks after which the entry can be purged.</summary>
    [Required]
    public long PurgeAfterTicks { get; set; }

    /// <summary>Gets or sets the immutable UTC absolute-expiration boundary in ticks.</summary>
    [Required]
    public long AbsoluteExpiryTicks { get; set; }

    /// <summary>Gets or sets the last acknowledged foreground-access UTC timestamp in ticks.</summary>
    [Required]
    public long LastAccessTicks { get; set; }

    /// <summary>Gets or sets the optional UTC timestamp in ticks when refresh becomes eligible.</summary>
    public long? RefreshDueTicks { get; set; }

    /// <summary>Gets or sets the optional UTC retry boundary in ticks after a refresh failure.</summary>
    public long? RefreshBackoffTicks { get; set; }

    /// <summary>Gets or sets a value indicating whether a foreground caller has accessed this generation.</summary>
    [Required]
    public bool HasForegroundAccess { get; set; }

    /// <summary>Gets or sets the stored payload size in bytes.</summary>
    [Required]
    public long StoredBytes { get; set; }

    /// <summary>Gets or sets the accounted metadata bytes for this row or scope.</summary>
    [Required]
    public long MetadataBytes { get; set; }

    /// <summary>Gets or sets the eviction priority used by bounded pressure reclamation.</summary>
    [Required]
    public int Priority { get; set; }

    /// <summary>Gets or sets a value indicating whether the publication represents an explicit negative result.</summary>
    [Required]
    public bool IsNotFound { get; set; }

    /// <summary>Gets or sets the referenced immutable payload row.</summary>
    [ForeignKey(nameof(PayloadId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CachePayloadEntity Payload { get; set; }
}