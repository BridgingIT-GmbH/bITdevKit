// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System.Linq.Expressions;

/// <summary>
///     Represents a specification that negates the result of another specification.
/// </summary>
/// <typeparam name="T">The type of the entity that this specification applies to.</typeparam>
public class NotSpecification<T>(ISpecification<T> specification) : Specification<T>
{
    /// <summary>
    ///     Creates an inverted boolean expression from the specified ISpecification by applying the NOT operator.
    /// </summary>
    /// <returns>
    ///     An Expression that represents the logical NOT of the original specification.
    /// </returns>
    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = specification.ToExpression();
        var notepression = Expression.Not(expression.Body);

        return Expression.Lambda<Func<T, bool>>(notepression, expression.Parameters.Single());
    }

    /// <summary>
    ///     Converts the specification to a string representation.
    /// </summary>
    /// <returns>
    ///     A string representation of the specification's expression.
    /// </returns>
    public override string ToString()
    {
        return this.ToExpression()?.ToString();
    }
}