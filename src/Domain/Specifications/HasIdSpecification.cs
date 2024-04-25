// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;
using EnsureThat;

public class HasIdSpecification<T> : Specification<T>
    where T : IEntity
{
    public HasIdSpecification(object id)
    {
        EnsureArg.IsNotNull(id);

        this.Id = id;
    }

    protected object Id { get; }

    public override Expression<Func<T, bool>> ToExpression()
    {
        return t => t.Id == this.Id;
    }

    public static class Factory
    {
        public static HasIdSpecification<T> Create(object id)
        {
            return new HasIdSpecification<T>(id);
        }
    }
}