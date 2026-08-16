// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents a durable occurrence-lease row.
/// </summary>
[Table("__Jobs_Leases")]
[Index(nameof(ExpiresUtc))]
[Index(nameof(SchedulerInstanceId), nameof(ExpiresUtc))]
public class JobLeaseEntity
{
    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    [Key]
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the ownership token.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string OwnershipToken { get; set; }

    /// <summary>
    /// Gets or sets the acquired utc.
    /// </summary>
    [Required]
    public DateTimeOffset AcquiredUtc { get; set; }

    /// <summary>
    /// Gets or sets the renewed utc.
    /// </summary>
    public DateTimeOffset? RenewedUtc { get; set; }

    /// <summary>
    /// Gets or sets the expires utc.
    /// </summary>
    [Required]
    public DateTimeOffset ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the renewal count.
    /// </summary>
    [Required]
    public int RenewalCount { get; set; }

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
