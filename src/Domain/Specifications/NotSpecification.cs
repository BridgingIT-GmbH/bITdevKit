// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System;
using System.Linq;
using System.Linq.Expressions;

public class NotSpecification<T>(ISpecification<T> specification) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = specification.ToExpression();
        var notepression = Expression.Not(expression.Body);

        return Expression.Lambda<Func<T, bool>>(notepression, expression.Parameters.Single());
    }

    public override string ToString()
    {
        return this.ToExpression()?.ToString();
    }
}