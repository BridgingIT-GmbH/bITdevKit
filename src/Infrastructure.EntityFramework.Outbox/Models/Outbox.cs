// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Outbox.Models;

/// <summary>
/// Represents outbox.
/// </summary>
public class Outbox
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the aggregate id.
    /// </summary>
    public Guid AggregateId { get; set; }

    /// <summary>
    /// Gets or sets the aggregate type.
    /// </summary>
    public string AggregateType { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the aggregate.
    /// </summary>
    public string Aggregate { get; set; }

    /// <summary>
    /// Gets or sets the aggregate event.
    /// </summary>
    public string AggregateEvent { get; set; }

    /// <summary>
    /// Gets or sets the time stamp.
    /// </summary>
    public DateTime TimeStamp { get; set; }

    /// <summary>
    /// Gets or sets the is processed.
    /// </summary>
    public bool IsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the retry attempt.
    /// </summary>
    public int RetryAttempt { get; set; }
}
