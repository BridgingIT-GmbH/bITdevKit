// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Collections.Generic;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Various options to specify the <see cref="IGenericRepository{TEntity}"/> find operations.
/// </summary>
public interface IFindOptions<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets or sets the skip amount.
    /// </summary>
    /// <value>
    /// The skip.
    /// </value>
    int? Skip { get; set; }

    /// <summary>
    /// Gets or sets the take amount.
    /// </summary>
    /// <value>
    /// The take.
    /// </value>
    int? Take { get; set; }

    /// <summary>
    /// Gets or sets the NoTracking.
    /// </summary>
    bool NoTracking { get; set; }

    /// <summary>
    /// Gets or sets the distinction.
    /// </summary>
    /// <value>
    /// The distinction.
    /// </value>
    DistinctOption<TEntity> Distinct { get; set; }

    /// <summary>
    /// Gets or sets the ordering.
    /// </summary>
    /// <value>
    /// The ordering.
    /// </value>
    OrderOption<TEntity> Order { get; set; }

    /// <summary>
    /// Gets or sets the orderings.
    /// </summary>
    /// <value>
    /// The orderings.
    /// </value>
    IEnumerable<OrderOption<TEntity>> Orders { get; set; }

    /// <summary>
    /// Gets or sets the include.
    /// </summary>
    /// <value>
    /// The order.
    /// </value>
    IncludeOption<TEntity> Include { get; set; }

    /// <summary>
    /// Gets or sets the includes.
    /// </summary>
    /// <value>
    /// The includes.
    /// </value>
    IEnumerable<IncludeOption<TEntity>> Includes { get; set; }

    /// <summary>
    /// Determines whether this instance has orderings.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if this instance has orderings; otherwise, <c>false</c>.
    /// </returns>
    bool HasOrders();

    /// <summary>
    /// Determines whether this instance has includes.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if this instance has includes; otherwise, <c>false</c>.
    /// </returns>
    bool HasIncludes();
}