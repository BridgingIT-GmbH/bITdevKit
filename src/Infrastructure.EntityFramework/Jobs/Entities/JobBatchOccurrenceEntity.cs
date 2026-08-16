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
/// Represents a durable batch-membership row.
/// </summary>
[Table("__Jobs_BatchOccurrences")]
[PrimaryKey(nameof(BatchId), nameof(OccurrenceId))]
[Index(nameof(BatchId), nameof(Sequence))]
[Index(nameof(OccurrenceId))]
public class JobBatchOccurrenceEntity
{
    /// <summary>
    /// Gets or sets the batch id.
    /// </summary>
    [Required]
    public Guid BatchId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    [Required]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the child status.
    /// </summary>
    [Required]
    public JobOccurrenceStatus ChildStatus { get; set; }

    /// <summary>
    /// Gets or sets the sequence.
    /// </summary>
    public int? Sequence { get; set; }

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
