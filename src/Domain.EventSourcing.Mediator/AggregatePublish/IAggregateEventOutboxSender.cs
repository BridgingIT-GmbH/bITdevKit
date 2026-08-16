// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using Model;

/// <summary>
/// Defines operations for i aggregate event outbox sender.
/// </summary>
public interface IAggregateEventOutboxSender
{
    /// <summary>
    /// Executes the write to outbox operation.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task WriteToOutboxAsync<TAggregate>(AggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;
}
