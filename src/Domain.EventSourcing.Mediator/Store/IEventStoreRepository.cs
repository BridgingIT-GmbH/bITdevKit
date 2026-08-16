// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

#nullable enable
namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

using Model;

/// <summary>
/// Defines operations for i event store repository.
/// </summary>
public interface IEventStoreRepository
{
    /// <summary>
    /// Adds .
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="event">The event used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AddAsync<TAggregate>(IAggregateEvent @event, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    /// Gets events.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IAggregateEvent[]> GetEventsAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

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
    /// <param name="none">The none used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Guid[]> GetAggregateIdsAsync<TAggregate>(CancellationToken none)
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
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="eventAggregateId">The event aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<int> GetMaxVersionAsync<TAggregate>(Guid eventAggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    /// Gets snapshot.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<TAggregate?> GetSnapshotAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    /// Saves snapshot.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveSnapshotAsync<TAggregate>(TAggregate aggregate, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;
}
