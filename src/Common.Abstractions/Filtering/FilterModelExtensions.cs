// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq;
using System.Text.Json;
using System.Web;

/// <summary>
/// Provides mutation, inspection, and serialization operations for <see cref="FilterModel"/> instances.
/// </summary>
/// <example>
/// <code>
/// var model = new FilterModel()
///     .AddOrUpdateFilter("Status", FilterOperator.Equal, "Active")
///     .WithPaging(1, 25);
/// var queryString = model.ToQueryString();
/// </code>
/// </example>
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

        source.Page = 0;
        source.PageSize = 0;
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

    /// <summary>
    /// Determines whether the model has no filters, orderings, includes, hierarchy, or paging values.
    /// </summary>
    /// <param name="source">The model to inspect.</param>
    /// <returns><see langword="true"/> when <paramref name="source"/> is <see langword="null"/> or contains none of the inspected query criteria; otherwise, <see langword="false"/>.</returns>
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
               source.Page == 0 &&
               source.PageSize == 0;
    }

    /// <summary>
    /// Determines whether the model contains at least one top-level filter.
    /// </summary>
    /// <param name="source">The model to inspect.</param>
    /// <returns><see langword="true"/> when a top-level filter exists; otherwise, <see langword="false"/>.</returns>
    public static bool HasFilters(this FilterModel source)
    {
        if (source?.Filters == null)
        {
            return false;
        }

        return source.Filters.Any();
    }

    /// <summary>
    /// Determines whether a top-level or directly nested filter targets a field.
    /// </summary>
    /// <param name="source">The model to inspect.</param>
    /// <param name="field">The case-sensitive field path to find.</param>
    /// <returns><see langword="true"/> when the field occurs in a top-level or directly nested filter; otherwise, <see langword="false"/>.</returns>
    public static bool HasFilters(this FilterModel source, string field)
    {
        if (source?.Filters == null)
        {
            return false;
        }

        return source.Filters.Any(f => f.Field == field) ||
               source.Filters.Any(f => f.Filters?.Any(nested => nested.Field == field) == true);
    }

    /// <summary>
    /// Determines whether the model contains at least one ordering criterion.
    /// </summary>
    /// <param name="source">The model to inspect.</param>
    /// <returns><see langword="true"/> when an ordering exists; otherwise, <see langword="false"/>.</returns>
    public static bool HasOrdering(this FilterModel source)
    {
        return source?.Orderings?.Any() == true;
    }

    /// <summary>
    /// Determines whether the model orders by a specified field.
    /// </summary>
    /// <param name="source">The model to inspect.</param>
    /// <param name="field">The case-sensitive field path to find.</param>
    /// <returns><see langword="true"/> when a matching ordering exists; otherwise, <see langword="false"/>.</returns>
    public static bool HasOrdering(this FilterModel source, string field)
    {
        return source?.Orderings?.Any(o => o.Field == field) == true;
    }

    /// <summary>
    /// Determines whether the model contains at least one include path.
    /// </summary>
    /// <param name="source">The model to inspect.</param>
    /// <returns><see langword="true"/> when an include exists; otherwise, <see langword="false"/>.</returns>
    public static bool HasInclude(this FilterModel source)
    {
        return source?.Includes?.Any() == true;
    }

    /// <summary>
    /// Determines whether the model contains a specified include path.
    /// </summary>
    /// <param name="source">The model to inspect.</param>
    /// <param name="path">The case-sensitive include path to find.</param>
    /// <returns><see langword="true"/> when the path is included; otherwise, <see langword="false"/>.</returns>
    public static bool HasInclude(this FilterModel source, string path)
    {
        return source?.Includes?.Contains(path) == true;
    }

    /// <summary>
    /// Replaces the top-level filter for a field and operator, then appends the supplied criterion.
    /// </summary>
    /// <param name="source">The model to modify.</param>
    /// <param name="field">The field path targeted by the criterion.</param>
    /// <param name="op">The comparison operation applied to the field.</param>
    /// <param name="value">The value associated with the comparison.</param>
    /// <returns>The modified model, or <see langword="null"/> when <paramref name="source"/> is <see langword="null"/>.</returns>
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

    /// <summary>
    /// Removes matching filters from the top level and from directly nested filter collections.
    /// </summary>
    /// <param name="source">The model to modify.</param>
    /// <param name="field">The case-sensitive field path to remove.</param>
    /// <param name="op">The comparison operation that must match.</param>
    /// <returns>The modified model, or the original value when no filter collection is available.</returns>
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

    /// <summary>
    /// Sets the hierarchy path and optionally replaces its maximum traversal depth.
    /// </summary>
    /// <param name="source">The model to modify.</param>
    /// <param name="path">The hierarchy path to select.</param>
    /// <param name="maxDepth">The maximum traversal depth, or <see langword="null"/> to preserve the current value.</param>
    /// <returns>The modified model, or <see langword="null"/> when <paramref name="source"/> is <see langword="null"/>.</returns>
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

    /// <summary>
    /// Replaces any ordering for a field with one ordering in the requested direction.
    /// </summary>
    /// <param name="source">The model to modify.</param>
    /// <param name="field">The case-sensitive field path to order by.</param>
    /// <param name="direction">The ordering direction.</param>
    /// <returns>The modified model, or <see langword="null"/> when <paramref name="source"/> is <see langword="null"/>.</returns>
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

    /// <summary>
    /// Finds the first matching top-level filter, falling back to directly nested filters.
    /// </summary>
    /// <param name="source">The model to search.</param>
    /// <param name="field">The case-sensitive field path to find.</param>
    /// <param name="op">The comparison operation that must match.</param>
    /// <returns>The first matching criterion, or <see langword="null"/> when none exists.</returns>
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

    /// <summary>
    /// Recursively enumerates all filters that target a specified field.
    /// </summary>
    /// <param name="source">The model to search.</param>
    /// <param name="field">The case-sensitive field path to find.</param>
    /// <returns>A lazy sequence of matching criteria, or an empty sequence when the model has no filters.</returns>
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

    /// <summary>
    /// Finds the first ordering criterion for a specified field.
    /// </summary>
    /// <param name="source">The model to search.</param>
    /// <param name="field">The case-sensitive field path to find.</param>
    /// <returns>The first matching ordering, or <see langword="null"/> when none exists.</returns>
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

    /// <summary>
    /// Marks the model so consumers can execute the query without change tracking.
    /// </summary>
    /// <param name="source">The model to modify.</param>
    /// <returns>The modified model, or <see langword="null"/> when <paramref name="source"/> is <see langword="null"/>.</returns>
    public static FilterModel WithoutTracking(this FilterModel source)
    {
        if (source == null)
        {
            return null;
        }

        source.NoTracking = true;
        return source;
    }

    /// <summary>
    /// Sets paging values, substituting page <c>1</c> and page size <c>10</c> for non-positive inputs.
    /// </summary>
    /// <param name="source">The model to modify.</param>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The requested number of items per page.</param>
    /// <returns>The modified model, or <see langword="null"/> when <paramref name="source"/> is <see langword="null"/>.</returns>
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

    /// <summary>
    /// Sets paging to the first page with ten items.
    /// </summary>
    /// <param name="source">The model to modify.</param>
    /// <returns>The modified model, or <see langword="null"/> when <paramref name="source"/> is <see langword="null"/>.</returns>
    public static FilterModel WithDefaultPaging(this FilterModel source)
    {
        return source.WithPaging(1, 10);
    }

    /// <summary>
    /// Serializes the model into URL-encoded query-string key-value pairs.
    /// </summary>
    /// <param name="source">The model to serialize.</param>
    /// <returns>The query string without a leading question mark, or an empty string for a <see langword="null"/> model.</returns>
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

    /// <summary>
    /// Parses paging, tracking, ordering, filtering, include, and hierarchy values from a query string.
    /// </summary>
    /// <param name="queryString">The URL query string to parse.</param>
    /// <returns>A populated model; malformed scalar values are left at their model defaults.</returns>
    /// <exception cref="JsonException">Thrown when a present JSON-encoded collection value is invalid.</exception>
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

    /// <summary>
    /// Projects all model components into a dictionary keyed by their query-model names.
    /// </summary>
    /// <param name="source">The model to project.</param>
    /// <returns>A dictionary containing every model component, or an empty dictionary for a <see langword="null"/> model.</returns>
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
