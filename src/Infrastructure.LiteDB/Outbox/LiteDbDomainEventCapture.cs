// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Diagnostics;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;

/// <summary>Captures pending aggregate and ActiveEntity events within their originating native transaction.</summary>
/// <param name="options">The selected outbox configuration.</param>
/// <param name="worker">The worker to signal after an immediate-mode commit.</param>
public sealed class LiteDbDomainEventCapture(LiteDbDomainEventOutboxOptions options, LiteDbDomainEventOutboxWorker worker) : ILiteDbEntityMutationObserver
{
    /// <inheritdoc />
    public void Capture(LiteDbDatabaseSession session, LiteDatabase database, LiteDbEntityMutation mutation)
    {
        if (!options.Enabled || mutation.Source == "BulkInsert" && !session.CaptureBulkDomainEvents) { return; }

        var aggregate = mutation.Entity as IAggregateRoot;
        var type = mutation.Entity.GetType();
        var get = aggregate is null && LiteDbActiveEntityMapping.IsActiveEntity(type) ? type.GetMethod("GetDomainEvents", Type.EmptyTypes) : null;
        var events = aggregate?.DomainEvents.GetAll().ToArray()
            ?? (get?.Invoke(mutation.Entity, null) as IEnumerable<IDomainEvent>)?.ToArray() ?? [];
        if (events.Length == 0) { return; }

        var collection = database.GetCollection<LiteDbOutboxDomainEvent>(options.CollectionName);
        collection.EnsureIndex(x => x.CreatedTicks);
        collection.EnsureIndex(x => x.LeaseUntilTicks);
        foreach (var domainEvent in events)
        {
            var content = options.Serializer.SerializeToString(domainEvent);
            var existing = collection.FindById(domainEvent.EventId);
            if (existing is not null)
            {
                if (existing.ContentHash != HashHelper.Compute(content))
                {
                    throw new LiteDbProviderException(new ConflictError("An outbox event identity already has different content."));
                }

                continue;
            }

            var properties = domainEvent is DomainEventBase baseEvent ? new Dictionary<string, object>(baseEvent.Properties) : [];
            foreach (var pair in Activity.Current?.Baggage ?? []) { properties[pair.Key] = pair.Value; }

            if (Activity.Current?.Id is { } parentId) { properties[ModuleConstants.ActivityParentIdKey] = parentId; }

            if (Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey) is { } correlationId) { properties["CorrelationId"] = correlationId; }

            if (Activity.Current?.GetBaggageItem(ActivityConstants.FlowIdTagKey) is { } flowId) { properties["FlowId"] = flowId; }

            collection.Insert(new LiteDbOutboxDomainEvent
            {
                Id = domainEvent.EventId, Type = domainEvent.GetType().AssemblyQualifiedNameShort(), Content = content,
                ContentHash = HashHelper.Compute(content), CreatedTicks = domainEvent.Timestamp.UtcTicks, Properties = JsonSerializer.Serialize(properties)
            });
        }

        session.OnCommitted(() =>
        {
            // Only acknowledge events represented by this successful commit; preserve later registrations.
            var captured = events.Select(e => e.EventId).ToHashSet();
            if (aggregate is not null)
            {
                var remaining = aggregate.DomainEvents.GetAll().Where(e => !captured.Contains(e.EventId)).ToArray();
                aggregate.DomainEvents.Clear();
                aggregate.DomainEvents.Register(remaining);
            }
            else
            {
                var remaining = ((IEnumerable<IDomainEvent>)get!.Invoke(mutation.Entity, null)!).Where(e => !captured.Contains(e.EventId)).ToArray();
                type.GetMethod("ClearDomainEvents", Type.EmptyTypes)!.Invoke(mutation.Entity, null);
                var register = type.GetMethod("RegisterDomainEvent", [typeof(IDomainEvent), typeof(bool)])!;
                foreach (var item in remaining) { register.Invoke(mutation.Entity, [item, false]); }
            }

            if (options.ProcessingMode == BridgingIT.DevKit.Domain.Outbox.OutboxDomainEventProcessMode.Immediate) { worker.Signal(); }
        });
    }
}
