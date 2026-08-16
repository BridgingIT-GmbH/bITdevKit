// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Defines operations for i entity command rule.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityCommandRule<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets the message.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// Determines whether is satisfied.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> IsSatisfiedAsync(TEntity entity);
}

/// <summary>
/// Defines operations for i entity create command rule.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityCreateCommandRule<TEntity> : IEntityCommandRule<TEntity>
    where TEntity : class, IEntity;

/// <summary>
/// Defines operations for i entity update command rule.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityUpdateCommandRule<TEntity> : IEntityCommandRule<TEntity>
    where TEntity : class, IEntity;

/// <summary>
/// Defines operations for i entity delete command rule.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityDeleteCommandRule<TEntity> : IEntityCommandRule<TEntity>
    where TEntity : class, IEntity;
