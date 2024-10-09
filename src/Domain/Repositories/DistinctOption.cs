// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Represents an option to specify distinct selection criteria in queries.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public class DistinctOption<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DistinctOption{TEntity}" /> class.
    ///     Represents an option that allows for projection of distinct values
    ///     from a particular entity type.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public DistinctOption() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DistinctOption{TEntity}" /> class.
    ///     Represents a distinct option for querying entities.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public DistinctOption(Expression<Func<TEntity, object>> expression)
    {
        EnsureArg.IsNotNull(expression, nameof(expression));

        this.Expression = expression;
    }

    /// <summary>
    ///     Gets or sets the expression used to determine the distinctness criteria.
    /// </summary>
    /// <value>
    ///     An expression that defines how to select distinct elements from a sequence of entities.
    /// </value>
    public Expression<Func<TEntity, object>> Expression { get; set; }
}