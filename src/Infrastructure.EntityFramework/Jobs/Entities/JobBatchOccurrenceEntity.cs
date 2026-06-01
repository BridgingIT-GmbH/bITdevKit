// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_BatchOccurrences")]
[PrimaryKey(nameof(BatchId), nameof(OccurrenceId))]
[Index(nameof(BatchId), nameof(Sequence))]
[Index(nameof(OccurrenceId))]
/// <summary>
/// Represents a durable batch-membership row.
/// </summary>
public class JobBatchOccurrenceEntity
{
    [Required]
    public Guid BatchId { get; set; }

    [Required]
    public Guid OccurrenceId { get; set; }

    [Required]
    public JobOccurrenceStatus ChildStatus { get; set; }

    public int? Sequence { get; set; }

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
