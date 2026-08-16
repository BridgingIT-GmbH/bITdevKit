// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Represents entity command rule base.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public abstract class EntityCommandRuleBase<TEntity> : IEntityCommandRule<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Determines whether is satisfied.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task<bool> IsSatisfiedAsync(TEntity entity);
}

/// <summary>
/// Represents entity create command rule base.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public abstract class EntityCreateCommandRuleBase<TEntity>
    : EntityCommandRuleBase<TEntity>, IEntityCreateCommandRule<TEntity>
    where TEntity : class, IEntity;

/// <summary>
/// Represents entity delete command rule base.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public abstract class EntityDeleteCommandRuleBase<TEntity>
    : EntityCommandRuleBase<TEntity>, IEntityDeleteCommandRule<TEntity>
    where TEntity : class, IEntity;

/// <summary>
/// Represents entity update command rule base.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public abstract class EntityUpdateCommandRuleBase<TEntity>
    : EntityCommandRuleBase<TEntity>, IEntityUpdateCommandRule<TEntity>
    where TEntity : class, IEntity;
