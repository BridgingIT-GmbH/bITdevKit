// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Application.Jobs;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_Batches")]
[Index(nameof(ExternalBatchId), IsUnique = true)]
[Index(nameof(Status), nameof(CreatedDate))]
[Index(nameof(ArchivedDate))]
[Index(nameof(CorrelationId))]
[Index(nameof(IdempotencyKey))]
/// <summary>
/// Represents a durable batch row stored by the Entity Framework jobs provider.
/// </summary>
public class JobBatchEntity
{
    [Key]
    public Guid BatchId { get; set; }

    [Required]
    [MaxLength(256)]
    public string ExternalBatchId { get; set; }

    [MaxLength(512)]
    public string Description { get; set; }

    [Required]
    public JobBatchStatus Status { get; set; }

    [Required]
    public JobBatchCompletionPolicy CompletionPolicy { get; set; }

    public string SerializedProperties { get; set; }

    [MaxLength(256)]
    public string CorrelationId { get; set; }

    [MaxLength(256)]
    public string CausationId { get; set; }

    [MaxLength(256)]
    public string IdempotencyKey { get; set; }

    [Required]
    public int AcceptedCount { get; set; }

    [Required]
    public int SucceededCount { get; set; }

    [Required]
    public int FailedCount { get; set; }

    [Required]
    public int CancelledCount { get; set; }

    [Required]
    public int ArchivedCount { get; set; }

    public DateTimeOffset? CancellationRequestedDate { get; set; }

    public DateTimeOffset? ArchivedDate { get; set; }

    public DateTimeOffset? CompletedDate { get; set; }

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
