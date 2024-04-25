// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

using BridgingIT.DevKit.Domain.EventSourcing.Model;

public interface IDomainEventPropagationRegistration
{
    IDomainEventPropagator<TAggregate, TDomainEvent> GetDomainEventPropagation<TAggregate, TDomainEvent>(
        TDomainEvent domainEvent)
        where TAggregate : class, IAggregateRootWithGuid, new()
        where TDomainEvent : IDomainEventWithGuid;

    void Register(IDomainEventWithGuid domainEvent, IDomainEventPropagatorRoot domainEventPropagation);
}