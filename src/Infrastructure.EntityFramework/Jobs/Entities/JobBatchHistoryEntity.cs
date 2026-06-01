// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Application.Jobs;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_BatchHistory")]
[Index(nameof(BatchId), nameof(RecordedAt))]
[Index(nameof(EventName), nameof(RecordedAt))]
/// <summary>
/// Represents an append-only batch-history row.
/// </summary>
public class JobBatchHistoryEntity
{
    [Key]
    public Guid HistoryId { get; set; }

    [Required]
    public Guid BatchId { get; set; }

    [MaxLength(256)]
    public string ExternalBatchId { get; set; }

    [Required]
    [MaxLength(128)]
    public string EventName { get; set; }

    public JobBatchStatus? BatchStatus { get; set; }

    [MaxLength(4000)]
    public string Message { get; set; }

    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    public string SerializedProperties { get; set; }

    [Required]
    public DateTimeOffset RecordedAt { get; set; }
}
