// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Humanizer;

/// <summary>
/// Represents entity deleted cannot be deleted again rule.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class EntityDeletedCannotBeDeletedAgainRule<TEntity> : EntityDeleteCommandRuleBase<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    /// <summary>
    /// Initializes a new instance of the <c>EntityDeletedCannotBeDeletedAgainRule</c> class.
    /// </summary>
    public EntityDeletedCannotBeDeletedAgainRule()
    {
        this.Message = $"{typeof(TEntity).Name.Pluralize()} which are deleted cannot be deleted again";
    }

    /// <inheritdoc/>
    public override Task<bool> IsSatisfiedAsync(TEntity entity)
    {
        if (entity.AuditState?.Deleted == true)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
