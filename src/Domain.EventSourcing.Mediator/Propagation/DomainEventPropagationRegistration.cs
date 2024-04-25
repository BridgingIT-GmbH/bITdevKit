// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

using System.Collections.Generic;
using BridgingIT.DevKit.Domain.EventSourcing.Model;

public class DomainEventPropagationRegistration : IDomainEventPropagationRegistration
{
    private readonly Dictionary<string, IDomainEventPropagatorRoot> registration = new();

    public void Register(IDomainEventWithGuid domainEvent, IDomainEventPropagatorRoot domainEventPropagation)
    {
        this.registration.Add(domainEvent?.GetType().FullName, domainEventPropagation);
    }

    public IDomainEventPropagator<TAggregate, TDomainEvent> GetDomainEventPropagation<TAggregate, TDomainEvent>(
        TDomainEvent domainEvent)
        where TAggregate : class, IAggregateRootWithGuid, new()
        where TDomainEvent : IDomainEventWithGuid
    {
        this.registration.TryGetValue(domainEvent.GetType().FullName, out var propagater);

        return propagater as IDomainEventPropagator<TAggregate, TDomainEvent>;
    }
}