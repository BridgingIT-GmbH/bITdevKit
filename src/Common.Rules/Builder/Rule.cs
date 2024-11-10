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
    /// Provides a fluent interface for building and executing collections of rules.
    /// </summary>
    /// <example>
    /// <code>
    ///     return Rule
    ///         // Basic validation using ValueRules
    ///         .Add(Rules.IsNotEmpty(product.Name))
    ///         .Add(Rules.StringLength(product.Name, 3, 100))
    ///         .Add(Rules.NumericRange(product.Price, 0.01m, 999.99m))
    ///
    ///         // Conditional validation with When
    ///         .When(!product.IsDigital, builder => builder
    ///             .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///             .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
    ///
    ///         // Using Unless (inverse of When)
    ///         .Unless(product.IsDigital,
    ///             Rules.GreaterThan(product.Price, 10m))
    ///
    ///         // Combining multiple conditions
    ///         .WhenAll(new[]
    ///         {
    ///             product.Price > 100,
    ///             product.IsDigital,
    ///             product.Categories?.Count > 2
    ///         }, Rules.IsNotEmpty(product.ShippingAddress))
    ///         .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder Add()
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
    /// var result = Rules
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
    public static RuleBuilder Add(IRule rule)
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
    /// var result = Rules
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
    public static RuleBuilder Add(params IRule[] rules)

    {
        var builder = new RuleBuilder();
        foreach (var rule in rules)
        {
            builder.Add(rule);
        }

        return builder;
    }

    /// <summary>
    /// Adds a rule with a sync condition using a lambda expression.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.When(() => product.IsDigital, Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(Func<bool> condition, IRule rule)
    {
        return RuleBuilderConditionalExtensions.When(Add(), condition, rule);
    }

    /// <summary>
    /// Adds multiple rules with a sync condition using a lambda expression.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rules">The rules to add if the condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.When(() => product.IsDigital,
    ///     Rules.IsNotEmpty(product.DownloadUrl),
    ///     Rules.StringLength(product.DownloadUrl, 5, 100))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(Func<bool> condition, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.When(Add(), condition, rules);
    }

    /// <summary>
    /// Adds rules with a sync condition using a builder action.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.When(() => !product.IsDigital, builder => builder
    ///     .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///     .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(Func<bool> condition, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.When(Add(), condition, addRules);
    }

    /// <summary>
    /// Adds a rule when the specified condition is true.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.When(product.IsDigital, Rules.ApplyDigitalRules())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(bool condition, IRule rule)
    {
        return RuleBuilderConditionalExtensions.When(Add(), condition, rule);
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is true.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.When(!product.IsDigital, builder => builder
    ///     .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///     .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(bool condition, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.When(Add(), condition, addRules);
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when the specified condition is true.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the condition is true.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.When(product.HasDiscount, () => product.Price > 0, "Price must be greater than 0 when discounted.")
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(bool condition, Func<bool> predicate, string message = null)
    {
        return RuleBuilderConditionalExtensions.When(Add(), condition, predicate, message);
    }

    /// <summary>
    /// Adds multiple rules defined by boolean predicates when the specified condition is true.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="predicates">The boolean predicates to evaluate if the condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.When(product.HasSubscription,
    ///     () => product.SubscriptionLevel > 0,
    ///     () => product.SubscriptionValid)
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(bool condition, params Func<bool>[] predicates)
    {
        return RuleBuilderConditionalExtensions.When(Add(), condition, predicates);
    }

    /// <summary>
    /// Adds multiple rules with an async condition using a lambda expression.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rules">The rules to add if the condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAsync(async (token) => await product.IsAvailableAsync(token),
    ///     Rules.IsNotEmpty(product.DownloadUrl),
    ///     Rules.StringLength(product.DownloadUrl, 5, 100))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.WhenAsync(Add(), condition, rules);
    }

    /// <summary>
    /// Adds rules with an asynchronous condition using multiple rules.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="predicate">The predicate to evaluate if the condition is true.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAsync(async (token) => await product.IsAvailableAsync(token),
    ///     () => product.Price > 0,
    ///     "Price must be greater than 0 for available products")
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, Func<bool> predicate, string message = null)
    {
        return RuleBuilderConditionalExtensions.WhenAsync(Add(), condition, predicate, message);
    }

    /// <summary>
    /// Adds rules asynchronously when the specified condition is false.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is false.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.UnlessAsync(async (token) => await product.IsDigitalAsync(token),
    ///     builder => builder
    ///         .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///         .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(Func<CancellationToken, Task<bool>> condition, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.UnlessAsync(Add(), condition, addRules);
    }

    /// <summary>
    /// Adds a rule asynchronously when the specified condition is false.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is false.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.UnlessAsync(async (token) => await product.IsDigitalAsync(token),
    ///     Rules.IsNotEmpty(product.ShippingAddress))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(Func<CancellationToken, Task<bool>> condition, IRule rule)
    {
        return RuleBuilderConditionalExtensions.UnlessAsync(Add(), condition, rule);
    }

    /// <summary>
    /// Adds multiple rules asynchronously when the specified condition is false.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rules">The rules to add if the condition is false.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.UnlessAsync(async (token) => await product.IsDigitalAsync(token),
    ///     Rules.IsNotEmpty(product.ShippingAddress),
    ///     Rules.StringLength(product.ShippingAddress, 10, 200))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(Func<CancellationToken, Task<bool>> condition, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.UnlessAsync(Add(), condition, rules);
    }

    /// <summary>
    /// Adds a rule defined by a predicate when the async condition is false.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="predicate">The predicate to evaluate if the condition is false.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.UnlessAsync(async (token) => await product.IsDigitalAsync(token),
    ///     () => product.Price > 0,
    ///     "Price must be greater than 0 for non-digital products")
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(Func<CancellationToken, Task<bool>> condition, Func<bool> predicate, string message = null)
    {
        return RuleBuilderConditionalExtensions.UnlessAsync(Add(), condition, predicate, message);
    }

    /// <summary>
    /// Adds a rule with an asynchronous condition.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAsync(async (token) => await product.IsAvailableAsync(token), Rules.ApplyAsyncRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenAsync(Add(), condition, rule);
    }

    /// <summary>
    /// Adds multiple rules with an asynchronous condition.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAsync(async (token) => await product.IsAvailableAsync(token), builder => builder
    ///     .Add(Rules.ApplyAsyncRule())
    ///     .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenAsync(Add(), condition, addRules);
    }

    /// <summary>
    /// Adds a rule when any of the conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if any condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAny(new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenAny(Add(), conditions, rule);
    }

    /// <summary>
    /// Adds rules when any of the conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if any condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAny(new[] { () => product.IsDigital, () => product.HasSubscription }, builder => builder
    ///     .Add(Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Add(Rules.StringLength(product.DownloadUrl, 5, 100)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenAny(Add(), conditions, addRules);
    }

    /// <summary>
    /// Adds multiple rules when any of the conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if any condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAny(new[] { () => product.IsDigital, () => product.HasSubscription },
    ///     Rules.IsNotEmpty(product.DownloadUrl),
    ///     Rules.StringLength(product.DownloadUrl, 5, 100))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.WhenAny(Add(), conditions, rules);
    }

    /// <summary>
    /// Adds a rule when all conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if all conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAll(new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenAll(Add(), conditions, rule);
    }

    /// <summary>
    /// Adds rules when all conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if all conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAll(new[] { () => product.IsDigital, () => product.HasSubscription }, builder => builder
    ///     .Add(Rules.IsNotEmpty(product.DownloadUrl))
    ///     .Add(Rules.StringLength(product.DownloadUrl, 5, 100)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenAll(Add(), conditions, addRules);
    }

    /// <summary>
    /// Adds multiple rules when all conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if all conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAll(new[] { () => product.IsDigital, () => product.HasSubscription },
    ///     Rules.IsNotEmpty(product.DownloadUrl),
    ///     Rules.StringLength(product.DownloadUrl, 5, 100))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.WhenAll(Add(), conditions, rules);
    }

    /// <summary>
    /// Adds a rule when none of the conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if none of the conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenNone(new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.GreaterThan(product.Price, 10m))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenNone(Add(), conditions, rule);
    }

    /// <summary>
    /// Adds rules when none of the conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if none of the conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenNone(new[] { () => product.IsDigital, () => product.HasSubscription }, builder => builder
    ///     .Add(Rules.GreaterThan(product.Price, 10m))
    ///     .Add(Rules.IsNotEmpty(product.ShippingAddress)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenNone(Add(), conditions, addRules);
    }

    /// <summary>
    /// Adds multiple rules when none of the conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if none of the conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenNone(new[] { () => product.IsDigital, () => product.HasSubscription },
    ///     Rules.GreaterThan(product.Price, 10m),
    ///     Rules.IsNotEmpty(product.ShippingAddress))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.WhenNone(Add(), conditions, rules);
    }

    /// <summary>
    /// Adds a rule when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the exact count of conditions is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenExactly(2, new[] { () => product.HasSubscription, () => product.IsDigital, () => product.IsOnSale }, Rules.ApplySpecialDiscount())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenExactly(Add(), count, conditions, rule);
    }

    /// <summary>
    /// Adds rules when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if the exact count of conditions is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenExactly(2, new[] { () => product.HasSubscription, () => product.IsDigital, () => product.IsOnSale }, builder => builder
    ///     .Add(Rules.ApplySpecialDiscount())
    ///     .Add(Rules.RequireDownloadUrl()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenExactly(Add(), count, conditions, addRules);
    }

    /// <summary>
    /// Adds multiple rules when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if the exact count of conditions is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenExactly(2, new[]
    /// {
    ///     () => product.IsDigital,
    ///     () => product.HasSubscription,
    ///     () => product.IsOnSale
    /// },
    /// Rules.ApplyDigitalPricing(),
    /// Rules.RequireDownloadUrl())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.WhenExactly(Add(), count, conditions, rules);
    }

    /// <summary>
    /// Adds a rule when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if at least the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtLeast(1, new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.SendNotification())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenAtLeast(Add(), count, conditions, rule);
    }

    /// <summary>
    /// Adds rules when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if at least the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtLeast(1, new[] { () => product.IsDigital, () => product.HasSubscription }, builder => builder
    ///     .Add(Rules.SendNotification())
    ///     .Add(Rules.ApplyDiscount()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenAtLeast(Add(), count, conditions, addRules);
    }

    /// <summary>
    /// Adds multiple rules when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if at least the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtLeast(1, new[] { () => product.IsDigital, () => product.HasSubscription },
    ///     Rules.SendNotification(),
    ///     Rules.ApplyDiscount())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.WhenAtLeast(Add(), count, conditions, rules);
    }

    /// <summary>
    /// Adds a rule when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if at most the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtMost(1, new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.ApplyStandardPrice())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenAtMost(Add(), count, conditions, rule);
    }

    /// <summary>
    /// Adds rules when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if at most the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtMost(1, new[] { () => product.IsDigital, () => product.HasSubscription }, builder => builder
    ///     .Add(Rules.ApplyStandardPrice())
    ///     .Add(Rules.RequireShippingAddress()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenAtMost(Add(), count, conditions, addRules);
    }

    /// <summary>
    /// Adds multiple rules when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if at most the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtMost(1, new[] { () => product.IsDigital, () => product.HasSubscription },
    ///     Rules.ApplyStandardPrice(),
    ///     Rules.RequireShippingAddress())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.WhenAtMost(Add(), count, conditions, rules);
    }

    /// <summary>
    /// Adds a rule when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the true condition count is within the range.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenBetween(1, 2, new[] { () => product.IsDigital, () => product.HasSubscription, () => product.IsOnSale }, Rules.ApplyFlexibleDiscount())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenBetween(Add(), min, max, conditions, rule);
    }

    /// <summary>
    /// Adds rules when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if the true condition count is within the range.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenBetween(1, 2, new[] { () => product.IsDigital, () => product.HasSubscription, () => product.IsOnSale }, builder => builder
    ///     .Add(Rules.ApplyFlexibleDiscount())
    ///     .Add(Rules.RequireDownloadUrl()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenBetween(Add(), min, max, conditions, addRules);
    }

    /// <summary>
    /// Adds multiple rules when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if the true condition count is within the range.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenBetween(1, 2, new[]
    /// {
    ///     () => product.IsDigital,
    ///     () => product.HasSubscription,
    ///     () => product.IsOnSale
    /// },
    /// Rules.ApplyFlexibleDiscount(),
    /// Rules.RequireDownloadUrl())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return RuleBuilderConditionalExtensions.WhenBetween(Add(), min, max, conditions, rules);
    }

    /// <summary>
    /// Adds a rule when the specified condition is false.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is false.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.Unless(product.IsDigital, Rules.GreaterThan(product.Price, 10m))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(bool condition, IRule rule)
    {
        return RuleBuilderConditionalExtensions.Unless(Add(), condition, rule);
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is false.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is false.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.Unless(product.IsDigital, builder => builder
    ///     .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///     .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(bool condition, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.Unless(Add(), condition, addRules);
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate when the specified condition is false.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="predicate">The boolean predicate to evaluate if the condition is false.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.Unless(user.IsActive, () => user.IsVerified, "User must be verified if not active.")
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(bool condition, Func<bool> predicate, string message = null)
    {
        return RuleBuilderConditionalExtensions.Unless(Add(), condition, predicate, message);
    }

    /// <summary>
    /// Adds multiple rules defined by boolean predicates when the specified condition is false.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="predicates">The boolean predicates to evaluate if the condition is false.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.Unless(product.IsOnSale,
    ///     () => product.Discount >= 0.1m,
    ///     () => product.Discount == 0.5m)
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(bool condition, params Func<bool>[] predicates)
    {
        return RuleBuilderConditionalExtensions.Unless(Add(), condition, predicates);
    }

    /// <summary>
    /// Adds a rule when all of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if all conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAll(new[] { true, true, true }, Rules.ApplyAllTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<bool> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenAll(Add(), conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when all of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if all conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAll(new[] { true, true, true }, builder => builder
    ///     .Add(Rules.ApplyAllTrueRule())
    ///     .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenAll(Add(), conditions, addRules);
    }

    /// <summary>
    /// Adds a rule when any of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if any condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAny(new[] { true, false, false }, Rules.ApplyAnyTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<bool> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenAny(Add(), conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when any of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if any condition is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAny(new[] { true, false, false }, builder => builder
    ///     .Add(Rules.ApplyAnyTrueRule())
    ///     .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenAny(Add(), conditions, addRules);
    }

    /// <summary>
    /// Adds a rule when none of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if none of the conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenNone(new[] { false, false, false }, Rules.ApplyNoneTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<bool> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenNone(Add(), conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when none of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if none of the conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenNone(new[] { false, false, false }, builder => builder
    ///     .Add(Rules.ApplyNoneTrueRule())
    ///     .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenNone(Add(), conditions, addRules);
    }

    /// <summary>
    /// Adds a rule when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the exact count of conditions is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenExactly(2, new[] { true, true, false }, Rules.ApplyExactTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenExactly(Add(), count, conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if the exact count of conditions is true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenExactly(2, new[] { true, true, false }, builder => builder
    ///     .Add(Rules.ApplyExactTrueRule())
    ///     .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenExactly(Add(), count, conditions, addRules);
    }

    /// <summary>
    /// Adds a rule when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if at least the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtLeast(1, new[] { true, false, false }, Rules.ApplyAtLeastTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenAtLeast(Add(), count, conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if at least the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtLeast(1, new[] { true, false, false }, builder => builder
    ///     .Add(Rules.ApplyAtLeastTrueRule())
    ///     .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenAtLeast(Add(), count, conditions, addRules);
    }

    /// <summary>
    /// Adds a rule when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if at most the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtMost(1, new[] { true, false, false }, Rules.ApplyAtMostTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenAtMost(Add(), count, conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if at most the specified count of conditions are true.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenAtMost(1, new[] { true, false, false }, builder => builder
    ///     .Add(Rules.ApplyAtMostTrueRule())
    ///     .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenAtMost(Add(), count, conditions, addRules);
    }

    /// <summary>
    /// Adds a rule when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the true condition count is within the range.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenBetween(1, 2, new[] { true, true, false }, Rules.ApplyBetweenTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<bool> conditions, IRule rule)
    {
        return RuleBuilderConditionalExtensions.WhenBetween(Add(), min, max, conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if the true condition count is within the range.</param>
    /// <returns>A new RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.WhenBetween(1, 2, new[] { true, true, false }, builder => builder
    ///     .Add(Rules.ApplyBetweenTrueRule())
    ///     .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return RuleBuilderConditionalExtensions.WhenBetween(Add(), min, max, conditions, addRules);
    }
}