// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

using Model;

/// <summary>
/// Defines operations for i domain event propagation center.
/// </summary>
public interface IDomainEventPropagationCenter
{
    /// <summary>
    /// Executes the apply domain event operation.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="domainEvent">The domain event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    void ApplyDomainEvent<TAggregate>(DomainEventWithGuid domainEvent, TAggregate aggregate)
        where TAggregate : IAggregateRootWithGuid, new();
}
