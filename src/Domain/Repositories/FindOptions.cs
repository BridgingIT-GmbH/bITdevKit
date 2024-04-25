﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Various options to specify the <see cref="IGenericRepository{T}" /> find operations.
/// </summary>
/// <seealso cref="Domain.IFindOptions{TEntity}" />
public class FindOptions<TEntity> : IFindOptions<TEntity>
     where TEntity : class, IEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FindOptions{T}"/> class.
    /// </summary>
    public FindOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FindOptions{T}"/> class.
    /// </summary>
    /// <param name="skip">The skip amount.</param>
    /// <param name="take">The take amount.</param>
    /// <param name="order">The order option.</param>
    /// <param name="orderExpression">the order expresion.</param>
    /// <param name="orders">The order options.</param>
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
    /// Gets or sets the skip amount.
    /// </summary>
    /// <value>
    /// The skip.
    /// </value>
    public int? Skip { get; set; }

    /// <summary>
    /// Gets or sets the take amount.
    /// </summary>
    /// <value>
    /// The take.
    /// </value>
    public int? Take { get; set; }

    /// <summary>
    /// Gets or sets the no tracking option.
    /// </summary>
    public bool NoTracking { get; set; }

    /// <summary>
    /// Gets or sets the distinction.
    /// </summary>
    /// <value>
    /// The distinction.
    /// </value>
    public DistinctOption<TEntity> Distinct { get; set; }

    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    /// <value>
    /// The order.
    /// </value>
    public OrderOption<TEntity> Order { get; set; }

    /// <summary>
    /// Gets or sets the orders.
    /// </summary>
    /// <value>
    /// The orders.
    /// </value>
    public IEnumerable<OrderOption<TEntity>> Orders { get; set; }

    /// <summary>
    /// Gets or sets the include.
    /// </summary>
    /// <value>
    /// The order.
    /// </value>
    public IncludeOption<TEntity> Include { get; set; }

    /// <summary>
    /// Gets or sets the includes.
    /// </summary>
    /// <value>
    /// The includes.
    /// </value>
    public IEnumerable<IncludeOption<TEntity>> Includes { get; set; }

    /// <summary>
    /// Determines whether this instance has orders.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if this instance has orders; otherwise, <c>false</c>.
    /// </returns>
    public bool HasOrders() => this.Order is not null || this.Orders.SafeAny();

    /// <summary>
    /// Determines whether this instance has includes.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if this instance has includes; otherwise, <c>false</c>.
    /// </returns>
    public bool HasIncludes() => this.Include is not null || this.Includes.SafeAny();
}