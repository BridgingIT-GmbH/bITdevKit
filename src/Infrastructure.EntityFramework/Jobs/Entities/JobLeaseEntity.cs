// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_Leases")]
[Index(nameof(ExpiresUtc))]
[Index(nameof(SchedulerInstanceId), nameof(ExpiresUtc))]
/// <summary>
/// Represents a durable occurrence-lease row.
/// </summary>
public class JobLeaseEntity
{
    [Key]
    public Guid OccurrenceId { get; set; }

    [Required]
    [MaxLength(256)]
    public string SchedulerInstanceId { get; set; }

    [Required]
    [MaxLength(128)]
    public string OwnershipToken { get; set; }

    [Required]
    public DateTimeOffset AcquiredUtc { get; set; }

    public DateTimeOffset? RenewedUtc { get; set; }

    [Required]
    public DateTimeOffset ExpiresUtc { get; set; }

    [Required]
    public int RenewalCount { get; set; }

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
