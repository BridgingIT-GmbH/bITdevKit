// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>Owns one immutable logical header and provider lifecycle/publication metadata.</summary>
/// <example><code>var entity = new AuditRecordEntity();</code></example>
[Table("__Auditing_Records")]
[Index(nameof(Position), IsUnique = true)]
[Index(nameof(StorageState), nameof(StartedUtcTicks), nameof(AuditId))]
[Index(nameof(StorageState), nameof(CompletedUtcTicks))]
[Index(nameof(OriginAuditId))]
[Index(nameof(CausationAuditId))]
public sealed class AuditRecordEntity
{
    /// <summary>Stable captured execution identity.</summary>
    /// <example><code>var value = entity.AuditId;</code></example>
    [Key]
    public Guid AuditId { get; set; }

    /// <summary>Versioned source document format.</summary>
    /// <example><code>var value = entity.SchemaVersion;</code></example>
    public int SchemaVersion { get; set; }

    /// <summary>Original SHA-256 document identity.</summary>
    /// <example><code>var value = entity.ContentHash;</code></example>
    [Required]
    [MaxLength(64)]
    public string ContentHash { get; set; }

    /// <summary>Lossless original root fragment, excluding owned child arrays and hash/envelope.</summary>
    /// <example><code>var value = entity.RootJson;</code></example>
    [Required]
    public string RootJson { get; set; }

    /// <summary>Optional original integrity envelope, committed with the complete graph.</summary>
    /// <example><code>var value = entity.IntegrityJson;</code></example>
    public string IntegrityJson { get; set; }

    /// <summary>Explicit normalized search projection, outside the authenticated document.</summary>
    /// <example><code>var value = entity.SearchJson;</code></example>
    [Required]
    public string SearchJson { get; set; }

    /// <summary>Committed publication order allocated under the store gate.</summary>
    /// <example><code>var value = entity.Position;</code></example>
    public long Position { get; set; }

    /// <summary>Provider lifecycle membership, separate from immutable facts.</summary>
    /// <example><code>var value = entity.StorageState;</code></example>
    public AuditStorageState StorageState { get; set; } = AuditStorageState.Active;

    /// <summary>UTC archive transition time outside protected facts.</summary>
    /// <example><code>var value = entity.ArchivedUtcTicks;</code></example>
    public long? ArchivedUtcTicks { get; set; }

    /// <summary>Searchable copy of root start UTC ticks.</summary>
    /// <example><code>var value = entity.StartedUtcTicks;</code></example>
    public long StartedUtcTicks { get; set; }

    /// <summary>Searchable copy of finalized UTC ticks and retention anchor.</summary>
    /// <example><code>var value = entity.CompletedUtcTicks;</code></example>
    public long CompletedUtcTicks { get; set; }

    /// <summary>Searchable captured business key.</summary>
    /// <example><code>var value = entity.Key;</code></example>
    [Required]
    [MaxLength(512)]
    public string Key { get; set; }

    /// <summary>Bounded captured display label.</summary>
    /// <example><code>var value = entity.DisplayName;</code></example>
    [MaxLength(1024)]
    public string DisplayName { get; set; }

    /// <summary>Captured execution kind.</summary>
    /// <example><code>var value = entity.Kind;</code></example>
    [MaxLength(128)]
    public string Kind { get; set; }

    /// <summary>Captured entry module.</summary>
    /// <example><code>var value = entity.ModuleName;</code></example>
    [MaxLength(512)]
    public string ModuleName { get; set; }

    /// <summary>Captured root outcome.</summary>
    /// <example><code>var value = entity.Outcome;</code></example>
    [MaxLength(32)]
    public string Outcome { get; set; }

    /// <summary>Captured completeness.</summary>
    /// <example><code>var value = entity.Completeness;</code></example>
    [MaxLength(32)]
    public string Completeness { get; set; }

    /// <summary>Non-owning trail origin identity.</summary>
    /// <example><code>var value = entity.OriginAuditId;</code></example>
    public Guid OriginAuditId { get; set; }

    /// <summary>Non-owning immediate cause identity.</summary>
    /// <example><code>var value = entity.CausationAuditId;</code></example>
    public Guid? CausationAuditId { get; set; }

    /// <summary>Opaque captured correlation identity.</summary>
    /// <example><code>var value = entity.CorrelationId;</code></example>
    [MaxLength(128)]
    public string CorrelationId { get; set; }

    /// <summary>Exact searchable executing actor ID.</summary>
    /// <example><code>var value = entity.ActorUserId;</code></example>
    [MaxLength(1024)]
    public string ActorUserId { get; set; }

    /// <summary>Exact searchable initiating actor ID.</summary>
    /// <example><code>var value = entity.InitiatingUserId;</code></example>
    [MaxLength(1024)]
    public string InitiatingUserId { get; set; }

    /// <summary>Frozen actor snapshot for payload-free summary projections.</summary>
    /// <example><code>var value = entity.ActorJson;</code></example>
    [Required]
    public string ActorJson { get; set; }

    /// <summary>Optional frozen initiating actor summary snapshot.</summary>
    /// <example><code>var value = entity.InitiatingActorJson;</code></example>
    public string InitiatingActorJson { get; set; }

    /// <summary>Observed action admission count.</summary>
    /// <example><code>var value = entity.TotalActionCount;</code></example>
    public long TotalActionCount { get; set; }

    /// <summary>Owned retained action count.</summary>
    /// <example><code>var value = entity.RetainedActionCount;</code></example>
    public long RetainedActionCount { get; set; }

    /// <summary>Owned retained change group count.</summary>
    /// <example><code>var value = entity.ChangeCount;</code></example>
    public long ChangeCount { get; set; }

    /// <summary>Observed retained-child failure flag.</summary>
    /// <example><code>var value = entity.HasFailedActions;</code></example>
    public bool HasFailedActions { get; set; }

    /// <summary>Owned retained action rows.</summary>
    /// <example><code>var value = entity.Actions;</code></example>
    [ForeignKey(nameof(AuditActionEntity.AuditId))]
    public ICollection<AuditActionEntity> Actions { get; set; } = new List<AuditActionEntity>();

    /// <summary>Owned retained change group rows.</summary>
    /// <example><code>var value = entity.EntityChanges;</code></example>
    [ForeignKey(nameof(AuditEntityChangeEntity.AuditId))]
    public ICollection<AuditEntityChangeEntity> EntityChanges { get; set; } = new List<AuditEntityChangeEntity>();

    /// <summary>Digest computed from the actual database projection stream during a bounded read, never a stored trust anchor.</summary>
    /// <example><code>var actual = entity.SearchProjectionHash;</code></example>
    [NotMapped]
    public string SearchProjectionHash { get; set; }
}
