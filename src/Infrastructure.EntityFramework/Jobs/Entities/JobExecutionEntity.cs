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
/// Represents a durable execution-attempt row.
/// </summary>
[Table("__Jobs_Executions")]
[Index(nameof(OccurrenceId), nameof(AttemptNumber), IsUnique = true)]
[Index(nameof(JobName), nameof(TriggerName), nameof(StartedUtc))]
[Index(nameof(Status), nameof(StartedUtc))]
public class JobExecutionEntity
{
    /// <summary>
    /// Gets or sets the execution id.
    /// </summary>
    [Key]
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    [Required]
    public Guid OccurrenceId { get; set; }

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
    /// Gets or sets the attempt number.
    /// </summary>
    [Required]
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [Required]
    public JobExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the started utc.
    /// </summary>
    [Required]
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the completed utc.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    [MaxLength(2048)]
    public string Message { get; set; }

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
