// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_Executions")]
[Index(nameof(OccurrenceId), nameof(AttemptNumber), IsUnique = true)]
[Index(nameof(JobName), nameof(TriggerName), nameof(StartedUtc))]
[Index(nameof(Status), nameof(StartedUtc))]
/// <summary>
/// Represents a durable execution-attempt row.
/// </summary>
public class JobExecutionEntity
{
    [Key]
    public Guid ExecutionId { get; set; }

    [Required]
    public Guid OccurrenceId { get; set; }

    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    [Required]
    [MaxLength(256)]
    public string TriggerName { get; set; }

    [Required]
    public int AttemptNumber { get; set; }

    [Required]
    public JobExecutionStatus Status { get; set; }

    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    [Required]
    public DateTimeOffset StartedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }

    [MaxLength(2048)]
    public string Message { get; set; }

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
