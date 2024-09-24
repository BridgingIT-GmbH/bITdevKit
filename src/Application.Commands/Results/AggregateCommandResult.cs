// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using Domain.Model;

public class AggregateCommandResult<TEntity>(TEntity entity)
    where TEntity : class, IAggregateRoot
{
    /// <summary>
    ///     The aggregate id
    /// </summary>
    public TEntity Entity { get; } = entity;
}