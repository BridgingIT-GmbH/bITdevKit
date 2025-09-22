// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Represents an option for including related entities in a query.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public class IncludeOption<TEntity>
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

    /// <summary>
    ///     Represents an expression used in an <see cref="IncludeOption{TEntity}" />.
    /// </summary>
    /// <remarks>
    ///     The <c>Expression</c> property defines an Expression{Func{TEntity, object}}
    ///     that specifies how to include related entities in a query. When set, this expression
    ///     will be used to include related entities via a queryable <c>Include</c> operation.
    /// </remarks>
    public Expression<Func<TEntity, object>> Expression { get; }

    /// <summary>
    ///     Gets the string representation of the include path used for eager loading related data
    ///     in queries against the repository. This property specifies the navigation property name
    ///     to be included in the query.
    /// </summary>
    public string Path { get; }
}
