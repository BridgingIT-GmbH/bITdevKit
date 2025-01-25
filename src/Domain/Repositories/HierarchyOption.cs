// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Represents an option for including child entities in a query.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public class HierarchyOption<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="IncludeOption{TEntity}" /> class.
    ///     Represents an option for including child entities in the query.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public HierarchyOption(Expression<Func<TEntity, object>> expression, int maxDepth = 5)
    {
        EnsureArg.IsNotNull(expression, nameof(expression));

        this.Expression = expression;
        this.MaxDepth = maxDepth;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="IncludeOption{TEntity}" /> class.
    ///     Represents an option for including child entities in the query.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="path">The navigation property path to hierarchy in the query.</param>
    public HierarchyOption(string path, int maxDepth = 5)
    {
        EnsureArg.IsNotNull(path, nameof(path));

        this.Path = path;
        this.MaxDepth = maxDepth;
    }

    /// <summary>
    ///     Represents an expression used in an <see cref="IncludeOption{TEntity}" />.
    /// </summary>
    /// <remarks>
    ///     The <c>Expression</c> property defines an Expression{Func{TEntity, object}}
    ///     that specifies how to hierarchy related entities in a query. When set, this expression
    ///     will be used to hierarchy related entities via a queryable <c>Include</c> operation.
    /// </remarks>
    public Expression<Func<TEntity, object>> Expression { get; }

    public int MaxDepth { get; }

    /// <summary>
    ///     Gets the string representation of the hierarchy path used for eager loading related data
    ///     in queries against the repository. This property specifies the navigation property name
    ///     to be included in the query.
    /// </summary>
    public string Path { get; }
}