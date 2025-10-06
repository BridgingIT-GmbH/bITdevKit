// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Builder class for creating FindOptions instances from FilterModel.
/// </summary>
public static class FindOptionsBuilder
{
    /// <summary>
    ///     Builds a FindOptions instance from the provided FilterModel.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity for which to build the options.</typeparam>
    /// <param name="filterModel">The filter model containing the search criteria.</param>
    /// <returns>A FindOptions instance configured according to the filter model.</returns>
    public static FindOptions<TEntity> Build<TEntity>(FilterModel filterModel)
        where TEntity : class, IEntity
    {
        if (filterModel == null)
        {
            return new FindOptions<TEntity>();
        }

        return new FindOptions<TEntity>
        {
            Orders = OrderOptionBuilder.Build<TEntity>(filterModel.Orderings),
            Includes = IncludeOptionBuilder.Build<TEntity>(filterModel.Includes),
            Hierarchy = HierarchyOptionBuilder.Build<TEntity>(filterModel.Hierarchy, filterModel.HierarchyMaxDepth),
            Skip = filterModel.PageSize > 0 ? filterModel.PageSize * (filterModel.Page - 1) : null,
            Take = filterModel.PageSize > 0 ? filterModel.PageSize : null,
            NoTracking = filterModel.NoTracking
        };
    }

    /// <summary>
    ///     Builds FindOptions with default settings and applies custom configurations.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity for which to build the options.</typeparam>
    /// <param name="configure">Action to configure the FindOptions.</param>
    /// <returns>A configured FindOptions instance.</returns>
    public static FindOptions<TEntity> Create<TEntity>(Action<FindOptions<TEntity>> configure = null)
        where TEntity : class, IEntity
    {
        var options = new FindOptions<TEntity>();
        configure?.Invoke(options);

        return options;
    }
}