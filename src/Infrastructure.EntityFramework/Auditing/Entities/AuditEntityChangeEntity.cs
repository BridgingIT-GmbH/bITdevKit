// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>Owns one original optional source evidence group without depending on ChangeHistory tables.</summary>
/// <example><code>var entity = new AuditEntityChangeEntity();</code></example>
[Table("__Auditing_EntityChanges")]
[Index(nameof(AuditId), nameof(Ordinal), IsUnique = true)]
public sealed class AuditEntityChangeEntity
{
    /// <summary>Stable retained evidence identity.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Owning execution identity.</summary>
    /// <example><code>var value = entity.AuditId;</code></example>
    public Guid AuditId { get; set; }

    /// <summary>Associated captured action identity when retained.</summary>
    /// <example><code>var value = entity.ActionId;</code></example>
    public Guid? ActionId { get; set; }

    /// <summary>Original retained-array ordinal for lossless reconstruction.</summary>
    /// <example><code>var value = entity.Ordinal;</code></example>
    public int Ordinal { get; set; }

    /// <summary>Captured source evidence time.</summary>
    /// <example><code>var value = entity.TimestampUtcTicks;</code></example>
    public long TimestampUtcTicks { get; set; }

    /// <summary>Frozen source entity type alias.</summary>
    /// <example><code>var value = entity.EntityType;</code></example>
    [MaxLength(512)]
    public string EntityType { get; set; }

    /// <summary>Exact opaque source entity identity.</summary>
    /// <example><code>var value = entity.EntityId;</code></example>
    [MaxLength(512)]
    public string EntityId { get; set; }

    /// <summary>Confirmed or explicitly unknown source transaction state.</summary>
    /// <example><code>var value = entity.PersistenceState;</code></example>
    [MaxLength(32)]
    public string PersistenceState { get; set; }

    /// <summary>Complete original evidence/property fragment.</summary>
    /// <example><code>var value = entity.DocumentJson;</code></example>
    [Required]
    public string DocumentJson { get; set; }

    /// <summary>Normalized scalar/property projection.</summary>
    /// <example><code>var value = entity.SearchJson;</code></example>
    [Required]
    public string SearchJson { get; set; }

    /// <summary>Digest computed from the actual database projection stream during a bounded read, never a stored trust anchor.</summary>
    /// <example><code>var actual = entity.SearchProjectionHash;</code></example>
    [NotMapped]
    public string SearchProjectionHash { get; set; }
}
