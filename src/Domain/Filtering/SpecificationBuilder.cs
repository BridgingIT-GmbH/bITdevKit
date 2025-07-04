// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Globalization;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// A static utility class for building collections of entity specifications based on filtering criteria.
/// </summary>
public static class SpecificationBuilder
{
    /// <summary>
    /// Builds a collection of <see cref="ISpecification{TEntity}"/> based on the provided <see cref="FilterModel"/>.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="filterModel">The filter model that provides the criteria for building specifications.</param>
    /// <param name="specifications">An optional collection of existing specifications to be included.</param>
    /// <returns>A collection of <see cref="ISpecification{TEntity}"/> that meet the criteria defined in the filter model.</returns>
    public static IEnumerable<ISpecification<TEntity>> Build<TEntity>(FilterModel filterModel, IEnumerable<ISpecification<TEntity>> specifications = null)
        where TEntity : class, IEntity
    {
        return Build(filterModel?.Filters, specifications);
    }

    /// <summary>
    /// Builds a collection of <see cref="ISpecification{TEntity}"/> based on the provided filters and existing specifications.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="filters">The filters to be applied.</param>
    /// <param name="specifications">An optional set of existing specifications to be included.</param>
    /// <returns>A collection of <see cref="ISpecification{TEntity}"/> that apply the given filters and existing specifications.</returns>
    public static IEnumerable<ISpecification<TEntity>> Build<TEntity>(IEnumerable<FilterCriteria> filters, IEnumerable<ISpecification<TEntity>> specifications = null)
        where TEntity : class, IEntity
    {
        if (filters == null) //  || !filters.Any()
        {
            return [];
        }

        var result = new List<ISpecification<TEntity>>();
        ISpecification<TEntity> currentSpeciification = null;

        for (var i = 0; i < filters.Count(); i++)
        {
            var filter = filters.ElementAt(i);
            var isLastFilter = i == filters.Count() - 1;
            var specification = BuildSpecification<TEntity>(filter);

            if (currentSpeciification == null)
            {
                currentSpeciification = specification;
            }
            else
            {
                currentSpeciification = currentSpeciification.Or(specification);
            }

            if (isLastFilter || filter.Logic == FilterLogicOperator.And)
            {
                result.Add(currentSpeciification);
                currentSpeciification = null;
            }
        }

        if (specifications != null)
        {
            result.AddRange(specifications);
        }

        return result;
    }

    private static ISpecification<TEntity> BuildSpecification<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (filter == null)
        {
            throw new ArgumentException("Filter criteria is required.");
        }

        return filter.CustomType switch
        {
            FilterCustomType.NamedSpecification => SpecificationResolver.Resolve<TEntity>(filter.SpecificationName, filter.SpecificationArguments),
            FilterCustomType.CompositeSpecification => BuildComposite<TEntity>(filter.CompositeSpecification),
            FilterCustomType.None => new Specification<TEntity>(BuildExpression<TEntity>(filter)),
            _ => CustomSpecificationBuilder.Build<TEntity>(filter)
        };
    }

    private static Expression<Func<TEntity, bool>> BuildExpression<TEntity>(FilterCriteria filter)
        where TEntity : class, IEntity
    {
        if (filter == null)
        {
            throw new ArgumentException("Filter criteria is required.");
        }

        if (filter.Field.IsNullOrEmpty())
        {
            throw new ArgumentException("Field is required for filter criteria.");
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");

        var body = filter.Operator switch
        {
            FilterOperator.Any => BuildAnyExpression(parameter, filter.Field, filter.Value as FilterCriteria ?? filter.Filters?.FirstOrDefault()),
            FilterOperator.All => BuildAllExpression(parameter, filter.Field, filter.Value as FilterCriteria ?? filter.Filters?.FirstOrDefault()),
            FilterOperator.None => BuildNoneExpression(parameter, filter.Field, filter.Value as FilterCriteria ?? filter.Filters?.FirstOrDefault()),
            _ => BuildSimpleExpression(parameter, filter)
        };

        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static Expression BuildSimpleExpression(ParameterExpression parameter, FilterCriteria filter)
    {
        var property = BuildPropertyExpression(parameter, filter.Field);
        var value = ConvertValueType(filter.Value, property.Type);
        var constant = Expression.Constant(value);
        var underlyingType = Nullable.GetUnderlyingType(property.Type) ?? property.Type;

        // Handle comparisons for Enumeration types (both nullable and non-nullable)
        if (typeof(Enumeration).IsAssignableFrom(underlyingType) &&
            IsComparisonOperator(filter.Operator))
        {
            // For nullable types, we need to handle the null case
            if (property.Type.IsNullableType())
            {
                var propertyId = Expression.Property(Expression.Property(property, "Value"), nameof(Enumeration.Id));
                var valueId = Expression.Constant(value != null ? ((Enumeration)value).Id : 0);

                var hasValueProperty = Expression.Property(property, "HasValue");
                var nullCheckExpression = Expression.NotEqual(property, Expression.Constant(null, property.Type));

                var comparisonExpression = filter.Operator switch
                {
                    FilterOperator.GreaterThan => Expression.GreaterThan(propertyId, valueId),
                    FilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(propertyId, valueId),
                    FilterOperator.LessThan => Expression.LessThan(propertyId, valueId),
                    FilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(propertyId, valueId),
                    _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported.")
                };

                return Expression.AndAlso(nullCheckExpression, comparisonExpression);
            }

            // Non-nullable Enumeration handling
            var nonNullablePropertyId = Expression.Property(property, nameof(Enumeration.Id));
            var nonNullableValueId = Expression.Constant(((Enumeration)value).Id);

            return filter.Operator switch
            {
                FilterOperator.GreaterThan => Expression.GreaterThan(nonNullablePropertyId, nonNullableValueId),
                FilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(nonNullablePropertyId, nonNullableValueId),
                FilterOperator.LessThan => Expression.LessThan(nonNullablePropertyId, nonNullableValueId),
                FilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(nonNullablePropertyId, nonNullableValueId),
                _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported.")
            };
        }

        // For nullable properties, we need to ensure we're comparing the value part
        if (property.Type.IsNullableType())
        {
            return filter.Operator switch
            {
                FilterOperator.Equal => Expression.Equal(property, constant),
                FilterOperator.NotEqual => Expression.NotEqual(property, constant),
                FilterOperator.IsNull => Expression.Equal(property, Expression.Constant(null)),
                FilterOperator.IsNotNull => Expression.NotEqual(property, Expression.Constant(null)),
                FilterOperator.IsEmpty => Expression.OrElse(
                    Expression.Equal(property, Expression.Constant(null, property.Type)),
                    Expression.Equal(property, Expression.Constant(string.Empty, property.Type))),
                FilterOperator.IsNotEmpty => Expression.AndAlso(
                    Expression.NotEqual(property, Expression.Constant(null, property.Type)),
                    Expression.NotEqual(property, Expression.Constant(string.Empty, property.Type))),
                FilterOperator.GreaterThan => Expression.GreaterThan(
                    Expression.Property(property, "Value"),
                    Expression.Convert(constant, property.Type.GetGenericArguments()[0])),
                FilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(Expression.Property(property, "Value"),
                    Expression.Convert(constant, property.Type.GetGenericArguments()[0])),
                FilterOperator.LessThan => Expression.LessThan(
                    Expression.Property(property, "Value"),
                    Expression.Convert(constant, property.Type.GetGenericArguments()[0])),
                FilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(
                    Expression.Property(property, "Value"),
                    Expression.Convert(constant, property.Type.GetGenericArguments()[0])),
                FilterOperator.Contains => Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)]), constant),
                FilterOperator.DoesNotContain => Expression.Not(Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)]), constant)),
                FilterOperator.StartsWith => Expression.Call(property, typeof(string).GetMethod("StartsWith", [typeof(string)]), constant),
                FilterOperator.DoesNotStartWith => Expression.Not(Expression.Call(property, typeof(string).GetMethod("StartsWith", [typeof(string)]), constant)),
                FilterOperator.EndsWith => Expression.Call(property, typeof(string).GetMethod("EndsWith", [typeof(string)]), constant),
                FilterOperator.DoesNotEndWith => Expression.Not(Expression.Call(property, typeof(string).GetMethod("EndsWith", [typeof(string)]), constant)),
                _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported.")
            };
        }

        // For non-nullable properties, use the original logic
        return filter.Operator switch
        {
            FilterOperator.Equal => Expression.Equal(property, constant),
            FilterOperator.NotEqual => Expression.NotEqual(property, constant),
            FilterOperator.IsNull => Expression.Equal(property, Expression.Constant(null)),
            FilterOperator.IsNotNull => Expression.NotEqual(property, Expression.Constant(null)),
            FilterOperator.IsEmpty => Expression.OrElse(Expression.Equal(property, Expression.Constant(null)), Expression.Equal(property, Expression.Constant(string.Empty))),
            FilterOperator.IsNotEmpty => Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null)), Expression.NotEqual(property, Expression.Constant(string.Empty))),
            FilterOperator.GreaterThan => Expression.GreaterThan(property, constant),
            FilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, constant),
            FilterOperator.LessThan => Expression.LessThan(property, constant),
            FilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(property, constant),
            FilterOperator.Contains => Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)]), constant),
            FilterOperator.DoesNotContain => Expression.Not(Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)]), constant)),
            FilterOperator.StartsWith => Expression.Call(property, typeof(string).GetMethod("StartsWith", [typeof(string)]), constant),
            FilterOperator.DoesNotStartWith => Expression.Not(Expression.Call(property, typeof(string).GetMethod("StartsWith", [typeof(string)]), constant)),
            FilterOperator.EndsWith => Expression.Call(property, typeof(string).GetMethod("EndsWith", [typeof(string)]), constant),
            FilterOperator.DoesNotEndWith => Expression.Not(Expression.Call(property, typeof(string).GetMethod("EndsWith", [typeof(string)]), constant)),
            _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported.")
        };
    }

    private static bool IsComparisonOperator(FilterOperator @operator)
    {
        return @operator is FilterOperator.GreaterThan
            or FilterOperator.GreaterThanOrEqual
            or FilterOperator.LessThan
            or FilterOperator.LessThanOrEqual;
    }

    private static Expression BuildPropertyExpression(ParameterExpression parameter, string field)
    {
        return field.Split('.').Aggregate((Expression)parameter, Expression.Property); // WARN: throws when propertyname not correct (ArgumentException)
    }

    private static Expression BuildAnyExpression(ParameterExpression parameter, string fieldPath, FilterCriteria innerFilter)
    {
        var collection = BuildPropertyExpression(parameter, fieldPath);
        var elementType = collection.Type.GetGenericArguments()[0];
        var elementParameter = Expression.Parameter(elementType, "element");

        var innerBody = BuildExpression(elementType, innerFilter, elementParameter);
        var lambda = Expression.Lambda(innerBody, elementParameter);

        return Expression.Call(typeof(Enumerable), "Any", [elementType], collection, lambda);
    }

    private static Expression BuildAllExpression(ParameterExpression parameter, string fieldPath, FilterCriteria innerFilter)
    {
        var collection = BuildPropertyExpression(parameter, fieldPath);
        var elementType = collection.Type.GetGenericArguments()[0];
        var elementParameter = Expression.Parameter(elementType, "element");

        var innerBody = BuildExpression(elementType, innerFilter, elementParameter);
        var lambda = Expression.Lambda(innerBody, elementParameter);

        return Expression.Call(typeof(Enumerable), "All", [elementType], collection, lambda);
    }

    private static Expression BuildNoneExpression(ParameterExpression parameter, string fieldPath, FilterCriteria innerFilter)
    {
        var anyExpression = BuildAnyExpression(parameter, fieldPath, innerFilter);

        return Expression.Not(anyExpression);
    }

    private static Expression BuildExpression(Type entityType, FilterCriteria filter, ParameterExpression parameter)
    {
        var property = BuildPropertyExpression(parameter, filter.Field);
        var value = ConvertValueType(filter.Value, property.Type);
        var constant = Expression.Constant(value, property.Type);

        if (property.Type.IsNullableType())
        {
            var underlyingType = property.Type.GetGenericArguments()[0];
            var propertyValue = Expression.Property(property, "Value");
            var convertedConstant = Expression.Convert(constant, underlyingType);
            var notNullCheck = Expression.NotEqual(property, Expression.Constant(null, property.Type));

            return filter.Operator switch
            {
                FilterOperator.Equal => Expression.Equal(property, constant),
                FilterOperator.NotEqual => Expression.NotEqual(property, constant),
                FilterOperator.IsNull => Expression.Equal(property, Expression.Constant(null)),
                FilterOperator.IsNotNull => Expression.NotEqual(property, Expression.Constant(null)),
                FilterOperator.IsEmpty => Expression.OrElse(
                    Expression.Equal(property, Expression.Constant(null, property.Type)),
                    Expression.Equal(property, Expression.Constant(string.Empty, property.Type))),
                FilterOperator.IsNotEmpty => Expression.AndAlso(
                    Expression.NotEqual(property, Expression.Constant(null, property.Type)),
                    Expression.NotEqual(property, Expression.Constant(string.Empty, property.Type))),

                FilterOperator.GreaterThan => Expression.AndAlso(notNullCheck, Expression.GreaterThan(propertyValue, convertedConstant)),
                FilterOperator.GreaterThanOrEqual => Expression.AndAlso(notNullCheck, Expression.GreaterThanOrEqual(propertyValue, convertedConstant)),
                FilterOperator.LessThan => Expression.AndAlso(notNullCheck, Expression.LessThan(propertyValue, convertedConstant)),
                FilterOperator.LessThanOrEqual => Expression.AndAlso(notNullCheck, Expression.LessThanOrEqual(propertyValue, convertedConstant)),
                FilterOperator.Contains => Expression
                    .AndAlso(notNullCheck, Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)]), constant)),
                FilterOperator.DoesNotContain => Expression
                    .AndAlso(notNullCheck, Expression.Not(Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)]), constant))),
                FilterOperator.StartsWith => Expression
                    .AndAlso(notNullCheck, Expression.Call(property, typeof(string).GetMethod("StartsWith", [typeof(string)]), constant)),
                FilterOperator.DoesNotStartWith => Expression
                    .AndAlso(notNullCheck, Expression.Not(Expression.Call(property, typeof(string).GetMethod("StartsWith", [typeof(string)]), constant))),
                FilterOperator.EndsWith => Expression
                    .AndAlso(notNullCheck, Expression.Call(property, typeof(string).GetMethod("EndsWith", [typeof(string)]), constant)),
                FilterOperator.DoesNotEndWith => Expression
                    .AndAlso(notNullCheck, Expression.Not(Expression.Call(property, typeof(string).GetMethod("EndsWith", [typeof(string)]), constant))),
                _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported.")
            };
        }

        return filter.Operator switch
        {
            FilterOperator.Equal => Expression.Equal(property, constant),
            FilterOperator.NotEqual => Expression.NotEqual(property, constant),
            FilterOperator.IsNull => Expression.Equal(property, Expression.Constant(null)),
            FilterOperator.IsNotNull => Expression.NotEqual(property, Expression.Constant(null)),
            FilterOperator.IsEmpty => Expression.OrElse(
                Expression.Equal(property, Expression.Constant(null)),
                Expression.Equal(property, Expression.Constant(string.Empty))),
            FilterOperator.IsNotEmpty => Expression.AndAlso(
                Expression.NotEqual(property, Expression.Constant(null)),
                Expression.NotEqual(property, Expression.Constant(string.Empty))),
            FilterOperator.GreaterThan => Expression.GreaterThan(property, constant),
            FilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, constant),
            FilterOperator.LessThan => Expression.LessThan(property, constant),
            FilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(property, constant),
            FilterOperator.Contains => Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)]), constant),
            FilterOperator.DoesNotContain => Expression.Not(Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)]), constant)),
            FilterOperator.StartsWith => Expression.Call(property, typeof(string).GetMethod("StartsWith", [typeof(string)]), constant),
            FilterOperator.DoesNotStartWith => Expression.Not(Expression.Call(property, typeof(string).GetMethod("StartsWith", [typeof(string)]), constant)),
            FilterOperator.EndsWith => Expression.Call(property, typeof(string).GetMethod("EndsWith", [typeof(string)]), constant),
            FilterOperator.DoesNotEndWith => Expression.Not(Expression.Call(property, typeof(string).GetMethod("EndsWith", [typeof(string)]), constant)),
            _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported.")
        };
    }

    private static ISpecification<TEntity> BuildComposite<TEntity>(CompositeSpecification compositeSpec)
        where TEntity : class, IEntity
    {
        return BuildFromNodes<TEntity>(compositeSpec.Nodes);
    }

    private static ISpecification<TEntity> BuildFromNodes<TEntity>(List<SpecificationNode> nodes)
        where TEntity : class, IEntity
    {
        if (nodes.Count == 1)
        {
            return BuildFromNode<TEntity>(nodes[0]);
        }

        var specs = nodes.Select(BuildFromNode<TEntity>).ToList();

        return specs.Aggregate((current, next) => current.And(next));
    }

    private static ISpecification<TEntity> BuildFromNode<TEntity>(SpecificationNode node)
        where TEntity : class, IEntity
    {
        if (node is SpecificationLeaf leaf)
        {
            return SpecificationResolver.Resolve<TEntity>(leaf.Name, leaf.Arguments);
        }
        else if (node is SpecificationGroup group)
        {
            var specs = group.Nodes.Select(n => BuildFromNode<TEntity>(n)).ToList();

            return group.Logic switch
            {
                FilterLogicOperator.Or => specs.Aggregate((current, next) => current.Or(next)),
                FilterLogicOperator.And => specs.Aggregate((current, next) => current.And(next)),
                _ => throw new ArgumentException($"Unsupported logical operator: {group.Logic}")
            };
        }

        throw new ArgumentException($"Unknown node type: {node.GetType().Name}");
    }

    private static object ConvertValueType(object value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        var nullableTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => ConvertStringValue(jsonElement.GetString()),
                JsonValueKind.Number when nullableTargetType == typeof(int) => jsonElement.GetInt32(),
                JsonValueKind.Number when nullableTargetType == typeof(long) => jsonElement.GetInt64(),
                JsonValueKind.Number when nullableTargetType == typeof(double) => jsonElement.GetDouble(),
                JsonValueKind.Number when nullableTargetType == typeof(decimal) => jsonElement.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => jsonElement.EnumerateArray().ToList(),
                JsonValueKind.Object => jsonElement.Deserialize<Dictionary<string, object>>(),
                _ => throw new ArgumentException($"Unsupported JsonValueKind: {jsonElement.ValueKind}")
            };
        }

        return value is string stringValue
            ? ConvertStringValue(stringValue)
            : TypeConverter.ChangeType(value, nullableTargetType); // Convert.ChangeType(value, nullableTargetType);

        object ConvertStringValue(string stringValue) => nullableTargetType switch
        {
            not null when nullableTargetType == typeof(DateTimeOffset) => DateTimeOffset.Parse(stringValue, CultureInfo.InvariantCulture),
            not null when nullableTargetType == typeof(DateTime) => DateTime.Parse(stringValue, CultureInfo.InvariantCulture),
            not null when nullableTargetType == typeof(DateOnly) => DateOnly.Parse(stringValue, CultureInfo.InvariantCulture),
            not null when nullableTargetType == typeof(TimeOnly) => TimeOnly.Parse(stringValue, CultureInfo.InvariantCulture),
            not null when nullableTargetType == typeof(Guid) => Guid.Parse(stringValue),
            _ => TypeConverter.ChangeType(stringValue, nullableTargetType)
        };
    }
}

public static class TypeConverter
{
    public static object ChangeType(object value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        var sourceType = value.GetType();

        // Check if types are already compatible
        if (targetType.IsAssignableFrom(sourceType))
        {
            return value;
        }

        // Check for implicit conversion operator
        var implicitOperator = targetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
                m.Name == "op_Implicit" &&
                m.ReturnType == targetType &&
                m.GetParameters().FirstOrDefault()?.ParameterType == sourceType);

        if (implicitOperator != null)
        {
            return implicitOperator.Invoke(null, new[] { value });
        }

        // Fallback to standard conversion
        return Convert.ChangeType(value, targetType);
    }
}