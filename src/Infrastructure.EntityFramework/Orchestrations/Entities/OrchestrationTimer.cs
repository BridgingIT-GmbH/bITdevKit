// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Application.Orchestrations;

[Table("__Orchestration_Timers")]
[Index(nameof(Status), nameof(DueTimeUtc))]
[Index(nameof(InstanceId), nameof(Status), nameof(DueTimeUtc))]
/// <summary>
/// Represents a durable orchestration timer row stored in Entity Framework persistence.
/// </summary>
public class OrchestrationTimer
{
    /// <summary>
    /// Gets or sets the timer identifier.
    /// </summary>
    [Key]
    public Guid TimerId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    [Required]
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the timer trigger kind.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string TriggerKind { get; set; }

    /// <summary>
    /// Gets or sets the UTC due time.
    /// </summary>
    [Required]
    public DateTimeOffset DueTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional timer target state.
    /// </summary>
    [MaxLength(256)]
    public string TargetState { get; set; }

    /// <summary>
    /// Gets or sets optional continuation metadata.
    /// </summary>
    [MaxLength(2048)]
    public string Continuation { get; set; }

    /// <summary>
    /// Gets or sets the current timer status.
    /// </summary>
    [Required]
    public OrchestrationTimerStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the timer was finalized.
    /// </summary>
    public DateTimeOffset? ProcessedUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional status reason.
    /// </summary>
    [MaxLength(4000)]
    public string StatusReason { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    [MaxLength(256)]
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral concurrency token used by EF Core.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}