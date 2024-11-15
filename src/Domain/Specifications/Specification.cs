// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Linq.Dynamic.Core;

/// <summary>
/// Represents a specification pattern used to encapsulate domain-specific criteria
/// that can be combined and evaluated against entities of type T.
/// </summary>
/// <typeparam name="T">The type of the entity that this specification is applied to.</typeparam>
public class Specification<T> : ISpecification<T>
{
    private readonly Expression<Func<T, bool>> expression;

    private readonly string dynamicExpression;

    private readonly object[] dynamicExpressionValues;

    /// <summary>
    /// Represents a specification pattern used to define a query criteria for entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of entity to which the specification is applied.</typeparam>
    public Specification(Expression<Func<T, bool>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        this.expression = expression;
    }

    /// <summary>
    /// Represents a specification pattern that is used to check if an entity satisfies certain criteria.
    /// </summary>
    /// <typeparam name="T">The type of entity that this specification will be applied to.</typeparam>
    public Specification(string dynamicExpression, params object[] dynamicExpressionValues)
    {
        ArgumentException.ThrowIfNullOrEmpty(dynamicExpression);

        this.dynamicExpression = dynamicExpression;
        this.dynamicExpressionValues = dynamicExpressionValues;
    }

    /// <summary>
    /// Represents a specification pattern that encapsulates an expression used to filter objects of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of entity this specification applies to.</typeparam>
    protected Specification() { }

    /// <summary>
    /// Converts the specification into an expression that evaluates to a boolean value.
    /// </summary>
    /// <returns>An expression that represents the specification.</returns>
    public virtual Expression<Func<T, bool>> ToExpression()
    {
        if (this.expression != null)
        {
            return this.expression;
        }

        if (this.dynamicExpression != null)
        {
            return DynamicExpressionParser.ParseLambda<T, bool>(null, false, this.dynamicExpression, this.dynamicExpressionValues);
        }

        return default;
    }

    /// <summary>
    /// Converts the specification's expression into a predicate function that can be used to
    /// evaluate if an entity meets the specification criteria.
    /// </summary>
    /// <returns>A function that takes an entity of the specified type and returns a boolean indicating
    /// whether the entity satisfies the specification.</returns>
    public Func<T, bool> ToPredicate()
    {
        return this.ToExpression()?.Compile();
    }

    /// <summary>
    /// Converts the specification to a string representation.
    /// </summary>
    /// <returns>
    /// A string representation of the specification's expression.
    /// </returns>
    public string ToExpressionString()
    {
        return this.ToExpression()?.ToString();
    }

    /// <summary>
    /// Determines whether the specified entity satisfies the specification.
    /// </summary>
    /// <param name="entity">The entity to evaluate.</param>
    /// <returns>true if the entity satisfies the specification; otherwise, false.</returns>
    public bool IsSatisfiedBy(T entity)
    {
        if (entity is null)
        {
            return false;
        }

        var predicate = this.ToPredicate();

        try
        {
            return predicate(entity);
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    /// <summary>
    /// Combines the current specification with another specification using a logical AND operation.
    /// </summary>
    /// <param name="specification">The specification to combine with the current specification.</param>
    /// <returns>A new specification that represents the combined condition of the current specification and the provided specification.</returns>
    public ISpecification<T> And(ISpecification<T> specification)
    {
        //if (this == All)
        //{
        //    return specification;
        //}

        //if (specification == All)
        //{
        //    return this;
        //}

        return new AndSpecification<T>(this, specification);
    }

    /// <summary>
    /// Creates a new specification that is the logical OR of the current specification and the given specification.
    /// </summary>
    /// <param name="specification">The specification to combine with the current specification using a logical OR.</param>
    /// <returns>A new specification that is the logical OR of the current and given specifications.</returns>
    public ISpecification<T> Or(ISpecification<T> specification)
    {
        //if (this == All || specification == All)
        //{
        //    return All;
        //}

        return new OrSpecification<T>(this, specification);
    }

    /// <summary>
    /// Creates a specification that negates the current specification.
    /// </summary>
    /// <returns>An <see cref="ISpecification{T}"/> representing the negation of the current specification.</returns>
    public ISpecification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
}