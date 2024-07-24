// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;

public class HasIdSpecification<T>(object id) : Specification<T>
    where T : IEntity
{
    protected object Id { get; } = id;

    public override Expression<Func<T, bool>> ToExpression()
    {
        return t => t.Id == this.Id;
    }

    //public static class Factory
    //{
    //    public static HasIdSpecification<T> Create(object id)
    //    {
    //        return new HasIdSpecification<T>(id);
    //    }
    //}
}