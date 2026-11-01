// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>Owns one immutable action occurrence with scalar copies and lossless JSON.</summary>
/// <example><code>var entity = new AuditActionEntity();</code></example>
[Table("__Auditing_Actions")]
[Index(nameof(AuditId), nameof(Ordinal), IsUnique = true)]
public sealed class AuditActionEntity
{
    /// <summary>Stable action occurrence identity.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Owning execution identity.</summary>
    /// <example><code>var value = entity.AuditId;</code></example>
    public Guid AuditId { get; set; }

    /// <summary>Non-owning parent occurrence identity.</summary>
    /// <example><code>var value = entity.ParentActionId;</code></example>
    public Guid? ParentActionId { get; set; }

    /// <summary>Original retained-array ordinal for lossless reconstruction.</summary>
    /// <example><code>var value = entity.Ordinal;</code></example>
    public int Ordinal { get; set; }

    /// <summary>Observed action admission sequence.</summary>
    /// <example><code>var value = entity.Sequence;</code></example>
    public long Sequence { get; set; }

    /// <summary>Captured business/source action key.</summary>
    /// <example><code>var value = entity.Key;</code></example>
    [MaxLength(512)]
    public string Key { get; set; }

    /// <summary>Captured action outcome.</summary>
    /// <example><code>var value = entity.Outcome;</code></example>
    [MaxLength(32)]
    public string Outcome { get; set; }

    /// <summary>Captured start UTC ticks.</summary>
    /// <example><code>var value = entity.StartedUtcTicks;</code></example>
    public long StartedUtcTicks { get; set; }

    /// <summary>Captured completion UTC ticks.</summary>
    /// <example><code>var value = entity.CompletedUtcTicks;</code></example>
    public long CompletedUtcTicks { get; set; }

    /// <summary>Complete original versioned action fragment.</summary>
    /// <example><code>var value = entity.DocumentJson;</code></example>
    [Required]
    public string DocumentJson { get; set; }

    /// <summary>Normalized scalar/detail/resource/error projection.</summary>
    /// <example><code>var value = entity.SearchJson;</code></example>
    [Required]
    public string SearchJson { get; set; }

    /// <summary>Digest computed from the actual database projection stream during a bounded read, never a stored trust anchor.</summary>
    /// <example><code>var actual = entity.SearchProjectionHash;</code></example>
    [NotMapped]
    public string SearchProjectionHash { get; set; }
}
