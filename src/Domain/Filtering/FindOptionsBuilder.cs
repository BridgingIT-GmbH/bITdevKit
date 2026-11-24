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
        var options = BuildFilterOptions<TEntity>(filterModel);
        if (filterModel?.PageSize > 0)
        {
            options.Skip = filterModel.PageSize * (filterModel.Page - 1);
            options.Take = filterModel.PageSize;
        }

        return options;
    }

    internal static FindOptions<TEntity> BuildPaged<TEntity>(FilterModel filterModel, out int page, out int pageSize)
        where TEntity : class, IEntity
    {
        var paging = NormalizePaging(filterModel?.Page ?? 0, filterModel?.PageSize ?? 0);
        page = paging.Page;
        pageSize = paging.PageSize;

        var options = BuildFilterOptions<TEntity>(filterModel);
        options.Skip = paging.Skip;
        options.Take = paging.PageSize;
        return options;
    }

    internal static Result<FindOptions<TEntity>> BuildPagedResult<TEntity>(FilterModel filterModel)
        where TEntity : class, IEntity
    {
        try
        {
            return Result<FindOptions<TEntity>>.Success(BuildPaged<TEntity>(filterModel, out _, out _));
        }
        catch (OverflowException ex)
        {
            return Result<FindOptions<TEntity>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    internal static (int Page, int PageSize, int Skip) NormalizePaging(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = pageSize > 0 ? pageSize : 10;
        return (page, pageSize, checked((page - 1) * pageSize));
    }

    private static FindOptions<TEntity> BuildFilterOptions<TEntity>(FilterModel filterModel)
        where TEntity : class, IEntity =>
        filterModel is null ? new() : new()
        {
            Orders = OrderOptionBuilder.Build<TEntity>(filterModel.Orderings),
            Includes = IncludeOptionBuilder.Build<TEntity>(filterModel.Includes),
            Hierarchy = HierarchyOptionBuilder.Build<TEntity>(filterModel.Hierarchy, filterModel.HierarchyMaxDepth),
            NoTracking = filterModel.NoTracking
        };

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
