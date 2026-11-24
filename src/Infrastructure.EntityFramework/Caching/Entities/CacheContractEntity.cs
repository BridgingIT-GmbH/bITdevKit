// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>Stores a retained value contract and dimension schema within a logical collection.</summary>
/// <remarks>Mutations belong to the advanced cache provider; hosts expose this entity through their cache-capable DbContext.</remarks>
/// <example><code>var rows = await context.Set&lt;CacheContractEntity&gt;().AsNoTracking().Take(10).ToListAsync(token);</code></example>
[Table("__Caching_Contracts")]
[Index(nameof(CollectionId), nameof(ContractVersion), Name = "IX___Caching_Contracts_CollectionId_ContractVersion")]
public sealed class CacheContractEntity
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

    /// <summary>Gets or sets the canonical value-contract version.</summary>
    [Required]
    [MaxLength(128)]
    [Unicode(true)]
    public string ContractVersion { get; set; }

    /// <summary>Gets or sets the configured stable value-contract alias.</summary>
    [Required]
    [MaxLength(128)]
    [Unicode(true)]
    public string ContractAlias { get; set; }

    /// <summary>Gets or sets the serialized dimension schema retained for compatibility checks.</summary>
    [Required]
    public string SchemaJson { get; set; }

    /// <summary>Gets or sets the serialized effective collection policies.</summary>
    [Required]
    public string PolicyJson { get; set; }

    /// <summary>Gets or sets the persisted cache metadata format version.</summary>
    [Required]
    public int MetadataFormat { get; set; }
}