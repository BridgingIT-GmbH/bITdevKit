// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

using Model;

/// <summary>
/// Represents domain event propagation registration.
/// </summary>
public class DomainEventPropagationRegistration : IDomainEventPropagationRegistration
{
    private readonly Dictionary<string, IDomainEventPropagatorRoot> registration = [];

    /// <summary>
    /// Executes the register operation.
    /// </summary>
    /// <param name="domainEvent">The domain event used by the operation.</param>
    /// <param name="domainEventPropagation">The domain event propagation used by the operation.</param>
    public void Register(IDomainEventWithGuid domainEvent, IDomainEventPropagatorRoot domainEventPropagation)
    {
        this.registration.Add(domainEvent?.GetType().FullName, domainEventPropagation);
    }

    /// <summary>
    /// Represents get domain event propagation.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <typeparam name="TDomainEvent">The domain event type.</typeparam>
    /// <param name="domainEvent">The domain event used by the operation.</param>
    public IDomainEventPropagator<TAggregate, TDomainEvent> GetDomainEventPropagation<TAggregate, TDomainEvent>(
        TDomainEvent domainEvent)
        where TAggregate : class, IAggregateRootWithGuid, new()
        where TDomainEvent : IDomainEventWithGuid
    {
        this.registration.TryGetValue(domainEvent.GetType().FullName, out var propagater);

        return propagater as IDomainEventPropagator<TAggregate, TDomainEvent>;
    }
}
