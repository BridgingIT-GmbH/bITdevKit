// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using MediatR;

public interface IDomainEventHandler<in TEvent> :
    INotificationHandler<TEvent>,
    IDomainEventHandler
    where TEvent : class, IDomainEvent
{
    /// <summary>
    /// Determines whether this instance can handle the specified notification.
    /// </summary>
    /// <param name="event">The notification.</param>
    /// <returns>
    ///   <c>true</c> if this instance can handle the specified notification; otherwise, <c>false</c>.
    /// </returns>
    bool CanHandle(TEvent @event);
}

public interface IDomainEventHandler
{
}