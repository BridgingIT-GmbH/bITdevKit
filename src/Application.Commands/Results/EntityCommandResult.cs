// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using BridgingIT.DevKit.Domain.Model;

public class EntityCommandResult<TEntity>(TEntity entity)
    where TEntity : class, IEntity
{
    /// <summary>
    /// The entity id
    /// </summary>
    public TEntity Entity { get; } = entity;
}