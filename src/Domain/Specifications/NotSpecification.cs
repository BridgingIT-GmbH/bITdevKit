// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System;
using System.Linq;
using System.Linq.Expressions;
using EnsureThat;

public class NotSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> specification;

    public NotSpecification(ISpecification<T> specification)
    {
        EnsureArg.IsNotNull(specification);

        this.specification = specification;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = this.specification.ToExpression();

        var notepression = Expression.Not(expression.Body);
        return Expression.Lambda<Func<T, bool>>(notepression, expression.Parameters.Single());
    }

    public override string ToString()
    {
        return this.ToExpression()?.ToString();
    }
}