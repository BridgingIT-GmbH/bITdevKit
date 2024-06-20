// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System;
using System.Linq;
using System.Linq.Expressions;

public class OrSpecification<T>(
    ISpecification<T> leftSpecification,
    ISpecification<T> rightSpecification)
    : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = leftSpecification.ToExpression();
        var rightExpression = rightSpecification.ToExpression();

        //var orExpression = Expression.OrElse(leftExpression.Body, rightExpression.Body);
        var orExpression = Expression.OrElse(
            leftExpression.Body,
            Expression.Invoke(rightExpression, leftExpression.Parameters.Single()));

        //return Expression.Lambda<Func<T, bool>>(orExpression, leftExpression.Parameters.Single());
        return Expression.Lambda<Func<T, bool>>(orExpression, leftExpression.Parameters);
    }

    public override string ToString()
    {
        return this.ToExpression()?.ToString();
    }
}