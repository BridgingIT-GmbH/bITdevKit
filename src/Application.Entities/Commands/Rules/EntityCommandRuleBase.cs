// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

public abstract class EntityCommandRuleBase<TEntity> : IEntityCommandRule<TEntity>
    where TEntity : class, IEntity
{
    public string Message { get; init; }

    public abstract Task<bool> IsSatisfiedAsync(TEntity entity);
}

public abstract class EntityCreateCommandRuleBase<TEntity>
    : EntityCommandRuleBase<TEntity>, IEntityCreateCommandRule<TEntity>
    where TEntity : class, IEntity
{ }

public abstract class EntityDeleteCommandRuleBase<TEntity>
    : EntityCommandRuleBase<TEntity>, IEntityDeleteCommandRule<TEntity>
    where TEntity : class, IEntity
{ }

public abstract class EntityUpdateCommandRuleBase<TEntity>
    : EntityCommandRuleBase<TEntity>, IEntityUpdateCommandRule<TEntity>
    where TEntity : class, IEntity
{ }