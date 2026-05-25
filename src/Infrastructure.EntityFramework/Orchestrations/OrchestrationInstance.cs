// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Application.Orchestrations;

[Table("__Orchestration_Instances")]
[Index(nameof(OrchestrationName), nameof(Status), nameof(StartedUtc))]
[Index(nameof(Status), nameof(StartedUtc))]
[Index(nameof(IsArchived), nameof(Status), nameof(StartedUtc))]
[Index(nameof(IsArchived), nameof(ArchivedUtc))]
[Index(nameof(CurrentState), nameof(Status))]
[Index(nameof(CorrelationId))]
[Index(nameof(ConcurrencyKey))]
/// <summary>
/// Represents the durable orchestration instance row stored in Entity Framework persistence.
/// </summary>
public class OrchestrationInstance
{
    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    [Key]
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration definition name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string OrchestrationName { get; set; }

    /// <summary>
    /// Gets or sets the current lifecycle status.
    /// </summary>
    [Required]
    public OrchestrationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the current business state name.
    /// </summary>
    [MaxLength(256)]
    public string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the current activity name.
    /// </summary>
    [MaxLength(256)]
    public string CurrentActivity { get; set; }

    /// <summary>
    /// Gets or sets the orchestration correlation identifier.
    /// </summary>
    [MaxLength(256)]
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the optional concurrency key.
    /// </summary>
    [MaxLength(256)]
    public string ConcurrencyKey { get; set; }

    /// <summary>
    /// Gets or sets the orchestration start timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the orchestration completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the orchestration data type identifier.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string ContextType { get; set; }

    /// <summary>
    /// Gets or sets the serialized durable orchestration context snapshot.
    /// </summary>
    [Required]
    public string SerializedContext { get; set; }

    /// <summary>
    /// Gets or sets the logical optimistic concurrency version.
    /// </summary>
    [Required]
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the orchestration has been archived.
    /// </summary>
    [Required]
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the UTC archive timestamp when the orchestration has been archived.
    /// </summary>
    public DateTimeOffset? ArchivedUtc { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral concurrency token used by EF Core.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the current lease identifier when a worker owns the instance.
    /// </summary>
    public Guid? LeaseId { get; set; }

    /// <summary>
    /// Gets or sets the current lease owner identifier.
    /// </summary>
    [MaxLength(256)]
    public string LeaseOwner { get; set; }

    /// <summary>
    /// Gets or sets the lease acquisition timestamp.
    /// </summary>
    public DateTimeOffset? LeaseAcquiredUtc { get; set; }

    /// <summary>
    /// Gets or sets the lease expiration timestamp.
    /// </summary>
    public DateTimeOffset? LeaseExpiresUtc { get; set; }

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
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}