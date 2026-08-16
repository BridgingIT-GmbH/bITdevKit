// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Defines operations for i entity create command.
/// </summary>
public interface IEntityCreateCommand
{
    /// <summary>
    /// Gets the entity.
    /// </summary>
    object Entity { get; }
}

/// <summary>
/// Defines operations for i entity create command.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityCreateCommand<TEntity> : IEntityCreateCommand
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets the entity.
    /// </summary>
    new TEntity Entity { get; }
}
