// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using BridgingIT.DevKit.Domain.Model;
using Humanizer;

public class EntityDeactivatedCannotBeDeletedRule<TEntity> : EntityDeleteCommandRuleBase<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    public EntityDeactivatedCannotBeDeletedRule()
    {
        this.Message = $"{typeof(TEntity).Name.Pluralize()} which are deactivated cannot be deleted";
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
