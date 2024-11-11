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
    ///         .Check();
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
    ///     .Check();
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
    ///     .Check();
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder When(Func<bool> condition, IRule rule)
    {
        return Add().When(condition, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder When(Func<bool> condition, params IRule[] rules)
    {
        return Add().When(condition, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder When(Func<bool> condition, Action<RuleBuilder> addRules)
    {
        return Add().When(condition, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder When(bool condition, IRule rule)
    {
        return Add().When(condition, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder When(bool condition, Action<RuleBuilder> addRules)
    {
        return Add().When(condition, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder When(bool condition, Func<bool> predicate, string message = null)
    {
        return Add().When(condition, predicate, message);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder When(bool condition, params Func<bool>[] predicates)
    {
        return Add().When(condition, predicates);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, params IRule[] rules)
    {
        return Add().WhenAsync(condition, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, Func<bool> predicate, string message = null)
    {
        return Add().WhenAsync(condition, predicate, message);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(Func<CancellationToken, Task<bool>> condition, Action<RuleBuilder> addRules)
    {
        return Add().UnlessAsync(condition, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(Func<CancellationToken, Task<bool>> condition, IRule rule)
    {
        return Add().UnlessAsync(condition, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(Func<CancellationToken, Task<bool>> condition, params IRule[] rules)
    {
        return Add().UnlessAsync(condition, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(Func<CancellationToken, Task<bool>> condition, Func<bool> predicate, string message = null)
    {
        return Add().UnlessAsync(condition, predicate, message);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, IRule rule)
    {
        return Add().WhenAsync(condition, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, Action<RuleBuilder> addRules)
    {
        return Add().WhenAsync(condition, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return Add().WhenAny(conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenAny(conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return Add().WhenAny(conditions, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return Add().WhenAll(conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenAll(conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return Add().WhenAll(conditions, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return Add().WhenNone(conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenNone(conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return Add().WhenNone(conditions, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return Add().WhenExactly(count, conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenExactly(count, conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return Add().WhenExactly(count, conditions, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return Add().WhenAtLeast(count, conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenAtLeast(count, conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return Add().WhenAtLeast(count, conditions, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return Add().WhenAtMost(count, conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenAtMost(count, conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return Add().WhenAtMost(count, conditions, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<Func<bool>> conditions, IRule rule)
    {
        return Add().WhenBetween(min, max, conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<Func<bool>> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenBetween(min, max, conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<Func<bool>> conditions, params IRule[] rules)
    {
        return Add().WhenBetween(min, max, conditions, rules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(bool condition, IRule rule)
    {
        return Add().Unless(condition, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(bool condition, Action<RuleBuilder> addRules)
    {
        return Add().Unless(condition, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(bool condition, Func<bool> predicate, string message = null)
    {
        return Add().Unless(condition, predicate, message);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder Unless(bool condition, params Func<bool>[] predicates)
    {
        return Add().Unless(condition, predicates);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<bool> conditions, IRule rule)
    {
        return Add().WhenAll(conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenAll(conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<bool> conditions, IRule rule)
    {
        return Add().WhenAny(conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenAny(conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<bool> conditions, IRule rule)
    {
        return Add().WhenNone(conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenNone(conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return Add().WhenExactly(count, conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(int count, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenExactly(count, conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return Add().WhenAtLeast(count, conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(int count, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenAtLeast(count, conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return Add().WhenAtMost(count, conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(int count, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenAtMost(count, conditions, addRules);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<bool> conditions, IRule rule)
    {
        return Add().WhenBetween(min, max, conditions, rule);
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
    ///     .Check();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(int min, int max, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return Add().WhenBetween(min, max, conditions, addRules);
    }

    /// <summary>
    /// Adds multiple rules defined by async predicates when the specified condition is true.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="predicates">The async predicates to evaluate if the condition is true.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenAsync(async token => await product.HasSubscriptionAsync(token),
    ///     async token => await product.IsSubscriptionLevelValidAsync(token),
    ///     async token => await product.IsSubscriptionActiveAsync(token))
    ///     .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenAsync(
        Func<CancellationToken, Task<bool>> condition,
        params Func<CancellationToken, Task<bool>>[] predicates)
    {
        return Add().WhenAsync(condition, predicates);
    }

    /// <summary>
    /// Adds a rule defined by an async predicate when the specified condition is false.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="predicate">The async predicate to evaluate if the condition is false.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.UnlessAsync(async token => await user.IsActiveAsync(token),
    ///     async token => await user.IsVerifiedAsync(token),
    ///     "User must be verified if not active.")
    ///     .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> UnlessAsync(
        Func<CancellationToken, Task<bool>> condition,
        Func<CancellationToken, Task<bool>> predicate,
        string message = null)
    {
        return Add().UnlessAsync(condition, predicate, message);
    }

    /// <summary>
    /// Adds multiple rules defined by async predicates when the specified condition is false.
    /// </summary>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="predicates">The async predicates to evaluate if the condition is false.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.UnlessAsync(async token => await product.IsOnSaleAsync(token),
    ///     async token => await product.IsDiscountValidAsync(token),
    ///     async token => await product.IsPriceValidAsync(token))
    ///     .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> UnlessAsync(
        Func<CancellationToken, Task<bool>> condition,
        params Func<CancellationToken, Task<bool>>[] predicates)
    {
        return Add().UnlessAsync(condition, predicates);
    }

    /// <summary>
    /// Adds multiple rules when exactly the specified number of conditions are true asynchronously.
    /// </summary>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rules">The async rules to add if the exact count of conditions is true.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenExactlyAsync(2, new[]
    /// {
    ///     async token => await product.IsDigitalAsync(token),
    ///     async token => await product.HasSubscriptionAsync(token),
    ///     async token => await product.IsOnSaleAsync(token)
    /// },
    /// async token => await Rules.GetAsyncRule1(),
    /// async token => await Rules.GetAsyncRule2())
    /// .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenExactlyAsync(
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        return Add().WhenExactlyAsync(count, conditions, rules);
    }

    /// <summary>
    /// Adds a rule when exactly the specified number of conditions are true asynchronously.
    /// </summary>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rule">The async rule to add if the exact count of conditions is true.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenExactlyAsync(2, new[]
    /// {
    ///     async token => await product.IsDigitalAsync(token),
    ///     async token => await product.HasSubscriptionAsync(token)
    /// },
    /// async token => await Rules.GetAsyncRule())
    /// .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenExactlyAsync(
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        return Add().WhenExactlyAsync(count, conditions, rule);
    }

    /// <summary>
    /// Adds a rule when at least the specified number of conditions are true asynchronously.
    /// </summary>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rule">The async rule to add if at least the specified count of conditions are true.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenAtLeastAsync(1, new[]
    /// {
    ///     async token => await product.IsDigitalAsync(token),
    ///     async token => await product.HasSubscriptionAsync(token)
    /// },
    /// async token => await Rules.GetAsyncRule())
    /// .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenAtLeastAsync(
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        return Add().WhenAtLeastAsync(count, conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when at least the specified number of conditions are true asynchronously.
    /// </summary>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rules">The async rules to add if at least the specified count of conditions are true.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenAtLeastAsync(1, new[]
    /// {
    ///     async token => await product.IsDigitalAsync(token),
    ///     async token => await product.HasSubscriptionAsync(token)
    /// },
    /// async token => await Rules.GetAsyncRule1(),
    /// async token => await Rules.GetAsyncRule2())
    /// .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenAtLeastAsync(
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        return Add().WhenAtLeastAsync(count, conditions, rules);
    }

    /// <summary>
    /// Adds a rule when at most the specified number of conditions are true asynchronously.
    /// </summary>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rule">The async rule to add if at most the specified count of conditions are true.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenAtMostAsync(1, new[]
    /// {
    ///     async token => await product.IsDigitalAsync(token),
    ///     async token => await product.HasSubscriptionAsync(token)
    /// },
    /// async token => await Rules.GetAsyncRule())
    /// .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenAtMostAsync(
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        return Add().WhenAtMostAsync(count, conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when at most the specified number of conditions are true asynchronously.
    /// </summary>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rules">The async rules to add if at most the specified count of conditions are true.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenAtMostAsync(1, new[]
    /// {
    ///     async token => await product.IsDigitalAsync(token),
    ///     async token => await product.HasSubscriptionAsync(token)
    /// },
    /// async token => await Rules.GetAsyncRule1(),
    /// async token => await Rules.GetAsyncRule2())
    /// .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenAtMostAsync(
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        return Add().WhenAtMostAsync(count, conditions, rules);
    }

    /// <summary>
    /// Adds a rule when the number of true conditions falls within the specified range asynchronously.
    /// </summary>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rule">The async rule to add if the true condition count is within the range.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenBetweenAsync(1, 2, new[]
    /// {
    ///     async token => await product.IsDigitalAsync(token),
    ///     async token => await product.HasSubscriptionAsync(token),
    ///     async token => await product.IsOnSaleAsync(token)
    /// },
    /// async token => await Rules.GetAsyncRule())
    /// .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenBetweenAsync(
        int min,
        int max,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        return Add().WhenBetweenAsync(min, max, conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when the number of true conditions falls within the specified range asynchronously.
    /// </summary>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rules">The async rules to add if the true condition count is within the range.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenBetweenAsync(1, 2, new[]
    /// {
    ///     async token => await product.IsDigitalAsync(token),
    ///     async token => await product.HasSubscriptionAsync(token),
    ///     async token => await product.IsOnSaleAsync(token)
    /// },
    /// async token => await Rules.GetAsyncRule1(),
    /// async token => await Rules.GetAsyncRule2())
    /// .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenBetweenAsync(
        int min,
        int max,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        return Add().WhenBetweenAsync(min, max, conditions, rules);
    }

    /// <summary>
    /// Adds a rule when any of the conditions are true asynchronously.
    /// </summary>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rule">The async rule to add if any condition is true.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await Rule.WhenAnyAsync(new[]
    /// {
    ///     async token => await product.IsDigitalAsync(token),
    ///     async token => await product.HasSubscriptionAsync(token)
    /// },
    /// async token => await Rules.GetAsyncRule())
    /// .Check();
    /// </code>
    /// </example>
    public static Task<RuleBuilder> WhenAnyAsync(
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        return Add().WhenAnyAsync(conditions, rule);
    }

    /// <summary>
    /// Adds multiple rules when any of the conditions are true asynchronously.
    /// </summary>
    /// <param name="conditions">The async conditions to evaluate.</param>
    /// <param name="rules">The async rules to add if any condition is true.</param>
    /// <returns>A Task containing the RuleBuilder instance.</returns>
    public static Task<RuleBuilder> WhenAnyAsync(
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        return Add().WhenAnyAsync(conditions, rules);
    }
}