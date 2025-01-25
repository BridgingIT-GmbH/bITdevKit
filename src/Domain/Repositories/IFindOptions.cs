// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Interface for specifying various options to find operations in a repository.
/// </summary>
public interface IFindOptions<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Gets or sets the number of items to skip in a query.
    /// </summary>
    /// <value>
    ///     The number of items to skip.
    /// </value>
    int? Skip { get; set; }

    /// <summary>
    ///     Gets or sets the number of records to return.
    /// </summary>
    /// <value>
    ///     The number of records.
    /// </value>
    int? Take { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the query should be executed with tracking disabled.
    /// </summary>
    /// <value>
    ///     <c>true</c> if the query should not track changes to the entities; otherwise, <c>false</c>.
    /// </value>
    bool NoTracking { get; set; }

    /// <summary>
    ///     Gets or sets the option to distinct the query results.
    /// </summary>
    /// <value>
    ///     The distinct option.
    /// </value>
    DistinctOption<TEntity> Distinct { get; set; }

    /// <summary>
    ///     Gets or sets the order options for querying entities.
    /// </summary>
    /// <value>
    ///     The order options used to define how entities should be ordered.
    /// </value>
    OrderOption<TEntity> Order { get; set; }

    /// <summary>
    ///     Gets or sets the collection of order options.
    /// </summary>
    /// <value>
    ///     The collection of <see cref="OrderOption{TEntity}" /> that define the order criteria.
    /// </value>
    IEnumerable<OrderOption<TEntity>> Orders { get; set; }

    /// <summary>
    ///     Gets or sets the include options for related entities.
    /// </summary>
    /// <value>
    ///     The include option used to specify related entities to include in the query results.
    /// </value>
    IncludeOption<TEntity> Include { get; set; }

    /// <summary>
    ///     Gets or sets the collection of include options.
    /// </summary>
    /// <value>
    ///     The collection of include options.
    /// </value>
    IEnumerable<IncludeOption<TEntity>> Includes { get; set; }

    HierarchyOption<TEntity> Hierarchy { get; set; }

    /// <summary>
    ///     Determines whether this instance has orderings.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if this instance has orderings; otherwise, <c>false</c>.
    /// </returns>
    bool HasOrders();

    /// <summary>
    ///     Determines whether this instance has includes.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if this instance has includes; otherwise, <c>false</c>.
    /// </returns>
    bool HasIncludes();

    bool HasHierarchy();
}