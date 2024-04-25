// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using BridgingIT.DevKit.Domain.Model;

public class AggregateCommandResult<TEntity>
    where TEntity : class, IAggregateRoot
{
    public AggregateCommandResult(TEntity entity)
    {
        this.Entity = entity;
    }

    /// <summary>
    /// The aggregate id
    /// </summary>
    public TEntity Entity { get; }
}