// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System.Linq.Expressions;

/// <summary>
///     Represents a specification that combines two specifications using a logical OR operation.
/// </summary>
/// <typeparam name="T">The type of entity that this specification applies to.</typeparam>
public class OrSpecification<T>(ISpecification<T> left, ISpecification<T> right) : Specification<T>
{
    /// <summary>
    ///     Combines the left and right specifications using a logical OR operation.
    /// </summary>
    /// <returns>An expression that represents the combined specification.</returns>
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = left.ToExpression();
        var rightExpression = right.ToExpression();

        //var orExpression = Expression.OrElse(leftExpression.Body, rightExpression.Body);
        var orExpression = Expression.OrElse(leftExpression.Body,
            Expression.Invoke(rightExpression, leftExpression.Parameters.Single()));

        //return Expression.Lambda<Func<T, bool>>(orExpression, leftExpression.Parameters.Single());
        return Expression.Lambda<Func<T, bool>>(orExpression, leftExpression.Parameters);
    }

    /// <summary>
    ///     Returns a string representation of the specification by converting the combined expression to a string.
    /// </summary>
    /// <returns>A string that represents the combined specification expression.</returns>
    public override string ToString()
    {
        return this.ToExpression()?.ToString();
    }
}