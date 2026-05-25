// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("__Orchestration_History")]
[Index(nameof(InstanceId), nameof(RecordedAt))]
/// <summary>
/// Represents an append-only orchestration history row stored in Entity Framework persistence.
/// </summary>
public class OrchestrationHistory
{
    /// <summary>
    /// Gets or sets the history entry identifier.
    /// </summary>
    [Key]
    public Guid EntryId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    [Required]
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the state name associated with the entry.
    /// </summary>
    [MaxLength(256)]
    public string StateName { get; set; }

    /// <summary>
    /// Gets or sets the activity name associated with the entry.
    /// </summary>
    [MaxLength(256)]
    public string ActivityName { get; set; }

    /// <summary>
    /// Gets or sets additional event details.
    /// </summary>
    [MaxLength(4000)]
    public string Details { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the event was recorded.
    /// </summary>
    [Required]
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>
    /// Gets or sets the actor that recorded the event when available.
    /// </summary>
    [MaxLength(256)]
    public string RecordedBy { get; set; }
}