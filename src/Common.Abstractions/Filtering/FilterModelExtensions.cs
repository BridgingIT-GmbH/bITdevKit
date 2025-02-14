// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq;
using System.Text.Json;
using System.Web;

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

    public static bool IsEmpty(this FilterModel source)
    {
        if (source == null)
        {
            return true;
        }

        return !source.Filters.Any() &&
               !source.Orderings.Any() &&
               !source.Includes.Any() &&
               string.IsNullOrEmpty(source.Hierarchy) &&
               source.Page == 1 &&
               source.PageSize == 10;
    }

    public static bool HasFilters(this FilterModel source, string field)
    {
        if (source?.Filters == null)
        {
            return false;
        }

        return source.Filters.Any(f => f.Field == field) ||
               source.Filters.Any(f => f.Filters?.Any(nested => nested.Field == field) == true);
    }

    public static bool HasOrdering(this FilterModel source, string field)
    {
        return source?.Orderings?.Any(o => o.Field == field) == true;
    }

    public static bool HasInclude(this FilterModel source, string path)
    {
        return source?.Includes?.Contains(path) == true;
    }

    public static FilterModel AddOrUpdateFilter(this FilterModel source, string field, FilterOperator op, object value)
    {
        if (source == null)
        {
            return null;
        }

        source.Filters.RemoveAll(f => f.Field == field && f.Operator == op);
        source.Filters.Add(new FilterCriteria(field, op, value));

        return source;
    }

    public static FilterModel RemoveFilter(this FilterModel source, string field, FilterOperator op)
    {
        if (source?.Filters == null)
        {
            return source;
        }

        source.Filters.RemoveAll(f => f.Field == field && f.Operator == op);
        foreach (var filter in source.Filters.Where(f => f.Filters?.Any() == true))
        {
            filter.Filters.RemoveAll(f => f.Field == field && f.Operator == op);
        }

        return source;
    }

    public static FilterModel SetHierarchy(this FilterModel source, string path, int? maxDepth = null)
    {
        if (source == null)
        {
            return null;
        }

        source.Hierarchy = path;
        if (maxDepth.HasValue)
        {
            source.HierarchyMaxDepth = maxDepth.Value;
        }

        return source;
    }

    public static FilterModel ReplaceOrdering(this FilterModel source, string field, OrderDirection direction)
    {
        if (source == null)
        {
            return null;
        }

        source.Orderings.RemoveAll(o => o.Field == field);
        source.Orderings.Add(new FilterOrderCriteria { Field = field, Direction = direction });

        return source;
    }

    public static FilterCriteria GetFilter(this FilterModel source, string field, FilterOperator op)
    {
        if (source?.Filters == null)
        {
            return null;
        }

        return source.Filters.FirstOrDefault(f => f.Field == field && f.Operator == op) ??
               source.Filters
                   .Where(f => f.Filters?.Any() == true)
                   .SelectMany(f => f.Filters)
                   .FirstOrDefault(f => f.Field == field && f.Operator == op);
    }

    public static IEnumerable<FilterCriteria> GetFilters(this FilterModel source, string field)
    {
        if (source?.Filters == null)
        {
            return Enumerable.Empty<FilterCriteria>();
        }

        return GetFiltersRecursive(source.Filters, field);
    }

    private static IEnumerable<FilterCriteria> GetFiltersRecursive(IEnumerable<FilterCriteria> filters, string field)
    {
        if (filters == null)
        {
            yield break;
        }

        foreach (var filter in filters)
        {
            if (filter.Field == field)
            {
                yield return filter;
            }

            if (filter.Filters?.Any() == true)
            {
                foreach (var nestedFilter in GetFiltersRecursive(filter.Filters, field))
                {
                    yield return nestedFilter;
                }
            }
        }
    }

    public static FilterOrderCriteria GetOrdering(this FilterModel source, string field)
    {
        return source?.Orderings?.FirstOrDefault(o => o.Field == field);
    }

    //public static FilterModel Clone(this FilterModel source)
    //{
    //    if (source == null)
    //    {
    //        return null;
    //    }

    //    return JsonSerializer.Deserialize<FilterModel>(JsonSerializer.Serialize(source));
    //}

    public static FilterModel WithoutTracking(this FilterModel source)
    {
        if (source == null)
        {
            return null;
        }

        source.NoTracking = true;
        return source;
    }

    public static FilterModel WithPaging(this FilterModel source, int page, int pageSize)
    {
        if (source == null)
        {
            return null;
        }

        source.Page = page > 0 ? page : 1;
        source.PageSize = pageSize > 0 ? pageSize : 10;
        return source;
    }

    public static FilterModel WithDefaultPaging(this FilterModel source)
    {
        return source.WithPaging(1, 10);
    }

    public static string ToQueryString(this FilterModel source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        var dict = new Dictionary<string, string>
        {
            ["page"] = source.Page.ToString(),
            ["pageSize"] = source.PageSize.ToString(),
            ["noTracking"] = source.NoTracking.ToString()
        };

        if (source.Orderings?.Any() == true)
        {
            dict["orderings"] = JsonSerializer.Serialize(source.Orderings);
        }

        if (source.Filters?.Any() == true)
        {
            dict["filters"] = JsonSerializer.Serialize(source.Filters);
        }

        if (source.Includes?.Any() == true)
        {
            dict["includes"] = JsonSerializer.Serialize(source.Includes);
        }

        if (!string.IsNullOrEmpty(source.Hierarchy))
        {
            dict["hierarchy"] = source.Hierarchy;
            dict["hierarchyMaxDepth"] = source.HierarchyMaxDepth.ToString();
        }

        return string.Join("&", dict.Select(kvp => $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));
    }

    public static FilterModel FromQueryString(string queryString)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return new FilterModel();
        }

        var dict = HttpUtility.ParseQueryString(queryString);
        var result = new FilterModel();

        if (int.TryParse(dict["page"], out var page))
        {
            result.Page = page;
        }

        if (int.TryParse(dict["pageSize"], out var pageSize))
        {
            result.PageSize = pageSize;
        }

        if (bool.TryParse(dict["noTracking"], out var noTracking))
        {
            result.NoTracking = noTracking;
        }

        var orderings = dict["orderings"];
        if (!string.IsNullOrEmpty(orderings))
        {
            result.Orderings = JsonSerializer.Deserialize<List<FilterOrderCriteria>>(orderings);
        }

        var filters = dict["filters"];
        if (!string.IsNullOrEmpty(filters))
        {
            result.Filters = JsonSerializer.Deserialize<List<FilterCriteria>>(filters);
        }

        var includes = dict["includes"];
        if (!string.IsNullOrEmpty(includes))
        {
            result.Includes = JsonSerializer.Deserialize<List<string>>(includes);
        }

        result.Hierarchy = dict["hierarchy"];
        if (int.TryParse(dict["hierarchyMaxDepth"], out var maxDepth))
        {
            result.HierarchyMaxDepth = maxDepth;
        }

        return result;
    }

    public static IDictionary<string, object> ToDictionary(this FilterModel source)
    {
        if (source == null)
        {
            return new Dictionary<string, object>();
        }

        return new Dictionary<string, object>
        {
            ["page"] = source.Page,
            ["pageSize"] = source.PageSize,
            ["noTracking"] = source.NoTracking,
            ["orderings"] = source.Orderings,
            ["filters"] = source.Filters,
            ["includes"] = source.Includes,
            ["hierarchy"] = source.Hierarchy,
            ["hierarchyMaxDepth"] = source.HierarchyMaxDepth
        };
    }
}