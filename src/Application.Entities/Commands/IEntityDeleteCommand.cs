// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Defines operations for i entity delete command.
/// </summary>
public interface IEntityDeleteCommand
{
    /// <summary>
    /// Gets or sets the entity.
    /// </summary>
    object Entity { get; set; }
}

/// <summary>
/// Defines operations for i entity delete command.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityDeleteCommand<TEntity> : IEntityDeleteCommand
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets or sets the entity.
    /// </summary>
    new TEntity Entity { get; set; }
}
