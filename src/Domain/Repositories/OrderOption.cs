// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Linq.Expressions;
using Model;

/// <summary>
///     Represents ordering options for querying entities from repositories.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being queried, which must implement IEntity.</typeparam>
public class OrderOption<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OrderOption{TEntity}" /> class.
    ///     Represents an ordering option for querying entities.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity being queried.</typeparam>
    public OrderOption(
        Expression<Func<TEntity, object>> orderingExpression,
        OrderDirection direction = OrderDirection.Ascending)
    {
        EnsureArg.IsNotNull(orderingExpression, nameof(orderingExpression));

        this.Expression = orderingExpression;
        this.Direction = direction;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OrderOption{TEntity}" /> class.
    ///     The OrderOption class is used to specify ordering information for entities.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public OrderOption(string ordering)
    {
        EnsureArg.IsNotNull(ordering, nameof(ordering));

        this.Ordering = ordering;
    }

    /// <summary>
    ///     Gets or sets the expression used for ordering entities.
    /// </summary>
    /// <remarks>
    ///     This property holds an expression that specifies the field or property used for ordering the entities.
    ///     It is of type Expression{Func{TEntity, object}} and can be used to dynamically construct order clauses in queries.
    /// </remarks>
    public Expression<Func<TEntity, object>> Expression { get; set; }

    /// <summary>
    ///     Gets the string representation of the ordering criteria.
    /// </summary>
    /// <value>
    ///     The ordering criteria in a string format typically representing the field name and direction (e.g., "fieldname
    ///     ascending").
    /// </value>
    public string Ordering { get; } // of the form >   fieldname [ascending|descending], ...

    /// <summary>
    ///     Gets or sets the direction of the order, which determines whether the order
    ///     should be in ascending or descending sequence.
    /// </summary>
    /// <remarks>
    ///     The default order direction is ascending. This property uses the <see cref="OrderDirection" />
    ///     enumeration to specify the ordering direction.
    /// </remarks>
    public OrderDirection Direction { get; set; }
}