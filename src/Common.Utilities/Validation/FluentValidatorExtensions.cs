// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

public static class FluentValidatorExtensions
{
    public static void AddRangeRule<T>(this AbstractValidator<T> validator, PropertyInfo prop, object minValue, object maxValue, string errorMessage = null)
    {
        if (!typeof(IComparable).IsAssignableFrom(prop.PropertyType))
        {
            return; // skip unsupported types
        }

        try
        {
            if (Convert.ChangeType(minValue, prop.PropertyType) is not IComparable min || Convert.ChangeType(maxValue, prop.PropertyType) is not IComparable max)
            {
                return; // skip if conversion fails
            }

            // Build expression: x => (TProperty)prop.Property
            var param = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(param, prop);
            var lambda = Expression.Lambda(propertyAccess, param);

            // Get RuleFor<T, TProperty>
            var ruleForMethod = typeof(DefaultValidatorExtensions)
                .GetMethods()
                .First(m => m.Name == "InclusiveBetween" && m.GetParameters().Length == 3);

            var genericMethod = ruleForMethod.MakeGenericMethod(typeof(T), prop.PropertyType);
            var ruleBuilder = validator.RuleFor((dynamic)lambda);

            // Call InclusiveBetween dynamically
            genericMethod.Invoke(null, [ruleBuilder, min, max]);

            ruleBuilder.WithMessage(
                errorMessage ?? $"{prop.Name} must be between {minValue} and {maxValue}."
            );
        }
        catch
        {
            // Skip if conversion fails (e.g., mismatched types)
        }
    }
}