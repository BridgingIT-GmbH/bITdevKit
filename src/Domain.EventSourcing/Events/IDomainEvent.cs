// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

/// <summary>
/// Defines operations for i domain event.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public interface IDomainEvent<out TId> : IDomainEvent
{
    /// <summary>
    /// Gets the aggregate id.
    /// </summary>
    TId AggregateId { get; }
}
