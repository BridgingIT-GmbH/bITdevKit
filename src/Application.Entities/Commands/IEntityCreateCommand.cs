// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Domain.Model;

public interface IEntityCreateCommand
{
    object Entity { get; }
}

public interface IEntityCreateCommand<TEntity> : IEntityCreateCommand
    where TEntity : class, IEntity
{
    new TEntity Entity { get; }
}