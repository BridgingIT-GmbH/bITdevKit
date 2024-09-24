// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Linq.Expressions;
using Common;
using Model;

/// <summary>
///     Various options to specify the find operations for a repository.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public class FindOptions<TEntity> : IFindOptions<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FindOptions{TEntity}" /> class.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public FindOptions() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FindOptions{TEntity}" /> class.
    ///     Provides options for finding entities in the repository.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public FindOptions(
        int? skip = null,
        int? take = null,
        OrderOption<TEntity> order = null,
        Expression<Func<TEntity, object>> orderExpression = null,
        IEnumerable<OrderOption<TEntity>> orders = null,
        IncludeOption<TEntity> include = null,
        Expression<Func<TEntity, object>> includeExpression = null,
        IEnumerable<IncludeOption<TEntity>> includes = null,
        DistinctOption<TEntity> distinct = null,
        Expression<Func<TEntity, object>> distinctExpression = null)
    {
        this.Take = take;
        this.Skip = skip;
        this.Order = orderExpression is not null ? new OrderOption<TEntity>(orderExpression) : order;
        this.Orders = orders;
        this.Include = includeExpression is not null ? new IncludeOption<TEntity>(includeExpression) : include;
        this.Includes = includes;
        this.Distinct = distinctExpression is not null ? new DistinctOption<TEntity>(distinctExpression) : distinct;
    }

    /// <summary>
    ///     Gets or sets the number of elements to skip before starting to return elements.
    /// </summary>
    /// <value>
    ///     The number of elements to skip.
    /// </value>
    public int? Skip { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of entities to retrieve.
    /// </summary>
    /// <value>
    ///     The maximum number of entities to be retrieved.
    /// </value>
    public int? Take { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the query should be executed with no tracking.
    /// </summary>
    /// <value>
    ///     True if the query should be executed without tracking; otherwise, false.
    /// </value>
    public bool NoTracking { get; set; }

    /// <summary>
    ///     Gets or sets the distinct option for the query.
    /// </summary>
    /// <value>
    ///     The distinct option.
    /// </value>
    public DistinctOption<TEntity> Distinct { get; set; }

    /// <summary>
    ///     Gets or sets the order option for sorting the query results.
    /// </summary>
    /// <value>
    ///     An <see cref="OrderOption{TEntity}" /> that specifies the ordering criteria.
    /// </value>
    public OrderOption<TEntity> Order { get; set; }

    /// <summary>
    ///     Gets or sets a collection of order options for sorting the query results.
    /// </summary>
    /// <value>
    ///     The collection of order options.
    /// </value>
    public IEnumerable<OrderOption<TEntity>> Orders { get; set; }

    /// <summary>
    ///     Gets or sets the inclusion option for related entities.
    /// </summary>
    /// <value>
    ///     The inclusion option used to specify which related entities to include in the query results.
    /// </value>
    public IncludeOption<TEntity> Include { get; set; }

    /// <summary>
    ///     Gets or sets the collection of include options for find operations.
    /// </summary>
    /// <value>
    ///     A collection of <see cref="IncludeOption{TEntity}" /> to specify related entities to include in the query result.
    /// </value>
    public IEnumerable<IncludeOption<TEntity>> Includes { get; set; }

    /// <summary>
    ///     Determines whether this instance has orders.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if this instance has orders; otherwise, <c>false</c>.
    /// </returns>
    public bool HasOrders()
    {
        return this.Order is not null || this.Orders.SafeAny();
    }

    /// <summary>
    ///     Determines whether this instance has includes.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if this instance has includes; otherwise, <c>false</c>.
    /// </returns>
    public bool HasIncludes()
    {
        return this.Include is not null || this.Includes.SafeAny();
    }
}