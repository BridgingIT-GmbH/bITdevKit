// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Application.Orchestrations;

[Table("__Orchestration_Signals")]
[Index(nameof(InstanceId), nameof(Status), nameof(ReceivedUtc))]
[Index(nameof(InstanceId), nameof(SignalName), nameof(ReceivedUtc))]
[Index(nameof(InstanceId), nameof(IdempotencyKey))]
/// <summary>
/// Represents a durable orchestration signal row stored in Entity Framework persistence.
/// </summary>
public class OrchestrationSignal
{
    /// <summary>
    /// Gets or sets the signal identifier.
    /// </summary>
    [Key]
    public Guid SignalId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    [Required]
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the signal name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string SignalName { get; set; }

    /// <summary>
    /// Gets or sets the state captured when the signal was accepted.
    /// </summary>
    [MaxLength(256)]
    public string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the serialized signal payload.
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// Gets or sets the payload type identifier.
    /// </summary>
    [MaxLength(2048)]
    public string PayloadType { get; set; }

    /// <summary>
    /// Gets or sets the optional idempotency key.
    /// </summary>
    [MaxLength(256)]
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the current signal status.
    /// </summary>
    [Required]
    public OrchestrationSignalStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the signal was received.
    /// </summary>
    [Required]
    public DateTimeOffset ReceivedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the signal was finalized.
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