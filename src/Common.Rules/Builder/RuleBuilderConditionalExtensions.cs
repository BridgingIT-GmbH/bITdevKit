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
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(() => product.IsDigital, Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(
        this RuleBuilder builder,
        Func<bool> condition,
        IRule rule)
    {
        return builder.Add(new ConditionalRule(condition, rule));
    }

    /// <summary>
    /// Adds multiple rules with a sync condition using a lambda expression.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rules">The rules to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(() => product.IsDigital, Rules.IsNotEmpty(product.DownloadUrl), Rules.StringLength(product.DownloadUrl, 5, 100))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(
        this RuleBuilder builder,
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
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(() => !product.IsDigital, builder => builder
    ///         .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///         .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(
        this RuleBuilder builder,
        Func<bool> condition,
        Action<RuleBuilder> addRules)
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
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAsync(async (token) => await product.IsAvailableAsync(token), Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        IRule rule)
    {
        return builder.Add(new AsyncConditionalRule(condition, rule));
    }

    /// <summary>
    /// Adds multiple rules with an async condition using a lambda expression.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rules">The rules to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAsync(async (token) => await product.IsAvailableAsync(token),
    ///         Rules.IsNotEmpty(product.DownloadUrl),
    ///         Rules.StringLength(product.DownloadUrl, 5, 100))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(
        this RuleBuilder builder,
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
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAny(new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(
        this RuleBuilder builder,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.Any(c => c()), rule);
    }

    /// <summary>
    /// Adds rules when any of the conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAny(new[] { () => product.IsDigital, () => product.HasSubscription }, builder => builder
    ///         .Add(Rules.IsNotEmpty(product.DownloadUrl))
    ///         .Add(Rules.StringLength(product.DownloadUrl, 5, 100)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(
        this RuleBuilder builder,
        IEnumerable<Func<bool>> conditions,
        Action<RuleBuilder> addRules)
    {
        return builder.When(() => conditions.Any(c => c()), addRules);
    }

    /// <summary>
    /// Adds a rule when all conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAll(new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(
        this RuleBuilder builder,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.All(c => c()), rule);
    }

    /// <summary>
    /// Adds a rule when none of the conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if none of the conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenNone(new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.GreaterThan(product.Price, 10m))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(
        this RuleBuilder builder,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => !conditions.Any(c => c()), rule);
    }

    /// <summary>
    /// Adds a rule when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the exact count of conditions is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenExactly(2, new[] { () => product.HasSubscription, () => product.IsDigital, () => product.IsOnSale }, Rules.ApplySpecialDiscount())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.Count(c => c()) == count, rule);
    }

    /// <summary>
    /// Adds a rule when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if at least the specified count of conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAtLeast(1, new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.SendNotification())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.Count(c => c()) >= count, rule);
    }

    /// <summary>
    /// Adds a rule when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if at most the specified count of conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenAtMost(1, new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.ApplyStandardPrice())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        IRule rule)
    {
        return builder.When(() => conditions.Count(c => c()) <= count, rule);
    }

    /// <summary>
    /// Adds a rule when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the true condition count is within the range.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .WhenBetween(1, 2, new[] { () => product.IsDigital, () => product.HasSubscription, () => product.IsOnSale }, Rules.ApplyFlexibleDiscount())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(
        this RuleBuilder builder,
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

    /// <summary>
    /// Adds a rule when the specified condition is true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(product.IsDigital, Rules.ApplyDigitalRules())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(this RuleBuilder builder, bool condition, IRule rule)
    {
        if (condition)
        {
            builder.Add(rule);
        }
        return builder;
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(!product.IsDigital, builder => builder
    ///         .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///         .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(this RuleBuilder builder, bool condition, Action<RuleBuilder> addRules)
    {
        if (condition)
        {
            addRules(builder);
        }
        return builder;
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when the specified condition is true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the condition is true.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(product.HasDiscount, () => product.Price > 0, "Price must be greater than 0 when discounted.")
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(this RuleBuilder builder, bool condition, Func<bool> predicate, string message = null)
    {
        if (condition)
        {
            builder.Add(new FuncRule(predicate, message));
        }
        return builder;
    }

    /// <summary>
    /// Adds multiple rules defined by boolean predicates when the specified condition is true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="predicates">The boolean predicates to evaluate if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .When(product.HasSubscription,
    ///         () => product.SubscriptionLevel > 0,
    ///         () => product.SubscriptionValid)
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(this RuleBuilder builder, bool condition, params Func<bool>[] predicates)
    {
        if (condition)
        {
            foreach (var predicate in predicates)
            {
                builder.Add(new FuncRule(predicate));
            }
        }
        return builder;
    }

    /// <summary>
    /// Adds a rule when the specified condition is false.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .Unless(product.IsDigital, Rules.GreaterThan(product.Price, 10m))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(this RuleBuilder builder, bool condition, IRule rule)
    {
        if (!condition)
        {
            builder.Add(rule);
        }
        return builder;
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is false.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .Unless(product.IsDigital, builder => builder
    ///         .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///         .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(this RuleBuilder builder, bool condition, Action<RuleBuilder> addRules)
    {
        if (!condition)
        {
            addRules(builder);
        }
        return builder;
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when the specified condition is false.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the condition is false.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .Unless(user.IsActive, () => user.IsVerified, "User must be verified if not active.")
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(this RuleBuilder builder, bool condition, Func<bool> predicate, string message = null)
    {
        if (!condition)
        {
            builder.Add(new FuncRule(predicate, message));
        }
        return builder;
    }

    /// <summary>
    /// Adds multiple rules defined by boolean predicates when the specified condition is false.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="predicates">The boolean predicates to evaluate if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule.For()
    ///     .Unless(product.IsOnSale,
    ///         () => product.Discount >= 0.1m,
    ///         () => product.Discount == 0.5m)
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(this RuleBuilder builder, bool condition, params Func<bool>[] predicates)
    {
        if (!condition)
        {
            foreach (var expr in predicates)
            {
                builder.Add(new FuncRule(expr));
            }
        }
        return builder;
    }
}