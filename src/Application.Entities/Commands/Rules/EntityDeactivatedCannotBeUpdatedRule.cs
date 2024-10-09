// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Humanizer;

public class EntityDeactivatedCannotBeUpdatedRule<TEntity> : EntityUpdateCommandRuleBase<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    public EntityDeactivatedCannotBeUpdatedRule()
    {
        this.Message = $"{typeof(TEntity).Name.Pluralize()} which are deactivated cannot be updated";
    }

    public override Task<bool> IsSatisfiedAsync(TEntity entity)
    {
        if (entity.AuditState?.IsDeactivated() == true)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}