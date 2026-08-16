// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using Outbox;

/// <summary>
/// Defines operations for i aggregate event outbox receiver.
/// </summary>
public interface IAggregateEventOutboxReceiver
{
    /// <summary>
    /// Executes the receive and publish operation.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<(bool projectionSended, bool eventOccuredSended, bool eventOccuredNotified)> ReceiveAndPublishAsync(
        OutboxMessage message);
}
