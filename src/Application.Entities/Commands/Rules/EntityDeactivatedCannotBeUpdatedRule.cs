// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Humanizer;

/// <summary>
/// Represents entity deactivated cannot be updated rule.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class EntityDeactivatedCannotBeUpdatedRule<TEntity> : EntityUpdateCommandRuleBase<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    /// <summary>
    /// Initializes a new instance of the <c>EntityDeactivatedCannotBeUpdatedRule</c> class.
    /// </summary>
    public EntityDeactivatedCannotBeUpdatedRule()
    {
        this.Message = $"{typeof(TEntity).Name.Pluralize()} which are deactivated cannot be updated";
    }

    /// <inheritdoc/>
    public override Task<bool> IsSatisfiedAsync(TEntity entity)
    {
        if (entity.AuditState?.IsDeactivated() == true)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
