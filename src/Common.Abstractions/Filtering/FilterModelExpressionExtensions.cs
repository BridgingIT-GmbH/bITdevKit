// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq.Expressions;

public static class FilterModelExpressionExtensions
{
    /// <summary>
    /// Gets the full property path from a MemberExpression.
    /// For example, for the expression (x => x.Details.IsGift), it returns "Details.IsGift".
    /// </summary>
    /// <param name="expression">The MemberExpression.</param>
    /// <returns>The full property path as a string.</returns>
    public static string GetFullPropertyName(this MemberExpression expression)
    {
        var parts = new Stack<string>();
        var currentExpression = expression;

        while (currentExpression != null)
        {
            parts.Push(currentExpression.Member.Name);
            currentExpression = currentExpression.Expression as MemberExpression;
        }

        return string.Join(".", parts);
    }

    /// <summary>
    /// Overload to handle lambda expressions directly.
    /// It extracts the MemberExpression from the lambda's body.
    /// </summary>
    public static string GetFullPropertyName<T, TProperty>(this Expression<Func<T, TProperty>> lambda)
    {
        // The body of the lambda might be a UnaryExpression (e.g., for boxing value types)
        // or the MemberExpression directly.
        if (lambda.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression memberExpression)
        {
            return GetFullPropertyName(memberExpression);
        }

        if (lambda.Body is MemberExpression memberExpr)
        {
            return GetFullPropertyName(memberExpr);
        }

        throw new ArgumentException("The lambda expression's body must be a MemberExpression.", nameof(lambda));
    }
}