// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;

/// <summary>Applies scalar operational search criteria to provider queries and in-memory snapshots.</summary>
/// <remarks>Includes, hierarchy and custom specifications are not supported by operational searches.</remarks>
/// <example><code>var page = records.AsQueryable().ApplyFilter(filter).ApplyOrder(filter).ApplyPage(filter).ToArray();</code></example>
public static class FilterModelQueryExtensions
{
    /// <summary>Filters a query without ordering or paging it.</summary>
    /// <example><code>var total = records.ApplyFilter(filter).Count();</code></example>
    public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> source, FilterModel filter)
    {
        if (filter?.Includes?.Count > 0 || !string.IsNullOrEmpty(filter?.Hierarchy))
        {
            throw new ArgumentException("Operational search does not support includes or hierarchy.");
        }

        try
        {
            var parameter = Expression.Parameter(typeof(T), "item");
            var predicate = BuildGroup(parameter, filter?.Filters, 0);
            return source.Where(Expression.Lambda<Func<T, bool>>(predicate, parameter));
        }
        catch (InvalidOperationException exception)
        {
            throw new ArgumentException("Search operator is not supported for this field type.", exception);
        }
    }

    /// <summary>Applies explicit sort criteria, preserving existing order when none are supplied. GUID criteria use canonical text order to match seek comparisons.</summary>
    /// <example><code>var ordered = records.ApplyOrder(filter);</code></example>
    public static IQueryable<T> ApplyOrder<T>(this IQueryable<T> source, FilterModel filter, Expression<Func<T, object>> tieBreaker = null)
    {
        var first = true;
        foreach (var order in filter?.Orderings ?? [])
        {
            if (order is null || !Enum.IsDefined(order.Direction))
            {
                throw new ArgumentException("Invalid ordering criterion.");
            }

            var parameter = Expression.Parameter(typeof(T), "item");
            Expression property = Property(parameter, order.Field);
            ValidateScalar(property.Type);
            if (property.Type == typeof(Guid))
            {
                property = GuidText(property);
            }

            var method = first ? "OrderBy" : "ThenBy";
            method += order.Direction == OrderDirection.Descending ? "Descending" : string.Empty;
            source = source.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), method,
                [typeof(T), property.Type], source.Expression, Expression.Quote(Expression.Lambda(property, parameter))));
            first = false;
        }

        return !first && tieBreaker is not null
            ? ((IOrderedQueryable<T>)source).ThenBy(tieBreaker)
            : source;
    }

    /// <summary>Applies one-based paging with a default size of 50 and a maximum size of 1000.</summary>
    /// <example><code>var page = records.ApplyPage(filter).ToArray();</code></example>
    public static IQueryable<T> ApplyPage<T>(this IQueryable<T> source, FilterModel filter)
    {
        var page = Math.Max(1, filter?.Page ?? 1);
        var size = Math.Clamp(filter?.PageSize > 0 ? filter.PageSize : 50, 1, 1000);
        var skip = (long)(page - 1) * size;
        if (skip > int.MaxValue)
        {
            throw new ArgumentException("Search page exceeds the supported offset.");
        }

        return source.Skip((int)skip).Take(size);
    }

    private static Expression BuildGroup(Expression parameter, List<FilterCriteria> filters, int depth)
    {
        if (depth > 16 || filters?.Count > 100)
        {
            throw new ArgumentException("Search criteria exceed the supported complexity.");
        }

        Expression result = null;
        Expression alternatives = null;
        foreach (var filter in filters ?? [])
        {
            if (filter is null || filter.CustomType != FilterCustomType.None ||
                !string.IsNullOrEmpty(filter.SpecificationName) || filter.CompositeSpecification is not null ||
                !Enum.IsDefined(filter.Logic))
            {
                throw new ArgumentException("Invalid or unsupported search criterion.");
            }

            var expression = string.IsNullOrEmpty(filter.Field) && filter.Filters?.Count > 0
                ? BuildGroup(parameter, filter.Filters, depth + 1)
                : BuildCriterion(parameter, filter, depth);
            alternatives = alternatives is null ? expression : Expression.OrElse(alternatives, expression);
            if (filter.Logic == FilterLogicOperator.And)
            {
                result = result is null ? alternatives : Expression.AndAlso(result, alternatives);
                alternatives = null;
            }
        }

        if (alternatives is not null)
        {
            result = result is null ? alternatives : Expression.AndAlso(result, alternatives);
        }

        return result ?? Expression.Constant(true);
    }

    private static Expression BuildCriterion(Expression parameter, FilterCriteria filter, int depth)
    {
        var property = Property(parameter, filter.Field);
        if (filter.Operator is FilterOperator.Any or FilterOperator.All or FilterOperator.None)
        {
            if (property.Type.GetInterfaces().Append(property.Type).Any(t => t.IsGenericType &&
                (t.GetGenericTypeDefinition() == typeof(IDictionary<,>) || t.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))))
            {
                throw new ArgumentException("Search does not support dictionary payloads.");
            }

            var elementType = property.Type.GetInterfaces().Append(property.Type)
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0]
                ?? throw new ArgumentException("Collection operator requires a collection field.");
            var element = Expression.Parameter(elementType, "child");
            var children = filter.Value is FilterCriteria child ? new List<FilterCriteria> { child } : filter.Filters;
            if (children?.Count is not > 0)
            {
                throw new ArgumentException("Collection operator requires child criteria.");
            }

            Expression call = Expression.Call(typeof(Enumerable), filter.Operator == FilterOperator.All ? "All" : "Any",
                [elementType], property, Expression.Lambda(BuildGroup(element, children, depth + 1), element));
            return Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, property.Type)),
                filter.Operator == FilterOperator.None ? Expression.Not(call) : call);
        }

        if (filter.Filters?.Count > 0)
        {
            throw new ArgumentException("Nested criteria require a group or collection operator.");
        }

        ValidateScalar(property.Type);
        var type = Nullable.GetUnderlyingType(property.Type) ?? property.Type;
        var unary = filter.Operator is FilterOperator.IsNull or FilterOperator.IsNotNull or FilterOperator.IsEmpty or FilterOperator.IsNotEmpty;
        var value = unary ? null : filter.Value is JsonElement json ? json.ValueKind == JsonValueKind.Null ? null : json.ToString() : filter.Value;
        var converted = ConvertValue(value, type);
        Expression constant = converted is null && property.Type.IsValueType && Nullable.GetUnderlyingType(property.Type) is null
            ? unary ? Expression.Default(property.Type) : throw new ArgumentException("Null cannot be compared to a required value field.")
            : Expression.Constant(converted, converted is null ? property.Type : type);
        if (constant.Type != property.Type)
        {
            constant = Expression.Convert(constant, property.Type);
        }

        var nullValue = !property.Type.IsValueType || Nullable.GetUnderlyingType(property.Type) is not null
            ? Expression.Constant(null, property.Type) : null;
        return filter.Operator switch
        {
            FilterOperator.Equal => Expression.Equal(property, constant),
            FilterOperator.NotEqual => Expression.NotEqual(property, constant),
            FilterOperator.IsNull => nullValue is null ? Expression.Constant(false) : Expression.Equal(property, nullValue),
            FilterOperator.IsNotNull => nullValue is null ? Expression.Constant(true) : Expression.NotEqual(property, nullValue),
            FilterOperator.GreaterThan => Compare(property, constant, FilterOperator.GreaterThan),
            FilterOperator.GreaterThanOrEqual => Compare(property, constant, FilterOperator.GreaterThanOrEqual),
            FilterOperator.LessThan => Compare(property, constant, FilterOperator.LessThan),
            FilterOperator.LessThanOrEqual => Compare(property, constant, FilterOperator.LessThanOrEqual),
            FilterOperator.IsEmpty when type == typeof(string) => Expression.OrElse(Expression.Equal(property, nullValue), Expression.Equal(property, Expression.Constant(string.Empty))),
            FilterOperator.IsNotEmpty when type == typeof(string) => Expression.AndAlso(Expression.NotEqual(property, nullValue), Expression.NotEqual(property, Expression.Constant(string.Empty))),
            FilterOperator.Contains or FilterOperator.DoesNotContain or FilterOperator.StartsWith or FilterOperator.DoesNotStartWith or FilterOperator.EndsWith or FilterOperator.DoesNotEndWith when type == typeof(string) && converted is string => StringMatch(property, constant, filter.Operator),
            _ => throw new ArgumentException($"Operator '{filter.Operator}' is not supported for '{filter.Field}'.")
        };
    }

    private static Expression GuidText(Expression value) => Expression.Call(
        Expression.Call(value, nameof(Guid.ToString), Type.EmptyTypes), nameof(string.ToLower), Type.EmptyTypes);

    private static Expression Compare(Expression left, Expression right, FilterOperator operation)
    {
        if (left.Type == typeof(Guid))
        {
            left = GuidText(left);
            right = GuidText(right);
        }

        if (left.Type == typeof(string))
        {
            left = Expression.Call(typeof(string), nameof(string.Compare), Type.EmptyTypes, left, right);
            right = Expression.Constant(0);
        }

        return operation switch
        {
            FilterOperator.GreaterThan => Expression.GreaterThan(left, right),
            FilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(left, right),
            FilterOperator.LessThan => Expression.LessThan(left, right),
            _ => Expression.LessThanOrEqual(left, right)
        };
    }

    private static object ConvertValue(object value, Type type)
    {
        try
        {
            return value is null ? null : type.IsInstanceOfType(value) ? value
            : type.IsEnum ? Enum.Parse(type, value.ToString(), true)
            : type == typeof(Guid) ? Guid.Parse(value.ToString())
            : type == typeof(DateTimeOffset) ? DateTimeOffset.Parse(value.ToString(), CultureInfo.InvariantCulture)
            : type == typeof(DateTime) ? DateTime.Parse(value.ToString(), CultureInfo.InvariantCulture)
            : type == typeof(DateOnly) ? DateOnly.Parse(value.ToString(), CultureInfo.InvariantCulture)
            : type == typeof(TimeOnly) ? TimeOnly.Parse(value.ToString(), CultureInfo.InvariantCulture)
            : type == typeof(TimeSpan) ? TimeSpan.Parse(value.ToString(), CultureInfo.InvariantCulture)
            : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new ArgumentException($"Invalid search value for '{type.Name}'.", exception);
        }
    }

    private static Expression StringMatch(Expression property, Expression value, FilterOperator operation)
    {
        var method = operation is FilterOperator.Contains or FilterOperator.DoesNotContain ? "Contains"
            : operation is FilterOperator.StartsWith or FilterOperator.DoesNotStartWith ? "StartsWith" : "EndsWith";
        Expression call = Expression.Call(property, method, [], value);
        if (operation is FilterOperator.DoesNotContain or FilterOperator.DoesNotStartWith or FilterOperator.DoesNotEndWith)
        {
            call = Expression.Not(call);
        }

        return Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, typeof(string))), call);
    }

    private static MemberExpression Property(Expression parameter, string field)
    {
        if (string.IsNullOrWhiteSpace(field) || field.Contains('.'))
        {
            throw new ArgumentException("Search fields must be direct public properties.");
        }

        var property = parameter.Type.GetProperty(field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (property?.GetMethod is null || property.GetIndexParameters().Length != 0)
        {
            throw new ArgumentException($"Unknown search field '{field}'.");
        }

        return Expression.Property(parameter, property);
    }

    private static void ValidateScalar(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!(type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan)))
        {
            throw new ArgumentException("Search comparison and ordering require scalar fields.");
        }
    }
}
