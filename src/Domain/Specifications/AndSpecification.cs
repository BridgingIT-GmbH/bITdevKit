// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System.Linq.Expressions;

/// <summary>
///     Represents a composite specification that combines two specifications using the logical AND operator.
/// </summary>
/// <typeparam name="T">The type of entity that this specification applies to.</typeparam>
public class AndSpecification<T>(ISpecification<T> left, ISpecification<T> right) : Specification<T>
{
    /// <summary>
    ///     Creates and returns an expression that evaluates to the logical AND
    ///     of two other specifications represented as expressions.
    /// </summary>
    /// <returns>
    ///     An Expression that represents the logical AND
    ///     of the expressions of the component specifications.
    /// </returns>
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = left.ToExpression();
        var rightExpression = right.ToExpression();

        //var andExpression = Expression.AndAlso(leftExpression.Body, rightExpression.Body);
        var andExpression = Expression.AndAlso(leftExpression.Body,
            Expression.Invoke(rightExpression, leftExpression.Parameters.Single()));

        //return Expression.Lambda<Func<T, bool>>(andExpression, leftExpression.Parameters.Single());
        return Expression.Lambda<Func<T, bool>>(andExpression, leftExpression.Parameters);
    }

    /// <summary>
    ///     Generates a string representation of the AndSpecification.
    /// </summary>
    /// <returns>
    ///     A string representing the combined expression of the left and right specifications.
    /// </returns>
    public override string ToString()
    {
        return this.ToExpression()?.ToString();
    }
}