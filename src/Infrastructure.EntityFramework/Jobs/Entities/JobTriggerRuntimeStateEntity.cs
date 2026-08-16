// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents the durable trigger runtime-state row required for deterministic materialization.
/// </summary>
[Table("__Jobs_TriggerRuntimeStates")]
[PrimaryKey(nameof(JobName), nameof(TriggerName))]
public class JobTriggerRuntimeStateEntity
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [MaxLength(256)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    [MaxLength(256)]
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the activated utc.
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the due utc.
    /// </summary>
    public DateTimeOffset? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the last materialized scheduled utc.
    /// </summary>
    public DateTimeOffset? LastMaterializedScheduledUtc { get; set; }

    /// <summary>
    /// Gets or sets the has materialized occurrence.
    /// </summary>
    [Required]
    public bool HasMaterializedOccurrence { get; set; }

    /// <summary>
    /// Gets or sets the enabled.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Gets or sets the paused.
    /// </summary>
    [Required]
    public bool Paused { get; set; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    public DateTimeOffset? CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    public DateTimeOffset? UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the last accepted event utc.
    /// </summary>
    public DateTimeOffset? LastAcceptedEventUtc { get; set; }

    /// <summary>
    /// Gets or sets the last accepted event id.
    /// </summary>
    public Guid? LastAcceptedEventId { get; set; }

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
