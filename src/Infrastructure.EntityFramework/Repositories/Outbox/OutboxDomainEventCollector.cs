// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Diagnostics;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Outbox;

/// <summary>Projects aggregate domain events to deterministic outbox rows in enumeration order.</summary>
/// <example><code>var projections = new OutboxDomainEventCollector(options).Collect(aggregates);</code></example>
public sealed class OutboxDomainEventCollector
{
    private readonly OutboxDomainEventOptions options;

    /// <summary>Initializes the collector with outbox serialization options.</summary>
    /// <param name="options">The optional outbox options.</param>
    /// <example><code>var collector = new OutboxDomainEventCollector(options);</code></example>
    public OutboxDomainEventCollector(OutboxDomainEventOptions options = null)
    {
        this.options = options ?? new OutboxDomainEventOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    /// <summary>Projects all currently registered domain events without mutating the aggregates.</summary>
    /// <param name="aggregates">The aggregates in deterministic insertion order.</param>
    /// <returns>Ordered event-to-outbox projections.</returns>
    /// <example><code>var projections = collector.Collect(aggregates);</code></example>
    public IReadOnlyList<OutboxDomainEventProjection> Collect(
        IEnumerable<IAggregateRoot> aggregates
    ) =>
        (aggregates ?? [])
            .SelectMany(aggregate =>
                aggregate
                    .DomainEvents.GetAll()
                    .Select(domainEvent => this.CreateProjection(aggregate, domainEvent))
            )
            .ToArray();

    private OutboxDomainEventProjection CreateProjection(
        IAggregateRoot aggregate,
        IDomainEvent domainEvent
    )
    {
        var outboxEvent = new OutboxDomainEvent
        {
            EventId = domainEvent.EventId.ToString(),
            Type = domainEvent.GetType().AssemblyQualifiedNameShort(),
            Content = this.options.Serializer.SerializeToString(domainEvent),
            ContentHash = HashHelper.Compute(domainEvent),
            CreatedDate = domainEvent.Timestamp,
        };
        PropagateContext(outboxEvent);
        return new OutboxDomainEventProjection(aggregate, domainEvent, outboxEvent);
    }

    private static void PropagateContext(OutboxDomainEvent outboxEvent)
    {
        AddActivityValue(
            outboxEvent,
            Constants.CorrelationIdKey,
            Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey)
        );
        AddActivityValue(
            outboxEvent,
            Constants.FlowIdKey,
            Activity.Current?.GetBaggageItem(ActivityConstants.FlowIdTagKey)
        );
        AddActivityValue(
            outboxEvent,
            ModuleConstants.ModuleNameKey,
            Activity.Current?.GetBaggageItem(ModuleConstants.ModuleNameKey)
        );
        AddActivityValue(outboxEvent, ModuleConstants.ActivityParentIdKey, Activity.Current?.Id);
    }

    private static void AddActivityValue(OutboxDomainEvent outboxEvent, string key, string value)
    {
        if (!value.IsNullOrEmpty())
        {
            outboxEvent.Properties.AddOrUpdate(key, value);
        }
    }
}

/// <summary>Associates one source aggregate and domain event with its projected outbox row.</summary>
/// <param name="Aggregate">The source aggregate.</param>
/// <param name="DomainEvent">The source domain event.</param>
/// <param name="OutboxEvent">The projected outbox row.</param>
/// <example><code>var row = projection.OutboxEvent;</code></example>
public sealed record OutboxDomainEventProjection(
    IAggregateRoot Aggregate,
    IDomainEvent DomainEvent,
    OutboxDomainEvent OutboxEvent
);
