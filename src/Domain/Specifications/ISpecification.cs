// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Interface for defining specifications that encapsulate business rules or validation logic.
/// </summary>
/// <typeparam name="T">The type of entity that this specification applies to.</typeparam>
public interface ISpecification<T>
{
    /// <summary>
    ///     Converts the specification to an expression that can be used for querying.
    /// </summary>
    /// <returns>
    ///     An expression representing the criteria defined in the specification.
    /// </returns>
    Expression<Func<T, bool>> ToExpression();

    /// <summary>
    ///     Converts the specification to a predicate function.
    /// </summary>
    /// <returns>A predicate function that can be used to evaluate if an entity satisfies the specification.</returns>
    Func<T, bool> ToPredicate();

    /// <summary>
    ///     Determines if the specification is satisfied by a given entity.
    /// </summary>
    /// <param name="entity">The entity to be evaluated against the specification.</param>
    /// <returns>True if the entity satisfies the specification; otherwise, false.</returns>
    bool IsSatisfiedBy(T entity);

    /// <summary>
    ///     Creates a new specification that is the logical OR of two specifications.
    /// </summary>
    /// <param name="specification">The specification to combine with the current specification.</param>
    /// <returns>A new specification that represents the logical OR of the two specifications.</returns>
    ISpecification<T> Or(ISpecification<T> specification);

    /// <summary>
    ///     Combines the current specification with another specification using a logical AND operator.
    /// </summary>
    /// <param name="specification">The other specification to combine with.</param>
    /// <return>
    ///     A new specification that represents the logical AND of the current specification and the specified
    ///     specification.
    /// </return>
    ISpecification<T> And(ISpecification<T> specification);

    /// <summary>
    ///     Creates a specification that negates the current specification.
    /// </summary>
    /// <returns>
    ///     A new specification that represents the negation of the current specification.
    /// </returns>
    ISpecification<T> Not();

    /// <summary>
    /// Converts the specification to a string representation.
    /// </summary>
    /// <returns>
    /// A string representation of the specification's expression.
    /// </returns>
    string ToExpressionString();
}