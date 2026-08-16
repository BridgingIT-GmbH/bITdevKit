// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Application.Jobs;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents an append-only batch-history row.
/// </summary>
[Table("__Jobs_BatchHistory")]
[Index(nameof(BatchId), nameof(RecordedAt))]
[Index(nameof(EventName), nameof(RecordedAt))]
public class JobBatchHistoryEntity
{
    /// <summary>
    /// Gets or sets the history id.
    /// </summary>
    [Key]
    public Guid HistoryId { get; set; }

    /// <summary>
    /// Gets or sets the batch id.
    /// </summary>
    [Required]
    public Guid BatchId { get; set; }

    /// <summary>
    /// Gets or sets the external batch id.
    /// </summary>
    [MaxLength(256)]
    public string ExternalBatchId { get; set; }

    /// <summary>
    /// Gets or sets the event name.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the batch status.
    /// </summary>
    public JobBatchStatus? BatchStatus { get; set; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    [MaxLength(4000)]
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the serialized properties.
    /// </summary>
    public string SerializedProperties { get; set; }

    /// <summary>
    /// Gets or sets the recorded at.
    /// </summary>
    [Required]
    public DateTimeOffset RecordedAt { get; set; }
}
