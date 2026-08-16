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
/// Represents an append-only execution-history row.
/// </summary>
[Table("__Jobs_ExecutionHistory")]
[Index(nameof(OccurrenceId), nameof(RecordedAt))]
[Index(nameof(ExecutionId), nameof(RecordedAt))]
[Index(nameof(EventName), nameof(RecordedAt))]
public class JobExecutionHistoryEntity
{
    /// <summary>
    /// Gets or sets the history id.
    /// </summary>
    [Key]
    public Guid HistoryId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    [Required]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the execution id.
    /// </summary>
    public Guid? ExecutionId { get; set; }

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
    /// Gets or sets the scheduler instance id.
    /// </summary>
    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the event name.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the occurrence status.
    /// </summary>
    public JobOccurrenceStatus? OccurrenceStatus { get; set; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public JobExecutionStatus? ExecutionStatus { get; set; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    [MaxLength(4000)]
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the recorded at.
    /// </summary>
    [Required]
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>
    /// Gets or sets the recorded by.
    /// </summary>
    [MaxLength(256)]
    public string RecordedBy { get; set; }

    /// <summary>
    /// Gets or sets the serialized properties.
    /// </summary>
    public string SerializedProperties { get; set; }
}
