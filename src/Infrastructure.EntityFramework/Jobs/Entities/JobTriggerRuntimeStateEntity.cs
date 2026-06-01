// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("__Jobs_TriggerRuntimeStates")]
[PrimaryKey(nameof(JobName), nameof(TriggerName))]
/// <summary>
/// Represents the durable trigger runtime-state row required for deterministic materialization.
/// </summary>
public class JobTriggerRuntimeStateEntity
{
    [MaxLength(256)]
    public string JobName { get; set; }

    [MaxLength(256)]
    public string TriggerName { get; set; }

    public DateTimeOffset? ActivatedUtc { get; set; }

    public DateTimeOffset? DueUtc { get; set; }

    public DateTimeOffset? LastMaterializedScheduledUtc { get; set; }

    [Required]
    public bool HasMaterializedOccurrence { get; set; }

    public bool? Enabled { get; set; }

    [Required]
    public bool Paused { get; set; }

    public DateTimeOffset? CreatedDate { get; set; }

    public DateTimeOffset? UpdatedDate { get; set; }

    public DateTimeOffset? LastAcceptedEventUtc { get; set; }

    public Guid? LastAcceptedEventId { get; set; }

    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}
