// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides static methods to create and configure s set of rules.
/// </summary>
public static partial class Rule
{
    /// <summary>
    /// Creates an empty rule builder.
    /// </summary>
    /// <returns>A new empty rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var result = Rules.For()
    ///     .Add(RuleSet.IsNotEmpty(user.Name))
    ///     .Add(RuleSet.IsValidEmail(user.Email))
    ///     .When(user.IsEmployee, builder => builder
    ///         .Add(RuleSet.HasStringLength(user.EmployeeId, 5, 10)))
    ///     .Apply();
    ///
    /// if (result.IsSuccess)
    /// {
    ///     // Validation passed
    /// }
    /// </code>
    /// </example>
    public static RuleBuilder For()
    {
        return new RuleBuilder();
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rule.
    /// </summary>
    /// <param name="rule">The initial rule.</param>
    /// <returns>A new rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var result = Rules.For()
    ///     .Add(RuleSet.IsNotEmpty(user.Name))
    ///     .Add(RuleSet.IsValidEmail(user.Email))
    ///     .When(user.IsEmployee, builder => builder
    ///         .Add(RuleSet.HasStringLength(user.EmployeeId, 5, 10)))
    ///     .Apply();
    ///
    /// if (result.IsSuccess)
    /// {
    ///     // Validation passed
    /// }
    /// </code>
    /// </example>
    public static RuleBuilder For(IRule rule)
    {
        return new RuleBuilder().Add(rule);
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rules.
    /// </summary>
    /// <param name="rules">The initial rules.</param>
    /// <returns>A new rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var result = Rules.For()
    ///     .Add(RuleSet.IsNotEmpty(user.Name))
    ///     .Add(RuleSet.IsValidEmail(user.Email))
    ///     .When(user.IsEmployee, builder => builder
    ///         .Add(RuleSet.HasStringLength(user.EmployeeId, 5, 10)))
    ///     .Apply();
    ///
    /// if (result.IsSuccess)
    /// {
    ///     // Validation passed
    /// }
    /// </code>
    /// </example>
    public static RuleBuilder For(params IRule[] rules)

    {
        var builder = new RuleBuilder();
        foreach (var rule in rules)
        {
            builder.Add(rule);
        }

        return builder;
    }
}