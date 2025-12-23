// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System;
using System.Linq.Expressions;

/// <summary>
/// Specification to check if a property value is unique, excluding a specific entity (useful for updates).
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TId">The entity identifier type</typeparam>
public class UniqueExceptSpecification<TEntity, TId> : Specification<TEntity>
    where TEntity : IEntity<TId>
{
    /// <summary>
    /// Creates a specification that checks if a property value is unique, excluding a specific entity.
    /// </summary>
    /// <param name="propertyExpression">Expression that selects the property to check (e.g., x => x.Name)</param>
    /// <param name="value">The value to check for uniqueness</param>
    /// <param name="excludeId">The ID of the entity to exclude from the check</param>
    public UniqueExceptSpecification(
        Expression<Func<TEntity, object>> propertyExpression,
        object value,
        TId excludeId)
        : base(BuildExpression(propertyExpression, value, excludeId))
    {
    }

    private static Expression<Func<TEntity, bool>> BuildExpression(
        Expression<Func<TEntity, object>> propertyExpression,
        object value,
        TId excludeId)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");

        // Get property access
        var propertyAccess = RemoveConvert(propertyExpression.Body);
        var property = Expression.PropertyOrField(parameter, GetMemberName(propertyAccess));

        // Create equality comparison for the property
        Expression propertyComparison;
        if (value is string stringValue)
        {
            var equalsMethod = typeof(string).GetMethod(nameof(string.Equals), [typeof(string)]);
            propertyComparison = Expression.Call(property, equalsMethod!, Expression.Constant(stringValue));
        }
        else
        {
            propertyComparison = Expression.Equal(property, Expression.Constant(value, property.Type));
        }

        // Create ID exclusion: x.Id != excludeId
        var idProperty = Expression.Property(parameter, nameof(IEntity<TId>.Id));
        var idNotEqual = Expression.NotEqual(idProperty, Expression.Constant(excludeId, typeof(TId)));

        // Combine: propertyMatches AND idNotEqual
        var combined = Expression.AndAlso(propertyComparison, idNotEqual);

        return Expression.Lambda<Func<TEntity, bool>>(combined, parameter);
    }

    private static Expression RemoveConvert(Expression expression)
    {
        while (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked)
        {
            expression = ((UnaryExpression)expression).Operand;
        }

        return expression;
    }

    private static string GetMemberName(Expression expression)
    {
        return expression switch
        {
            MemberExpression memberExpression => memberExpression.Member.Name,
            _ => throw new ArgumentException("Expression must be a property access", nameof(expression))
        };
    }
}
