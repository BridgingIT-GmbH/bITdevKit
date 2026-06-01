// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_Occurrences")]
[Index(nameof(OccurrenceKey), IsUnique = true)]
[Index(nameof(Status), nameof(DueUtc))]
[Index(nameof(JobName), nameof(TriggerName), nameof(DueUtc))]
[Index(nameof(CorrelationId))]
[Index(nameof(IdempotencyKey))]
/// <summary>
/// Represents a durable occurrence row stored by the Entity Framework jobs provider.
/// </summary>
public class JobOccurrenceEntity
{
    [Key]
    public Guid OccurrenceId { get; set; }

    [Required]
    [MaxLength(512)]
    public string OccurrenceKey { get; set; }

    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    [Required]
    [MaxLength(256)]
    public string TriggerName { get; set; }

    [Required]
    public JobTriggerType TriggerType { get; set; }

    [Required]
    public JobOccurrenceStatus Status { get; set; }

    [Required]
    public DateTimeOffset DueUtc { get; set; }

    public DateTimeOffset? ScheduledUtc { get; set; }

    public string SerializedData { get; set; }

    [Required]
    [MaxLength(2048)]
    public string DataType { get; set; }

    public string SerializedProperties { get; set; }

    [MaxLength(256)]
    public string CorrelationId { get; set; }

    [MaxLength(256)]
    public string CausationId { get; set; }

    [MaxLength(256)]
    public string IdempotencyKey { get; set; }

    public JobOccurrenceStatus? ResumeStatus { get; set; }

    [MaxLength(2048)]
    public string BlockedReason { get; set; }

    [Required]
    public DateTimeOffset CreatedDate { get; set; }

    [Required]
    public DateTimeOffset UpdatedDate { get; set; }

    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}
