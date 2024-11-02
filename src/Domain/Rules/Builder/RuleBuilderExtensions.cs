// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Provides extension methods for the rule builder.
/// </summary>
public static class RuleBuilderExtensions
{
    /// <summary>
    /// Adds an additional rule to the builder.
    /// </summary>
    /// <param name="builder">The rule builder.</param>
    /// <param name="rule">The rule to add.</param>
    /// <returns>The rule builder for method chaining.</returns>
    public static RulesBuilder And(this RulesBuilder builder, IRule rule)
    {
        return builder.Add(rule);
    }

    /// <summary>
    /// Adds multiple rules to the builder.
    /// </summary>
    /// <param name="builder">The rule builder.</param>
    /// <param name="rules">The rules to add.</param>
    /// <returns>The rule builder for method chaining.</returns>
    public static RulesBuilder And(this RulesBuilder builder, params IRule[] rules)
    {
        foreach (var rule in rules)
        {
            builder.Add(rule);
        }

        return builder;
    }

    /// <summary>
    /// Adds a rule to support collection initializer syntax.
    /// </summary>
    /// <param name="builder">The rule builder.</param>
    /// <param name="rule">The rule to add.</param>
    public static void Add(this RulesBuilder builder, IRule rule)
    {
        builder.Add(rule);
    }
}