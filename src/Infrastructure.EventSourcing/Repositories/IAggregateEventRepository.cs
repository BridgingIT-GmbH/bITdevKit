// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Domain.EventSourcing.Model;
using Domain.Repositories;

/// <summary>
/// Defines operations for i aggregate event repository.
/// </summary>
public interface IAggregateEventRepository : IRepository
{
    /// <summary>
    /// Executes the insert operation.
    /// </summary>
    /// <param name="event">The event used by the operation.</param>
    /// <param name="immutableAggregateTypeName">The immutable aggregate type name used by the operation.</param>
    /// <param name="immutableEventTypeName">The immutable event type name used by the operation.</param>
    /// <param name="data">The data used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task InsertAsync(
        IAggregateEvent @event,
        string immutableAggregateTypeName,
        string immutableEventTypeName,
        byte[] data);

    /// <summary>
    /// Gets events.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="immutableAggregateTypeName">The immutable aggregate type name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<EventStoreAggregateEvent[]> GetEventsAsync(
        Guid aggregateId,
        string immutableAggregateTypeName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Guid[]> GetAggregateIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Guid[]> GetAggregateIdsAsync<TAggregate>(CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="operation">The operation used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ExecuteScopedAsync(Func<Task> operation);

    /// <summary>
    /// Gets max version.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="immutableAggregateName">The immutable aggregate name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<int> GetMaxVersionAsync(Guid aggregateId, string immutableAggregateName, CancellationToken cancellationToken);
}
