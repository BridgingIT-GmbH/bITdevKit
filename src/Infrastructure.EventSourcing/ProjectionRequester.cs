// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Common;
using Domain.EventSourcing.AggregatePublish;
using Domain.EventSourcing.Model;
using Domain.EventSourcing.Store;
using Microsoft.Extensions.Logging;

/// <summary>
///     Über diese Klasse kann eine (erneute) Projektion für alle Aggregates eines Typs ausgelöst werden
/// </summary>
/// <typeparam name="TAggregate">Aggregateklasse, für welches die Projektion ausgelöst werden soll</typeparam>
public class ProjectionRequester<TAggregate>(
    IEventStore<TAggregate> eventStore,
    IPublishAggregateEventSender sender,
    ILoggerFactory logger) : IProjectionRequester<TAggregate>
    where TAggregate : EventSourcingAggregateRoot
{
    private readonly IEventStore<TAggregate> eventStore = eventStore;
    private readonly IPublishAggregateEventSender publishAggregateEventSender = sender;
    private readonly ILogger logger = logger.CreateLogger<ProjectionRequester<TAggregate>>();

    /// <summary>
    ///     Triggers a projection for all aggregates
    /// </summary>
    public async Task RequestProjectionAsync(CancellationToken cancellationToken)
    {
        var ids = await this.eventStore.GetAggregateIdsAsync(cancellationToken).AnyContext();
        foreach (var id in ids)
        {
            try
            {
                var aggregate = await this.eventStore.GetAsync(id, cancellationToken).AnyContext();
                await this.publishAggregateEventSender.PublishProjectionEventAsync(null, aggregate).AnyContext();
                await this.publishAggregateEventSender.SendProjectionEventAsync(null, aggregate).AnyContext();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.GetFullMessage());
            }
        }
    }

    /// <summary>
    ///     Triggres a projection for the aggregate with the id aggregateId.
    /// </summary>
    public async Task RequestProjectionAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        var aggregate = await this.eventStore.GetAsync(aggregateId, cancellationToken).AnyContext();
        await this.publishAggregateEventSender.PublishProjectionEventAsync(null, aggregate).AnyContext();
        await this.publishAggregateEventSender.SendProjectionEventAsync(null, aggregate).AnyContext();
    }
}