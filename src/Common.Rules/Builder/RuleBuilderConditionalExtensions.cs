// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides extension methods for conditional rule building.
/// </summary>
public static class RuleBuilderConditionalExtensions
{
    /// <summary>
    /// Adds a rule with a sync condition using a lambda expression.
    /// </summary>
    public static RulesBuilder When(
        this RulesBuilder builder,
        Func<bool> condition,
        IRule rule)
    {
        return builder.Add(new ConditionalRule(condition, rule));
    }

    /// <summary>
    /// Adds multiple rules with a sync condition using a lambda expression.
    /// </summary>
    public static RulesBuilder When(
        this RulesBuilder builder,
        Func<bool> condition,
        params IRule[] rules)
    {
        foreach (var rule in rules)
        {
            builder.Add(new ConditionalRule(condition, rule));
        }
        return builder;
    }

    /// <summary>
    /// Adds rules with a sync condition using a builder action.
    /// </summary>
    public static RulesBuilder When(
        this RulesBuilder builder,
        Func<bool> condition,
        Action<RulesBuilder> addRules)
    {
        if (condition())
        {
            addRules(builder);
        }
        return builder;
    }

    /// <summary>
    /// Adds a rule with an async condition using a lambda expression.
    /// </summary>
    public static RulesBuilder WhenAsync(
        this RulesBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        IRule rule)
    {
        return builder.Add(new AsyncConditionalRule(condition, rule));
    }

    /// <summary>
    /// Adds multiple rules with an async condition using a lambda expression.
    /// </summary>
    public static RulesBuilder WhenAsync(
        this RulesBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        params IRule[] rules)
    {
        foreach (var rule in rules)
        {
            builder.Add(new AsyncConditionalRule(condition, rule));
        }
        return builder;
    }

    /// <summary>
    /// Adds a rule when any of the conditions are true.
    /// </summary>
    public static RulesBuilder WhenAny(
        this RulesBuilder builder,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.Any(c => c()), rule);
    }

    /// <summary>
    /// Adds rules when any of the conditions are true.
    /// </summary>
    public static RulesBuilder WhenAny(
        this RulesBuilder builder,
        IEnumerable<Func<bool>> conditions,
        Action<RulesBuilder> addRules)
    {
        return builder.When(() => conditions.Any(c => c()), addRules);
    }

    /// <summary>
    /// Adds a rule when all conditions are true.
    /// </summary>
    public static RulesBuilder WhenAll(
        this RulesBuilder builder,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.All(c => c()), rule);
    }

    /// <summary>
    /// Adds a rule when none of the conditions are true.
    /// </summary>
    public static RulesBuilder WhenNone(
        this RulesBuilder builder,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => !conditions.Any(c => c()), rule);
    }

    /// <summary>
    /// Adds a rule when exactly the specified number of conditions are true.
    /// </summary>
    public static RulesBuilder WhenExactly(
        this RulesBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.Count(c => c()) == count, rule);
    }

    /// <summary>
    /// Adds a rule when at least the specified number of conditions are true.
    /// </summary>
    public static RulesBuilder WhenAtLeast(
        this RulesBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.Count(c => c()) >= count, rule);
    }

    /// <summary>
    /// Adds a rule when at most the specified number of conditions are true.
    /// </summary>
    public static RulesBuilder WhenAtMost(
        this RulesBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.Count(c => c()) <= count, rule);
    }

    /// <summary>
    /// Adds a rule when the number of true conditions falls within the specified range.
    /// </summary>
    public static RulesBuilder WhenBetween(
        this RulesBuilder builder,
        int min,
        int max,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() =>
        {
            var trueCount = conditions.Count(c => c());
            return trueCount >= min && trueCount <= max;
        }, rule);
    }
}