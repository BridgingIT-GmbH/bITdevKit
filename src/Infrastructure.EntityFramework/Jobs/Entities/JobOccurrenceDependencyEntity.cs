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
/// Represents a durable occurrence dependency row.
/// </summary>
[Table("__Jobs_OccurrenceDependencies")]
[Index(nameof(DependentOccurrenceId), nameof(Status))]
[Index(nameof(PrerequisiteOccurrenceId), nameof(Status))]
public class JobOccurrenceDependencyEntity
{
    /// <summary>
    /// Gets or sets the dependency id.
    /// </summary>
    [Key]
    public Guid DependencyId { get; set; }

    /// <summary>
    /// Gets or sets the dependent occurrence id.
    /// </summary>
    [Required]
    public Guid DependentOccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the prerequisite occurrence id.
    /// </summary>
    [Required]
    public Guid PrerequisiteOccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the required statuses.
    /// </summary>
    [Required]
    public string RequiredStatuses { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [Required]
    public JobDependencyStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the failure policy.
    /// </summary>
    [Required]
    public JobDependencyFailurePolicy FailurePolicy { get; set; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    [MaxLength(2048)]
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the serialized properties.
    /// </summary>
    public string SerializedProperties { get; set; }

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
