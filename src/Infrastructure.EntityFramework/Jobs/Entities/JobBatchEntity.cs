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
/// Represents a durable batch row stored by the Entity Framework jobs provider.
/// </summary>
[Table("__Jobs_Batches")]
[Index(nameof(ExternalBatchId), IsUnique = true)]
[Index(nameof(Status), nameof(CreatedDate))]
[Index(nameof(ArchivedDate))]
[Index(nameof(CorrelationId))]
[Index(nameof(IdempotencyKey))]
public class JobBatchEntity
{
    /// <summary>
    /// Gets or sets the batch id.
    /// </summary>
    [Key]
    public Guid BatchId { get; set; }

    /// <summary>
    /// Gets or sets the external batch id.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ExternalBatchId { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    [MaxLength(512)]
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [Required]
    public JobBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the completion policy.
    /// </summary>
    [Required]
    public JobBatchCompletionPolicy CompletionPolicy { get; set; }

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
    /// Gets or sets the accepted count.
    /// </summary>
    [Required]
    public int AcceptedCount { get; set; }

    /// <summary>
    /// Gets or sets the succeeded count.
    /// </summary>
    [Required]
    public int SucceededCount { get; set; }

    /// <summary>
    /// Gets or sets the failed count.
    /// </summary>
    [Required]
    public int FailedCount { get; set; }

    /// <summary>
    /// Gets or sets the cancelled count.
    /// </summary>
    [Required]
    public int CancelledCount { get; set; }

    /// <summary>
    /// Gets or sets the archived count.
    /// </summary>
    [Required]
    public int ArchivedCount { get; set; }

    /// <summary>
    /// Gets or sets the cancellation requested date.
    /// </summary>
    public DateTimeOffset? CancellationRequestedDate { get; set; }

    /// <summary>
    /// Gets or sets the archived date.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    /// <summary>
    /// Gets or sets the completed date.
    /// </summary>
    public DateTimeOffset? CompletedDate { get; set; }

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
