// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Application.Jobs;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_OccurrenceDependencies")]
[Index(nameof(DependentOccurrenceId), nameof(Status))]
[Index(nameof(PrerequisiteOccurrenceId), nameof(Status))]
/// <summary>
/// Represents a durable occurrence dependency row.
/// </summary>
public class JobOccurrenceDependencyEntity
{
    [Key]
    public Guid DependencyId { get; set; }

    [Required]
    public Guid DependentOccurrenceId { get; set; }

    [Required]
    public Guid PrerequisiteOccurrenceId { get; set; }

    [Required]
    public string RequiredStatuses { get; set; }

    [Required]
    public JobDependencyStatus Status { get; set; }

    [Required]
    public JobDependencyFailurePolicy FailurePolicy { get; set; }

    [MaxLength(2048)]
    public string Reason { get; set; }

    public string SerializedProperties { get; set; }

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
