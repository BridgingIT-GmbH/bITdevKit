// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using System.Linq.Expressions;

/// <summary>
/// Represents entity not soft deleted specification.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class EntityNotSoftDeletedSpecification<TEntity> : Specification<TEntity>
    where TEntity : class, IEntity, ISoftDeletable
{
    /// <inheritdoc/>
    public override Expression<Func<TEntity, bool>> ToExpression()
    {
        return e => e.Deleted == null || !(bool)e.Deleted;
    }
}
