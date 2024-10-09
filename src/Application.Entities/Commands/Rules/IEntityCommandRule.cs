// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

public interface IEntityCommandRule<TEntity>
    where TEntity : class, IEntity
{
    string Message { get; }

    Task<bool> IsSatisfiedAsync(TEntity entity);
}

public interface IEntityCreateCommandRule<TEntity> : IEntityCommandRule<TEntity>
    where TEntity : class, IEntity { }

public interface IEntityUpdateCommandRule<TEntity> : IEntityCommandRule<TEntity>
    where TEntity : class, IEntity { }

public interface IEntityDeleteCommandRule<TEntity> : IEntityCommandRule<TEntity>
    where TEntity : class, IEntity { }