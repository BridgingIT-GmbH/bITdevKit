// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Interface for handling domain events. Implementations of this interface are responsible
///     for processing specific types of domain events.
/// </summary>
/// <typeparam name="TEvent">The type of event to handle. Must implement <see cref="IDomainEvent" />.</typeparam>
public interface IDomainEventHandler<in TEvent> : IDomainEventHandler
    where TEvent : class, IDomainEvent
{
    /// <summary>
    ///     Determines whether this instance can handle the specified notification.
    /// </summary>
    /// <param name="event">The notification.</param>
    /// <returns>
    ///     <c>true</c> if this instance can handle the specified notification; otherwise, <c>false</c>.
    /// </returns>
    bool CanHandle(TEvent @event);
}

/// <summary>
///     Marker interface for domain event handlers. Implementations of this interface are designed to handle
///     domain events to encapsulate the handling logic for specific event types.
/// </summary>
public interface IDomainEventHandler;