// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using System.Linq.Expressions;

public class EntityNotDeletedSpecification<TEntity> : Specification<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    public override Expression<Func<TEntity, bool>> ToExpression()
    {
        return e => e.AuditState.Deleted == null || !(bool)e.AuditState.Deleted;
    }
}