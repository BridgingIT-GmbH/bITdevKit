// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

using Model;

/// <summary>
/// Defines operations for i event store.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type.</typeparam>
public interface IEventStore<TAggregate>
    where TAggregate : EventSourcingAggregateRoot
{
    /// <summary>
    /// Saves events.
    /// </summary>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveEventsAsync(TAggregate aggregate, CancellationToken cancellationToken);

    /// <summary>
    /// Saves events.
    /// </summary>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <param name="sendProjectionRequestForEveryEvent">The send projection request for every event used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveEventsAsync(
        TAggregate aggregate,
        bool sendProjectionRequestForEveryEvent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets events.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IAggregateEvent[]> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets events.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IAggregateEvent[]> GetEventsAsync(Guid aggregateId);

    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<TAggregate> GetAsync(Guid aggregateId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="forceReplay">The force replay used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<TAggregate> GetAsync(Guid aggregateId, bool forceReplay, CancellationToken cancellationToken);

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IEnumerable<Guid>> GetAggregateIdsAsync(CancellationToken cancellationToken);
}
