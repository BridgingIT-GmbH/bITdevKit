// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents one persisted property-level ChangeHistory row.
/// </summary>
/// <example>
/// <code>
/// var rows = await dbContext.Set&lt;ChangeHistoryEntry&gt;()
///     .Where(e =&gt; e.EntityId == customerId.ToString())
///     .ToListAsync();
/// </code>
/// </example>
[Table("__ChangeHistory_Entries")]
[Index(nameof(EntityType), nameof(EntityId), nameof(ChangedDateTicks), IsDescending = new[] { false, false, true })]
[Index(nameof(ChangeSetId), nameof(ChangeSetSequence))]
[Index(nameof(BulkOperationId))]
[Index(nameof(EntityType), nameof(EntityId), nameof(PropertyName), nameof(ChangedDateTicks), IsDescending = new[] { false, false, false, true })]
[Index(nameof(ChangedByUserId), nameof(ChangedDateTicks), IsDescending = new[] { false, true })]
[Index(nameof(CorrelationId))]
[Index(nameof(ModuleName), nameof(ChangedDateTicks), IsDescending = new[] { false, true })]
public class ChangeHistoryEntry
{
    /// <summary>
    /// Gets or sets the row identifier.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the change set identifier shared by rows captured together.
    /// </summary>
    [Required]
    public Guid ChangeSetId { get; set; }

    /// <summary>
    /// Gets or sets the property order inside the change set.
    /// </summary>
    [Required]
    public int ChangeSetSequence { get; set; }

    /// <summary>
    /// Gets or sets the short entity type name.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the entity CLR type token.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string EntityClrType { get; set; }

    /// <summary>
    /// Gets or sets the string form of the entity id.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets the entity id CLR type token.
    /// </summary>
    [MaxLength(512)]
    public string EntityIdType { get; set; }

    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the full property path.
    /// </summary>
    [MaxLength(1024)]
    public string PropertyPath { get; set; }

    /// <summary>
    /// Gets or sets the path kind.
    /// </summary>
    [MaxLength(64)]
    public string PathKind { get; set; }

    /// <summary>
    /// Gets or sets the collection action.
    /// </summary>
    [MaxLength(64)]
    public string CollectionAction { get; set; }

    /// <summary>
    /// Gets or sets the collection item id.
    /// </summary>
    [MaxLength(512)]
    public string CollectionItemId { get; set; }

    /// <summary>
    /// Gets or sets the value CLR type token.
    /// </summary>
    [MaxLength(2048)]
    public string ValueClrType { get; set; }

    /// <summary>
    /// Gets or sets the serialized old value.
    /// </summary>
    public string OldValue { get; set; }

    /// <summary>
    /// Gets or sets the serialized new value.
    /// </summary>
    public string NewValue { get; set; }

    /// <summary>
    /// Gets or sets the old value hash.
    /// </summary>
    [MaxLength(64)]
    public string OldValueHash { get; set; }

    /// <summary>
    /// Gets or sets the new value hash.
    /// </summary>
    [MaxLength(64)]
    public string NewValueHash { get; set; }

    /// <summary>
    /// Gets or sets the logical operation.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Operation { get; set; }

    /// <summary>
    /// Gets or sets the configured capture strategy.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string CaptureStrategy { get; set; }

    /// <summary>
    /// Gets or sets the capture source.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string CaptureSource { get; set; }

    /// <summary>
    /// Gets or sets the capture status.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string CaptureStatus { get; set; }

    /// <summary>
    /// Gets or sets optional capture diagnostics.
    /// </summary>
    [MaxLength(4000)]
    public string CaptureMessage { get; set; }

    /// <summary>
    /// Gets or sets the bulk operation id.
    /// </summary>
    public Guid? BulkOperationId { get; set; }

    /// <summary>
    /// Gets or sets the affected entity count.
    /// </summary>
    public int? AffectedEntityCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this row can participate in restore.
    /// </summary>
    public bool IsRestoreable { get; set; }

    /// <summary>
    /// Gets or sets the restore plan name.
    /// </summary>
    [MaxLength(256)]
    public string RestorePlanName { get; set; }

    /// <summary>
    /// Gets or sets the restore execution mode.
    /// </summary>
    [MaxLength(64)]
    public string RestoreExecutionMode { get; set; }

    /// <summary>
    /// Gets or sets the domain restore handler name.
    /// </summary>
    [MaxLength(256)]
    public string DomainRestoreHandlerName { get; set; }

    /// <summary>
    /// Gets or sets the user id that performed the change.
    /// </summary>
    [MaxLength(256)]
    public string ChangedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the user name that performed the change.
    /// </summary>
    [MaxLength(256)]
    public string ChangedByUserName { get; set; }

    /// <summary>
    /// Gets or sets the user email that performed the change.
    /// </summary>
    [MaxLength(512)]
    public string ChangedByEmail { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp for the change.
    /// </summary>
    [Required]
    public DateTimeOffset ChangedDate { get; set; }

    /// <summary>
    /// Gets or sets the UTC tick value used for provider-friendly ordering.
    /// </summary>
    [Required]
    public long ChangedDateTicks { get; set; }

    /// <summary>
    /// Gets or sets the optional change reason.
    /// </summary>
    [MaxLength(1024)]
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    [MaxLength(256)]
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the flow id.
    /// </summary>
    [MaxLength(256)]
    public string FlowId { get; set; }

    /// <summary>
    /// Gets or sets the module name.
    /// </summary>
    [MaxLength(256)]
    public string ModuleName { get; set; }

    /// <summary>
    /// Gets or sets the current activity parent id or activity id when no parent exists.
    /// </summary>
    [MaxLength(256)]
    public string ActivityParentId { get; set; }

    /// <summary>
    /// Gets or sets optional metadata JSON.
    /// </summary>
    public string Properties { get; set; }
}
