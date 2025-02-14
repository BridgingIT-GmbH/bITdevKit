// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq;

public static class FilterModelExtensions
{
    /// <summary>
    /// Merges the source filter models with the specifid filter models.
    /// Results in a modified source filter model with the other filter models merged in.
    /// </summary>
    /// <param name="source">The filter model to modify</param>
    /// <param name="filterModels">The filter models to merge in</param>
    public static FilterModel Merge(this FilterModel source, params FilterModel[] filterModels)
    {
        if (source == null)
        {
            return null;
        }

        if (filterModels == null || !filterModels.Any())
        {
            return source;
        }

        foreach (var filterModel in filterModels.Where(x => x != null))
        {
            // Merge paging properties
            source.Page = filterModel.Page > 0 ? filterModel.Page : source.Page;
            source.PageSize = filterModel.PageSize > 0 ? filterModel.PageSize : source.PageSize;

            // Merge orderings with deduplication
            if (filterModel.Orderings?.Any() == true)
            {
                source.Orderings.RemoveAll(so =>
                    filterModel.Orderings.Any(fo => fo.Field == so.Field));
                source.Orderings.AddRange(filterModel.Orderings);
            }

            // Merge filters with deduplication
            if (filterModel.Filters?.Any() == true)
            {
                source.Filters.RemoveAll(sf =>
                    filterModel.Filters.Any(ff =>
                        ff.Field == sf.Field && ff.Operator == sf.Operator));
                source.Filters.AddRange(filterModel.Filters);
            }

            // Merge includes with deduplication
            if (filterModel.Includes?.Any() == true)
            {
                source.Includes.RemoveAll(si =>
                    filterModel.Includes.Contains(si));
                source.Includes.AddRange(filterModel.Includes);
            }

            // Merge hierarchy
            if (!string.IsNullOrEmpty(filterModel.Hierarchy))
            {
                source.Hierarchy = filterModel.Hierarchy;
                source.HierarchyMaxDepth = filterModel.HierarchyMaxDepth;
            }

            // Merge tracking
            source.NoTracking = source.NoTracking || filterModel.NoTracking;
        }

        return source;
    }

    /// <summary>
    /// Clears the whole filter model.
    /// </summary>
    /// <param name="source">The filter model to modify</param>
    public static FilterModel Clear(this FilterModel source)
    {
        if (source == null)
        {
            return null;
        }

        source.Page = 1;
        source.PageSize = 10;
        source.NoTracking = true;
        source.Orderings = [];
        source.Filters = [];
        source.Includes = [];
        source.Hierarchy = null;
        source.HierarchyMaxDepth = 5;

        return source;
    }

    /// <summary>
    /// Clears the whole filter model with everything related to the field.
    /// </summary>
    /// <param name="source">The filter model to modify</param>
    /// <param name="field">The filter models to merge in</param>
    public static FilterModel Clear(this FilterModel source, string field)
    {
        if (source == null || string.IsNullOrEmpty(field))
        {
            return source;
        }

        // Clear orderings for the field
        source.Orderings.RemoveAll(o => o.Field == field);

        // Clear filters for the field (including nested filters)
        source.Filters.RemoveAll(f => f.Field == field);
        foreach (var filter in source.Filters.Where(f => f.Filters?.Any() == true))
        {
            filter.Filters.RemoveAll(f => f.Field == field);
        }

        // Clear includes for the field
        source.Includes.RemoveAll(i => i == field);

        // Clear hierarchy if it matches the field
        if (source.Hierarchy == field)
        {
            source.Hierarchy = null;
            source.HierarchyMaxDepth = 5;
        }

        return source;
    }
}