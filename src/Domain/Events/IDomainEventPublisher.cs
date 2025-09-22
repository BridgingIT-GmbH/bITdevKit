// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;
/// <summary>
///     Represents a domain event publisher capable of sending domain events.
/// </summary>
public interface IDomainEventPublisher // TODO: another implementation for the new Notifier is needed
{
    /// <summary>
    ///     Sends a domain event using the domain event publisher.
    /// </summary>
    /// <param name="event">The domain event to be sent.</param>
    /// <param name="cancellationToken">Optional. A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IResult> Send(IDomainEvent @event, CancellationToken cancellationToken = default); // TODO: rename to Publish?
}