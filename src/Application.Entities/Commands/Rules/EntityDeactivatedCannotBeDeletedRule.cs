// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Humanizer;

/// <summary>
/// Represents entity deactivated cannot be deleted rule.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class EntityDeactivatedCannotBeDeletedRule<TEntity> : EntityDeleteCommandRuleBase<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    /// <summary>
    /// Initializes a new instance of the <c>EntityDeactivatedCannotBeDeletedRule</c> class.
    /// </summary>
    public EntityDeactivatedCannotBeDeletedRule()
    {
        this.Message = $"{typeof(TEntity).Name.Pluralize()} which are deactivated cannot be deleted";
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
