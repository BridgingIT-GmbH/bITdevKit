// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

using Model;

/// <summary>
/// Defines operations for i domain event propagator.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type.</typeparam>
/// <typeparam name="TDomainEvent">The domain event type.</typeparam>
public interface IDomainEventPropagator<TAggregate, TDomainEvent> : IDomainEventPropagatorRoot
    where TAggregate : class, IAggregateRootWithGuid, new()
    where TDomainEvent : IDomainEventWithGuid
{
    /// <summary>
    /// Executes the propagate operation.
    /// </summary>
    /// <param name="domainEvent">The domain event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    TAggregate Propagate(TDomainEvent domainEvent, TAggregate aggregate);
}
