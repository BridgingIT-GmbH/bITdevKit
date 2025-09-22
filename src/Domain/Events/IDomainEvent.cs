// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Represents a domain event which is used to signal changes within the domain.
///     Domain events are used to encapsulate changes to the state of an aggregate.
/// </summary>
public interface IDomainEvent : MediatR.INotification, INotification // TODO: double interface for now to support old (mediator) and new (Notifier) domain events
{
    /// <summary>
    ///     Gets the unique identifier for the domain event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    ///     Gets the timestamp indicating when the domain event occurred.
    /// </summary>
    /// <value>
    ///     A <see cref="DateTimeOffset" /> representing the exact date and time the event was generated.
    /// </value>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    ///     Hold extra properties for this event.
    /// </summary>
    IDictionary<string, object> Properties { get; }
}