// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

using Model;

/// <summary>
/// Defines operations for i domain event propagation registration.
/// </summary>
public interface IDomainEventPropagationRegistration
{
    /// <summary>
    /// Represents get domain event propagation.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <typeparam name="TDomainEvent">The domain event type.</typeparam>
    /// <param name="domainEvent">The domain event used by the operation.</param>
    IDomainEventPropagator<TAggregate, TDomainEvent> GetDomainEventPropagation<TAggregate, TDomainEvent>(
        TDomainEvent domainEvent)
        where TAggregate : class, IAggregateRootWithGuid, new()
        where TDomainEvent : IDomainEventWithGuid;

    /// <summary>
    /// Executes the register operation.
    /// </summary>
    /// <param name="domainEvent">The domain event used by the operation.</param>
    /// <param name="domainEventPropagation">The domain event propagation used by the operation.</param>
    void Register(IDomainEventWithGuid domainEvent, IDomainEventPropagatorRoot domainEventPropagation);
}
