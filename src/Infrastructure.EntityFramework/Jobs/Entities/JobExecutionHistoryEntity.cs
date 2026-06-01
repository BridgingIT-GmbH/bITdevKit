// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_ExecutionHistory")]
[Index(nameof(OccurrenceId), nameof(RecordedAt))]
[Index(nameof(ExecutionId), nameof(RecordedAt))]
[Index(nameof(EventName), nameof(RecordedAt))]
/// <summary>
/// Represents an append-only execution-history row.
/// </summary>
public class JobExecutionHistoryEntity
{
    [Key]
    public Guid HistoryId { get; set; }

    [Required]
    public Guid OccurrenceId { get; set; }

    public Guid? ExecutionId { get; set; }

    [Required]
    [MaxLength(256)]
    public string JobName { get; set; }

    [Required]
    [MaxLength(256)]
    public string TriggerName { get; set; }

    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    [Required]
    [MaxLength(128)]
    public string EventName { get; set; }

    public JobOccurrenceStatus? OccurrenceStatus { get; set; }

    public JobExecutionStatus? ExecutionStatus { get; set; }

    [MaxLength(4000)]
    public string Message { get; set; }

    [Required]
    public DateTimeOffset RecordedAt { get; set; }

    [MaxLength(256)]
    public string RecordedBy { get; set; }

    public string SerializedProperties { get; set; }
}
