// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents a durable occurrence row stored by the Entity Framework jobs provider.
/// </summary>
[Table("__Jobs_Occurrences")]
[Index(nameof(OccurrenceKey), IsUnique = true)]
[Index(nameof(Status), nameof(DueUtc))]
[Index(nameof(JobName), nameof(TriggerName), nameof(DueUtc))]
[Index(nameof(CorrelationId))]
[Index(nameof(IdempotencyKey))]
public class JobOccurrenceEntity
{
    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    [Key]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence key.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string OccurrenceKey { get; set; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the trigger type.
    /// </summary>
    [Required]
    public JobTriggerType TriggerType { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [Required]
    public JobOccurrenceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the due utc.
    /// </summary>
    [Required]
    public DateTimeOffset DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the scheduled utc.
    /// </summary>
    public DateTimeOffset? ScheduledUtc { get; set; }

    /// <summary>
    /// Gets or sets the serialized data.
    /// </summary>
    public string SerializedData { get; set; }

    /// <summary>
    /// Gets or sets the data type.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string DataType { get; set; }

    /// <summary>
    /// Gets or sets the serialized properties.
    /// </summary>
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    [MaxLength(256)]
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the causation id.
    /// </summary>
    [MaxLength(256)]
    public string CausationId { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    [MaxLength(256)]
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the resume status.
    /// </summary>
    public JobOccurrenceStatus? ResumeStatus { get; set; }

    /// <summary>
    /// Gets or sets the blocked reason.
    /// </summary>
    [MaxLength(2048)]
    public string BlockedReason { get; set; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the concurrency version.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Executes the advance concurrency version operation.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}
