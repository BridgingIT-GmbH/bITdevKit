// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Represents the entity inserted domain event domain event.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="entity">The entity involved in the operation.</param>
public class EntityInsertedDomainEvent<TEntity>(TEntity entity) : DomainEventBase
    where TEntity : IEntity, IAggregateRoot
{
    /// <summary>
    /// Initializes a new instance of the <c>EntityInsertedDomainEvent</c> class.
    /// </summary>
    protected EntityInsertedDomainEvent() : this(default)
    { }

    /// <summary>
    /// Gets or sets the entity.
    /// </summary>
    public TEntity Entity { get; protected set; } = entity;
}
