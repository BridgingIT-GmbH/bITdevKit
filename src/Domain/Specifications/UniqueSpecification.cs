// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

using System;
using System.Linq.Expressions;

/// <summary>
/// Specification to check if a property value is unique across all entities.
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
public class UniqueSpecification<TEntity> : Specification<TEntity>
{
    /// <summary>
    /// Creates a specification that checks if a property value is unique.
    /// </summary>
    /// <param name="propertyExpression">Expression that selects the property to check (e.g., x => x.Name)</param>
    /// <param name="value">The value to check for uniqueness</param>
    public UniqueSpecification(Expression<Func<TEntity, object>> propertyExpression, object value)
        : base(BuildExpression(propertyExpression, value))
    {
    }

    private static Expression<Func<TEntity, bool>> BuildExpression(
        Expression<Func<TEntity, object>> propertyExpression,
        object value)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");

        // Handle the property expression
        var propertyAccess = RemoveConvert(propertyExpression.Body);
        var property = Expression.PropertyOrField(parameter, GetMemberName(propertyAccess));

        // Create equality comparison
        Expression comparison;
        if (value is string stringValue)
        {
            // For strings, use Equals method for better comparison
            var equalsMethod = typeof(string).GetMethod(nameof(string.Equals), [typeof(string)]);
            comparison = Expression.Call(property, equalsMethod!, Expression.Constant(stringValue));
        }
        else
        {
            comparison = Expression.Equal(property, Expression.Constant(value, property.Type));
        }

        return Expression.Lambda<Func<TEntity, bool>>(comparison, parameter);
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
