// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents one durably accepted event waiting to be materialized into scheduler occurrences.
/// </summary>
public sealed record JobAcceptedEvent
{
    /// <summary>
    /// Gets the accepted-event identifier.
    /// </summary>
    public Guid AcceptedEventId { get; init; }

    /// <summary>
    /// Gets the logical event source name such as notifier, messaging, or queueing.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets the accepted event payload.
    /// </summary>
    public object Data { get; init; }

    /// <summary>
    /// Gets the accepted event payload type.
    /// </summary>
    public Type DataType { get; init; }

    /// <summary>
    /// Gets the stable idempotency key used to deduplicate accepted events.
    /// </summary>
    public string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets the source-system identifier when available.
    /// </summary>
    public string SourceId { get; init; }

    /// <summary>
    /// Gets the propagated correlation identifier when available.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets the accepted event properties.
    /// </summary>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets the UTC instant when the adapter durably accepted the event.
    /// </summary>
    public DateTimeOffset AcceptedUtc { get; init; }
}