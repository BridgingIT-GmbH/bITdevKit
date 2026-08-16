// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Common;
using Domain.Model;
using Messaging;

/// <summary>
/// Represents entity created message.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="entity">The entity involved in the operation.</param>
public class EntityCreatedMessage<TEntity>(TEntity entity) : MessageBase
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets the entity.
    /// </summary>
    public TEntity Entity { get; } = entity;

    /// <summary>
    /// Gets the entity id.
    /// </summary>
    public string EntityId { get; } = entity?.Id?.ToString();

    /// <summary>
    /// Gets the entity type.
    /// </summary>
    public string EntityType { get; } = entity?.GetType().PrettyName();
}
