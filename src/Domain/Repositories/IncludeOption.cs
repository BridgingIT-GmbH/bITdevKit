// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Supports type-safe chaining of ThenInclude operations for eager loading of related entities.
/// </summary>
/// <typeparam name="TEntity">The root entity type.</typeparam>
/// <typeparam name="TProperty">The type of the previously included navigation property.</typeparam>
public interface IIncludableOption<out TEntity, out TProperty>
    where TEntity : class, IEntity
{
}

/// <summary>
///     Base class for include options without generic property type (for storage in collections).
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public abstract class IncludeOptionBase<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Represents an expression used in an <see cref="IncludeOption{TEntity}" />.
    /// </summary>
    /// <remarks>
    ///     The <c>Expression</c> property defines an Expression{Func{TEntity, object}}
    ///     that specifies how to include related entities in a query. When set, this expression
    ///     will be used to include related entities via a queryable <c>Include</c> operation.
    /// </remarks>
    public Expression<Func<TEntity, object>> Expression { get; protected set; }

    /// <summary>
    ///     Gets the string representation of the include path used for eager loading related data
    ///     in queries against the repository. This property specifies the navigation property name
    ///     to be included in the query.
    /// </summary>
    public string Path { get; protected set; }

    /// <summary>
    ///     Storage for the ThenInclude chain.
    /// </summary>
    public List<ThenIncludeDescriptor> ThenIncludes { get; } = [];
}

/// <summary>
///     Represents an option for including related entities in a query (non-generic property version).
///     Use this for string-based paths or when type-safe ThenInclude is not needed.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public class IncludeOption<TEntity> : IncludeOptionBase<TEntity>, IIncludableOption<TEntity, object>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="IncludeOption{TEntity}" /> class.
    ///     Represents an option for including a related entity in the query.
    /// </summary>
    public IncludeOption(Expression<Func<TEntity, object>> expression)
    {
        EnsureArg.IsNotNull(expression, nameof(expression));

        this.Expression = expression;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="IncludeOption{TEntity}" /> class.
    ///     Represents an option for including related entities in a query.
    /// </summary>
    /// <param name="path">The navigation property path to include in the query.</param>
    public IncludeOption(string path)
    {
        EnsureArg.IsNotNull(path, nameof(path));

        this.Path = path;
    }
}

/// <summary>
///     Represents an option for including related entities in a query with type-safe ThenInclude support.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TProperty">The type of the navigation property being included.</typeparam>
public class IncludeOption<TEntity, TProperty> : IncludeOptionBase<TEntity>, IIncludableOption<TEntity, TProperty>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="IncludeOption{TEntity, TProperty}" /> class.
    ///     Represents an option for including a related entity in the query with type-safe ThenInclude support.
    /// </summary>
    public IncludeOption(Expression<Func<TEntity, TProperty>> expression)
    {
        EnsureArg.IsNotNull(expression, nameof(expression));

        this.TypedExpression = expression;
        // Also store as object expression for compatibility
        this.Expression = ConvertToObjectExpression(expression);
    }

    /// <summary>
    ///     Gets the strongly-typed expression for the navigation property.
    /// </summary>
    public Expression<Func<TEntity, TProperty>> TypedExpression { get; }

    private static Expression<Func<TEntity, object>> ConvertToObjectExpression(Expression<Func<TEntity, TProperty>> expression)
    {
        var parameter = expression.Parameters[0];
        var body = System.Linq.Expressions.Expression.Convert(expression.Body, typeof(object));

        return System.Linq.Expressions.Expression.Lambda<Func<TEntity, object>>(body, parameter);
    }
}

/// <summary>
///     Descriptor for storing ThenInclude expression information.
/// </summary>
public class ThenIncludeDescriptor
{
    public LambdaExpression Expression { get; init; }
    public bool IsCollection { get; init; }
}
