// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using BridgingIT.DevKit.Common;

/// <summary>A validated filter condition echoed back to the UI.</summary>
/// <param name="Path">The canonical property path.</param>
/// <param name="Operator">The wire token of the operator (for example <c>eq</c>).</param>
/// <param name="Value">The raw value text, or null for operators without a value.</param>
internal sealed record EntitiesDashboardCondition(string Path, string Operator, string Value);

/// <summary>
/// Translates the client query state into a <see cref="FilterModel"/>. Every client-supplied field and operator is validated
/// against the entity descriptors; no reflection is performed on user strings.
/// </summary>
internal static class EntitiesDashboardFilterBuilder
{
    private static readonly Dictionary<string, FilterOperator> Operators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = FilterOperator.Equal,
        ["neq"] = FilterOperator.NotEqual,
        ["isnull"] = FilterOperator.IsNull,
        ["isnotnull"] = FilterOperator.IsNotNull,
        ["isempty"] = FilterOperator.IsEmpty,
        ["isnotempty"] = FilterOperator.IsNotEmpty,
        ["gt"] = FilterOperator.GreaterThan,
        ["gte"] = FilterOperator.GreaterThanOrEqual,
        ["lt"] = FilterOperator.LessThan,
        ["lte"] = FilterOperator.LessThanOrEqual,
        ["contains"] = FilterOperator.Contains,
        ["doesnotcontain"] = FilterOperator.DoesNotContain,
        ["startswith"] = FilterOperator.StartsWith,
        ["doesnotstartwith"] = FilterOperator.DoesNotStartWith,
        ["endswith"] = FilterOperator.EndsWith,
        ["doesnotendwith"] = FilterOperator.DoesNotEndWith
    };

    private static readonly FilterOperator[] NullOperators = [FilterOperator.IsNull, FilterOperator.IsNotNull];
    private static readonly FilterOperator[] EqualityOperators = [FilterOperator.Equal, FilterOperator.NotEqual, .. NullOperators];

    private static readonly FilterOperator[] OrderedOperators =
        [.. EqualityOperators, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual];

    private static readonly FilterOperator[] TextOperators =
    [
        .. EqualityOperators, FilterOperator.IsEmpty, FilterOperator.IsNotEmpty,
        FilterOperator.Contains, FilterOperator.DoesNotContain, FilterOperator.StartsWith, FilterOperator.DoesNotStartWith,
        FilterOperator.EndsWith, FilterOperator.DoesNotEndWith
    ];

    /// <summary>Gets the operator wire tokens that are valid for a property.</summary>
    /// <param name="property">The property descriptor.</param>
    /// <returns>The valid operator tokens in display order.</returns>
    public static IReadOnlyList<string> GetOperatorTokens(EntityPropertyDescriptor property) =>
        GetAllowed(property).Select(o => Operators.First(p => p.Value == o).Key).ToList();

    /// <summary>Parses the raw <c>f</c> values into validated conditions.</summary>
    /// <param name="entity">The entity.</param>
    /// <param name="query">The query.</param>
    /// <returns>The conditions or a failed result with a message.</returns>
    public static Result<IReadOnlyList<EntitiesDashboardCondition>> ParseConditions(EntitiesDashboardEntity entity, EntitiesDashboardQuery query)
    {
        var conditions = new List<EntitiesDashboardCondition>();
        if (query.Filters.Count > EntitiesDashboardQuery.MaxConditions)
        {
            return Fail<IReadOnlyList<EntitiesDashboardCondition>>($"At most {EntitiesDashboardQuery.MaxConditions} filter conditions are allowed.");
        }

        foreach (var raw in query.Filters)
        {
            var parts = raw.Split('~', 3);
            if (parts.Length < 2)
            {
                return Fail<IReadOnlyList<EntitiesDashboardCondition>>("A filter condition is malformed.");
            }

            var property = FindFilterable(entity, parts[0]);
            if (property == null)
            {
                return Fail<IReadOnlyList<EntitiesDashboardCondition>>($"'{parts[0]}' cannot be used in a filter.");
            }

            if (!Operators.TryGetValue(parts[1], out var op) || !GetAllowed(property).Contains(op))
            {
                return Fail<IReadOnlyList<EntitiesDashboardCondition>>($"The operator '{parts[1]}' is not supported for '{property.Path}'.");
            }

            conditions.Add(new EntitiesDashboardCondition(property.Path, parts[1].ToLowerInvariant(), parts.Length > 2 ? parts[2] : null));
        }

        return Result<IReadOnlyList<EntitiesDashboardCondition>>.Success(conditions);
    }

    /// <summary>Builds the filter model for one batch.</summary>
    /// <param name="entity">The entity.</param>
    /// <param name="query">The query.</param>
    /// <param name="batchSize">The batch size.</param>
    /// <returns>The filter model or a failed result with a message.</returns>
    public static Result<FilterModel> Build(EntitiesDashboardEntity entity, EntitiesDashboardQuery query, int batchSize)
    {
        var conditions = ParseConditions(entity, query);
        if (conditions.IsFailure)
        {
            return Result<FilterModel>.Failure(conditions.Messages.FirstOrDefault(), conditions.Errors.FirstOrDefault());
        }

        var filters = new List<FilterCriteria>();

        if (!string.IsNullOrWhiteSpace(query.QuickSearch))
        {
            var searchable = GetSearchable(entity).ToList();
            for (var i = 0; i < searchable.Count; i++)
            {
                // an OR group is closed by its last member, which keeps the default And logic
                filters.Add(new FilterCriteria(searchable[i].Path, FilterOperator.Contains, query.QuickSearch)
                {
                    Logic = i < searchable.Count - 1 ? FilterLogicOperator.Or : FilterLogicOperator.And
                });
            }
        }

        var userStart = filters.Count;
        foreach (var condition in conditions.Value)
        {
            var property = entity.FindProperty(condition.Path);
            var op = Operators[condition.Operator];
            var value = ParseValue(property, op, condition.Value);
            if (value.IsFailure)
            {
                return Result<FilterModel>.Failure(value.Messages.FirstOrDefault(), value.Errors.FirstOrDefault());
            }

            filters.Add(new FilterCriteria(property.Path, op, value.Value) { Logic = FilterLogicOperator.And });
        }

        if (query.MatchAny)
        {
            for (var i = userStart; i < filters.Count - 1; i++)
            {
                filters[i].Logic = FilterLogicOperator.Or;
            }
        }

        var model = new FilterModel
        {
            Page = Math.Max(1, query.Batch),
            PageSize = batchSize,
            NoTracking = true,
            Filters = filters,
            Orderings = BuildOrderings(entity, query)
        };

        return Result<FilterModel>.Success(model);
    }

    /// <summary>Gets the property used for sorting when it is valid, otherwise null.</summary>
    /// <param name="entity">The entity.</param>
    /// <param name="sort">The requested sort path.</param>
    /// <returns>The sortable descriptor or null.</returns>
    public static EntityPropertyDescriptor FindSortable(EntitiesDashboardEntity entity, string sort)
    {
        var property = entity.FindProperty(sort);

        return property is { IsSortable: true, IsMasked: false, IsHidden: false } ? property : null;
    }

    private static List<FilterOrderCriteria> BuildOrderings(EntitiesDashboardEntity entity, EntitiesDashboardQuery query)
    {
        var orderings = new List<FilterOrderCriteria>();
        var idName = entity.IdProperty?.Name ?? "Id";
        var sort = FindSortable(entity, query.Sort);

        if (sort != null)
        {
            orderings.Add(new FilterOrderCriteria
            {
                Field = sort.Path,
                Direction = query.Descending ? OrderDirection.Descending : OrderDirection.Ascending
            });
        }
        else if (entity.IsAuditable)
        {
            orderings.Add(new FilterOrderCriteria { Field = "AuditState.UpdatedDate", Direction = OrderDirection.Descending });
            orderings.Add(new FilterOrderCriteria { Field = "AuditState.CreatedDate", Direction = OrderDirection.Descending });
        }

        if (!orderings.Any(o => string.Equals(o.Field, idName, StringComparison.OrdinalIgnoreCase)))
        {
            orderings.Add(new FilterOrderCriteria { Field = idName, Direction = OrderDirection.Ascending });
        }

        return orderings;
    }

    private static IEnumerable<EntityPropertyDescriptor> GetSearchable(EntitiesDashboardEntity entity)
    {
        // value-object members are left out: stores that map them through value converters cannot translate member paths
        return entity.Properties.Where(p => !p.IsMasked && !p.IsHidden && p.IsFilterable && p.UnderlyingType == typeof(string));
    }

    private static EntityPropertyDescriptor FindFilterable(EntitiesDashboardEntity entity, string path)
    {
        var property = entity.FindProperty(path);
        if (property is not { IsFilterable: true, IsMasked: false, IsHidden: false })
        {
            return null;
        }

        // a masked or hidden parent value object must not leak through its members
        var parent = path.Contains('.') ? entity.FindProperty(path.Split('.')[0]) : null;

        return parent is { IsMasked: true } or { IsHidden: true } ? null : property;
    }

    private static FilterOperator[] GetAllowed(EntityPropertyDescriptor property) => property.EditorKind switch
    {
        EntitiesDashboardEditorKind.Text => TextOperators,
        EntitiesDashboardEditorKind.Number or EntitiesDashboardEditorKind.DateTime => OrderedOperators,
        EntitiesDashboardEditorKind.Guid or EntitiesDashboardEditorKind.Boolean or EntitiesDashboardEditorKind.Enum
            or EntitiesDashboardEditorKind.Enumeration => EqualityOperators,
        _ => []
    };

    private static Result<object> ParseValue(EntityPropertyDescriptor property, FilterOperator op, string text)
    {
        if (op is FilterOperator.IsNull or FilterOperator.IsNotNull or FilterOperator.IsEmpty or FilterOperator.IsNotEmpty)
        {
            return Result<object>.Success(null);
        }

        if (property.UnderlyingType == typeof(string))
        {
            return Result<object>.Success(text ?? string.Empty);
        }

        return EntitiesDashboardValueConverter.Parse(property.ClrType, text, property.Path);
    }

    private static Result<T> Fail<T>(string message) =>
        Result<T>.Failure(message, new ValidationError(message, "filter", null));
}
