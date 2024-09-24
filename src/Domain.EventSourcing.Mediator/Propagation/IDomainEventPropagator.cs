// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

using Model;

public interface IDomainEventPropagator<TAggregate, TDomainEvent> : IDomainEventPropagatorRoot
    where TAggregate : class, IAggregateRootWithGuid, new()
    where TDomainEvent : IDomainEventWithGuid
{
    TAggregate Propagate(TDomainEvent domainEvent, TAggregate aggregate);
}