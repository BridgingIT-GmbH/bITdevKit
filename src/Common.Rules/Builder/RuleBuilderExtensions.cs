// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq.Expressions;

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

    public static RulesBuilder Add(this RulesBuilder builder, Func<bool> predicate, string message = null)
    {
        return builder.Add(new FuncRule(predicate, message));
    }

    // Overload for expression-based messages
    public static RulesBuilder Add(this RulesBuilder builder, Func<bool> predicate, Expression<Func<bool>> expression)
    {
        var message = expression.ToString();
        return builder.Add(new FuncRule(predicate, message));
    }

    public static RulesBuilder Add(this RulesBuilder builder, Func<CancellationToken, Task<bool>> predicate, string message = null)
    {
        return builder.Add(new AsyncFuncRule(predicate, message));
    }

    public static RulesBuilder Add<T>(this RulesBuilder builder, Func<T, IRule> ruleFactory)
    {
        return builder.Add(new ItemRule<T>(ruleFactory));
    }

    public static RulesBuilder And<T>(this RulesBuilder builder, Func<T, IRule> ruleFactory)
    {
        return builder.Add(ruleFactory);
    }
}

public class ItemRule<T>(Func<T, IRule> ruleFactory) : RuleBase
{
    private T currentItem;

    internal void SetItem(T item)
    {
        this.currentItem = item;
    }

    protected override Result ExecuteRule() =>
        ruleFactory(this.currentItem).Apply();

    public override Task<Result> ApplyAsync(CancellationToken cancellationToken = default) =>
        ruleFactory(this.currentItem).ApplyAsync(cancellationToken);
}