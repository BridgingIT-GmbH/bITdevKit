// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Domain.Model;
using System.Linq.Expressions;

public class EntityNotSoftDeletedSpecification<TEntity> : Specification<TEntity>
    where TEntity : class, IEntity, ISoftDeletable
{
    public override Expression<Func<TEntity, bool>> ToExpression()
    {
        return e => e.Deleted == null || !(bool)e.Deleted;
    }
}