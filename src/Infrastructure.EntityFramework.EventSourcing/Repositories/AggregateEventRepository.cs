// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using Common;
using Domain.EventSourcing.Model;
using Domain.EventSourcing.Registration;
using Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Infrastructure.EventSourcing;
using Models;
using Repositories;

/// <summary>
/// Represents aggregate event repository.
/// </summary>
/// <param name="aggregateRegistration">The aggregate registration used by the operation.</param>
/// <param name="options">The options controlling the operation.</param>
public class AggregateEventRepository(
    IEventStoreAggregateRegistration aggregateRegistration,
    EntityFrameworkRepositoryOptions options) : EntityFrameworkGenericRepository<EventStoreAggregateEvent>(options),
    IAggregateEventRepository
{
    private readonly IEventStoreAggregateRegistration aggregateRegistration = aggregateRegistration;
    private readonly EventStoreDbContext context = options.DbContext as EventStoreDbContext;

    /// <summary>
    /// Initializes a new instance of the <c>AggregateEventRepository</c> class.
    /// </summary>
    /// <param name="aggregateRegistration">The aggregate registration used by the operation.</param>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    public AggregateEventRepository(
        IEventStoreAggregateRegistration aggregateRegistration,
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(aggregateRegistration, optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    /// <summary>
    /// Executes the insert operation.
    /// </summary>
    /// <param name="aggregateEvent">The aggregate event used by the operation.</param>
    /// <param name="immutableAggregateTypeName">The immutable aggregate type name used by the operation.</param>
    /// <param name="immutableEventTypeName">The immutable event type name used by the operation.</param>
    /// <param name="data">The data used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InsertAsync(
        IAggregateEvent aggregateEvent,
        string immutableAggregateTypeName,
        string immutableEventTypeName,
        byte[] data)
    {
        var dto = new EventStoreAggregateEvent
        {
            Id = Guid.NewGuid(), // TODO: use GuidGenerator.CreateSequential() here
            AggregateId = aggregateEvent.AggregateId,
            AggregateVersion = aggregateEvent.AggregateVersion,
            Identifier = aggregateEvent.EventId,
            AggregateType = immutableAggregateTypeName,
            Data = data,
            EventType = immutableEventTypeName,
            TimeStamp = DateTime.Now.ToUniversalTime()
        };
        await base.InsertAsync(dto).AnyContext();
    }

    /// <summary>
    /// Gets events.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="immutableAggregateTypeName">The immutable aggregate type name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<EventStoreAggregateEvent[]> GetEventsAsync(
        Guid aggregateId,
        string immutableAggregateTypeName,
        CancellationToken cancellationToken)
    {
        var options = new FindOptions<EventStoreAggregateEvent>
        {
            Order = new OrderOption<EventStoreAggregateEvent>(oo => oo.AggregateVersion),
            NoTracking = true
        };
        var spec = new Specification<EventStoreAggregateEvent>(s =>
            s.AggregateId == aggregateId && s.AggregateType == immutableAggregateTypeName);
        var result = await this.FindAllAsync(spec, options, cancellationToken).AnyContext();

        return result.ToArray();
    }

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Guid[]> GetAggregateIdsAsync(CancellationToken cancellationToken)
    {
        var list = await this.FindAllAsync(null, cancellationToken).AnyContext();

        return list.Select(s => s.AggregateId).Distinct().ToArray();
    }

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Guid[]> GetAggregateIdsAsync<TAggregate>(CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        var aggregateType = this.aggregateRegistration.GetImmutableName<TAggregate>();
        var spec = new Specification<EventStoreAggregateEvent>(ev => ev.AggregateType == aggregateType);
        var list = await this.FindAllAsync(spec, null, cancellationToken).AnyContext();

        return list.Select(s => s.AggregateId).Distinct().ToArray();
    }

    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="operation">The operation used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteScopedAsync(Func<Task> operation)
    {
        //tag::ExecuteScopedAsync[]
        await ResilientTransaction.Create(this.Options.DbContext)
            .ExecuteAsync(async () =>
            {
                await operation().AnyContext(); // <1>
            })
            .AnyContext();
        //end::ExecuteScopedAsync[]
    }

    /// <summary>
    /// Gets max version.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="immutableAggregateName">The immutable aggregate name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<int> GetMaxVersionAsync(
        Guid aggregateId,
        string immutableAggregateName,
        CancellationToken cancellationToken)
    {
        var options = new FindOptions<EventStoreAggregateEvent>
        {
            Take = 1,
            Order = new OrderOption<EventStoreAggregateEvent>(oo => oo.AggregateVersion, OrderDirection.Descending),
            NoTracking = true
        };
        var spec = new Specification<EventStoreAggregateEvent>(s =>
            s.AggregateId == aggregateId && s.AggregateType == immutableAggregateName);
        var result = (await this.FindAllAsync(spec, options, cancellationToken).AnyContext()).ToArray();
        if (!result.Any())
        {
            return 0;
        }

        return result.First().AggregateVersion;
    }
}
