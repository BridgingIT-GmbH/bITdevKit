// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Defines the Entity Framework context contract required by the domain event outbox infrastructure.
/// </summary>
public interface IOutboxDomainEventContext
{
    /// <summary>
    /// Gets or sets the persisted domain event outbox rows.
    /// </summary>
    DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }
}
