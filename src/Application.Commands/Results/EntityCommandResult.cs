// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using Domain.Model;

/// <summary>
/// Represents entity command result.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="entity">The entity involved in the operation.</param>
public class EntityCommandResult<TEntity>(TEntity entity)
    where TEntity : class, IEntity
{
    /// <summary>
    ///     The entity id
    /// </summary>
    public TEntity Entity { get; } = entity;
}
