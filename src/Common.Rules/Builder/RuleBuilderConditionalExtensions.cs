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
    // Basic When methods
    /// <summary>
    /// Adds a rule with a sync condition using a lambda expression.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
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
    /// Adds a rule with an asynchronous condition.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAsync(async (token) => await product.IsAvailableAsync(token),
    ///         async (token) => await Rules.GetAsyncRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        Func<CancellationToken, Task<IRule>> rule)
    {
        var result = await condition(CancellationToken.None);
        if (result)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
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
    /// Rule
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
    /// Adds multiple rules with an asynchronous condition.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rules">The async rules to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAsync(async (token) => await product.IsAvailableAsync(token),
    ///         async (token) => await Rules.GetAsyncRule1(),
    ///         async (token) => await Rules.GetAsyncRule2())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        var result = await condition(CancellationToken.None);
        if (result)
        {
            foreach (var rule in rules)
            {
                builder.Add(await rule(CancellationToken.None));
            }
        }

        return builder;
    }

    // When with predicates
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
    /// Rule
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
    /// Adds multiple rules defined by async predicates when the specified condition is true.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAsync(async (token) => await product.HasSubscriptionAsync(token),
    ///         async (token) => await product.IsSubscriptionLevelValidAsync(token),
    ///         async (token) => await product.IsSubscriptionActiveAsync(token))
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        Func<CancellationToken, Task<bool>> predicate,
        string message = null)
    {
        var result = await condition(CancellationToken.None);
        if (result)
        {
            builder.Add(new AsyncFuncRule(predicate, message));
        }

        return builder;
    }

    // Unless methods
    /// <summary>
    /// Adds a rule when the specified condition is false.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
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
    /// Adds a rule asynchronously when the specified condition is false.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .UnlessAsync(async (token) => await product.IsDigitalAsync(token),
    ///         Rules.IsNotEmpty(product.ShippingAddress))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        IRule rule)
    {
        return builder.Add(new AsyncConditionalRule(
            async (token) => !(await condition(token)),
            rule));
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
    /// Rule
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
    /// Adds rules with a sync condition using a builder action.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
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
    /// Adds rules with an asynchronous condition using multiple rules.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="predicate">The predicate to evaluate if the condition is true.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAsync(async (token) => await product.IsAvailableAsync(token),
    ///         () => product.Price > 0,
    ///         "Price must be greater than 0 for available products")
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        Func<bool> predicate,
        string message = null)
    {
        return builder.Add(new AsyncConditionalRule(condition, new FuncRule(predicate, message)));
    }

    /// <summary>
    /// Adds rules with an asynchronous condition using multiple rules.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAsync(async (token) => await product.IsAvailableAsync(token), builder => builder
    ///         .Add(Rules.ApplyAsyncRule())
    ///         .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAsync(this RuleBuilder builder, Func<CancellationToken, Task<bool>> condition, Action<RuleBuilder> addRules)
    {
        var innerBuilder = new RuleBuilder();
        addRules(innerBuilder);

        foreach (var rule in innerBuilder.Rules)
        {
            builder.Add(new AsyncConditionalRule(condition, rule));
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
    /// Rule
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
    /// Adds multiple rules when the specified condition is true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="rule">The rule to add rules if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .When(!product.IsDigital, builder, Rules.IsNotEmpty(product.ShippingAddress))
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
    /// Adds multiple rules defined by boolean predicates when the specified condition is true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="predicates">The boolean predicates to evaluate if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .When(product.HasSubscription,
    ///         () => product.SubscriptionLevel > 0,
    ///         () => product.SubscriptionValid)
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder When(this RuleBuilder builder, bool condition, params Func<bool>[] predicates)
    {
        if (!condition)
        {
            return builder;
        }

        foreach (var predicate in predicates)
        {
            builder.Add(new FuncRule(predicate));
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
    /// Rule
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
    /// Rule
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
    /// Rule
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

    /// <summary>
    /// Adds rules asynchronously when the specified condition is false.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .UnlessAsync(async (token) => await product.IsDigitalAsync(token),
    ///         builder => builder
    ///             .Add(Rules.IsNotEmpty(product.ShippingAddress))
    ///             .Add(Rules.StringLength(product.ShippingAddress, 10, 200)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        Action<RuleBuilder> addRules)
    {
        var innerBuilder = new RuleBuilder();
        addRules(innerBuilder);

        foreach (var rule in innerBuilder.Rules)
        {
            builder.Add(new AsyncConditionalRule(
                async (token) => !(await condition(token)),
                rule));
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple rules asynchronously when the specified condition is false.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="rules">The rules to add if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .UnlessAsync(async (token) => await product.IsDigitalAsync(token),
    ///         Rules.IsNotEmpty(product.ShippingAddress),
    ///         Rules.StringLength(product.ShippingAddress, 10, 200))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        params IRule[] rules)
    {
        foreach (var rule in rules)
        {
            builder.Add(new AsyncConditionalRule(
                async (token) => !(await condition(token)),
                rule));
        }

        return builder;
    }

    /// <summary>
    /// Adds a rule defined by a predicate when the async condition is false.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="condition">The asynchronous condition to evaluate.</param>
    /// <param name="predicate">The predicate to evaluate if the condition is false.</param>
    /// <param name="message">Optional custom message for rule failure.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .UnlessAsync(async (token) => await product.IsDigitalAsync(token),
    ///         () => product.Price > 0,
    ///         "Price must be greater than 0 for non-digital products")
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder UnlessAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        Func<bool> predicate,
        string message = null)
    {
        return builder.Add(new AsyncConditionalRule(
            async (token) => !(await condition(token)),
            new FuncRule(predicate, message)));
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
    /// Rule
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
    /// Rule
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
    /// Adds multiple rules when any of the conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAny(new[] { () => product.IsDigital, () => product.HasSubscription },
    ///         Rules.IsNotEmpty(product.DownloadUrl),
    ///         Rules.StringLength(product.DownloadUrl, 5, 100))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(
        this RuleBuilder builder,
        IEnumerable<Func<bool>> conditions,
        params IRule[] rules)
    {
        return builder.When(() => conditions.Any(c => c()), rules);
    }

    /// <summary>
    /// Adds a rule when any of the conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAnyAsync(new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAnyAsync(
        this RuleBuilder builder,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        var result = await Task.WhenAny(conditions.Select(c => c(CancellationToken.None)));
        if (await result)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple rules when any of the conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAnyAsync(new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule1(),
    ///     async (token) => await Rules.GetAsyncRule2())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAnyAsync(
        this RuleBuilder builder,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        var result = await Task.WhenAny(conditions.Select(c => c(CancellationToken.None)));
        if (await result)
        {
            foreach (var rule in rules)
            {
                builder.Add(await rule(CancellationToken.None));
            }
        }

        return builder;
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
    /// Rule
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
    /// Adds rules when all conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAll(new[] { () => product.IsDigital, () => product.HasSubscription }, builder => builder
    ///         .Add(Rules.IsNotEmpty(product.DownloadUrl))
    ///         .Add(Rules.StringLength(product.DownloadUrl, 5, 100)))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(
        this RuleBuilder builder,
        IEnumerable<Func<bool>> conditions,
        Action<RuleBuilder> addRules)
    {
        return builder.When(() => conditions.All(c => c()), addRules);
    }

    /// <summary>
    /// Adds multiple rules when all conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAll(new[] { () => product.IsDigital, () => product.HasSubscription },
    ///         Rules.IsNotEmpty(product.DownloadUrl),
    ///         Rules.StringLength(product.DownloadUrl, 5, 100))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(
        this RuleBuilder builder,
        IEnumerable<Func<bool>> conditions,
        params IRule[] rules)
    {
        return builder.When(() => conditions.All(c => c()), rules);
    }

    /// <summary>
    /// Adds rules when all conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAllAsync(new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAllAsync(
        this RuleBuilder builder,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.All(r => r))
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple rules when all conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAllAsync(new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule1(),
    ///     async (token) => await Rules.GetAsyncRule2())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAllAsync(
        this RuleBuilder builder,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.All(r => r))
        {
            foreach (var rule in rules)
            {
                builder.Add(await rule(CancellationToken.None));
            }
        }

        return builder;
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
    /// Rule
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
    /// Adds rules when none of the conditions are true.
    /// </summary>
    public static RuleBuilder WhenNone(
        this RuleBuilder builder,
        IEnumerable<Func<bool>> conditions,
        Action<RuleBuilder> addRules)
    {
        return builder.When(() => !conditions.Any(c => c()), addRules);
    }

    /// <summary>
    /// Adds multiple rules when none of the conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if none of the conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenNone(new[] { () => product.IsDigital, () => product.HasSubscription },
    ///         Rules.GreaterThan(product.Price, 10m),
    ///         Rules.IsNotEmpty(product.ShippingAddress))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(
        this RuleBuilder builder,
        IEnumerable<Func<bool>> conditions,
        params IRule[] rules)
    {
        return builder.When(() => !conditions.Any(c => c()), rules);
    }

    /// <summary>
    /// Adds rules when none of the conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenNoneAsync(new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenNoneAsync(
        this RuleBuilder builder,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.All(r => !r))
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple rules when none of the conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenNoneAsync(new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule1(),
    ///     async (token) => await Rules.GetAsyncRule2())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenNoneAsync(
        this RuleBuilder builder,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.All(r => !r))
        {
            foreach (var rule in rules)
            {
                builder.Add(await rule(CancellationToken.None));
            }
        }

        return builder;
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
    /// Rule
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
    /// Adds rules when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if the exact count of conditions is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenExactly(2, new[] { () => product.HasSubscription, () => product.IsDigital, () => product.IsOnSale }, builder => builder
    ///         .Add(Rules.ApplySpecialDiscount())
    ///         .Add(Rules.RequireDownloadUrl()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        Action<RuleBuilder> addRules)
    {
        return builder.When(() => conditions.Count(c => c()) == count, addRules);
    }

    /// <summary>
    /// Adds multiple rules when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if the exact count of conditions is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenExactly(2, new[]
    ///     {
    ///         () => product.IsDigital,
    ///         () => product.HasSubscription,
    ///         () => product.IsOnSale
    ///     },
    ///     Rules.ApplyDigitalPricing(),
    ///     Rules.RequireDownloadUrl())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        params IRule[] rules)
    {
        return builder.When(() => conditions.Count(c => c()) == count, rules);
    }

    /// <summary>
    /// Adds rules when exactly the specified number of conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenExactlyAsync(2, new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenExactlyAsync(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.Count(r => r) == count)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple rules when exactly the specified number of conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenExactlyAsync(2, new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule1(),
    ///     async (token) => await Rules.GetAsyncRule2())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenExactlyAsync(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.Count(r => r) != count)
        {
            return builder;
        }

        foreach (var rule in rules)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
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
    /// Rule
    /// ///     .WhenAtLeast(1, new[] { () => product.IsDigital, () => product.HasSubscription }, Rules.SendNotification())
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
    /// Adds rules when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if at least the specified count of conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAtLeast(1, new[] { () => product.IsDigital, () => product.HasSubscription }, builder => builder
    ///         .Add(Rules.SendNotification())
    ///         .Add(Rules.ApplyDiscount()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        Action<RuleBuilder> addRules)
    {
        return builder.When(() => conditions.Count(c => c()) >= count, addRules);
    }

    /// <summary>
    /// Adds multiple rules when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if at least the specified count of conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAtLeast(1, new[] { () => product.IsDigital, () => product.HasSubscription },
    ///         Rules.SendNotification(),
    ///         Rules.ApplyDiscount())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        params IRule[] rules)
    {
        return builder.When(() => conditions.Count(c => c()) >= count, rules);
    }

    /// <summary>
    /// Adds rules when at least the specified number of conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAtLeastAsync(1, new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAtLeastAsync(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.Count(r => r) >= count)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple rules when at least the specified number of conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAtLeastAsync(1, new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule1(),
    ///     async (token) => await Rules.GetAsyncRule2())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAtLeastAsync(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.Count(r => r) < count)
        {
            return builder;
        }

        foreach (var rule in rules)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
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
    /// Rule
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
    /// Adds rules when at most the specified number of conditions are true.
    /// </summary>
    public static RuleBuilder WhenAtMost(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        Action<RuleBuilder> addRules)
    {
        return builder.When(() => conditions.Count(c => c()) <= count, addRules);
    }

    /// <summary>
    /// Adds multiple rules when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if at most the specified count of conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAtMost(1, new[] { () => product.IsDigital, () => product.HasSubscription },
    ///         Rules.ApplyStandardPrice(),
    ///         Rules.RequireShippingAddress())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<bool>> conditions,
        params IRule[] rules)
    {
        return builder.When(() => conditions.Count(c => c()) <= count, rules);
    }

    /// <summary>
    /// Adds rules when at most the specified number of conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAtMostAsync(1, new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAtMostAsync(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.Count(r => r) <= count)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple rules when at most the specified number of conditions are true asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAtMostAsync(1, new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule1(),
    ///     async (token) => await Rules.GetAsyncRule2())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAtMostAsync(
        this RuleBuilder builder,
        int count,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        if (results.Count(r => r) > count)
        {
            return builder;
        }

        foreach (var rule in rules)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
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
    /// Rule
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
            },
            rule);
    }

    /// <summary>
    /// Adds rules when the number of true conditions falls within the specified range.
    /// </summary>
    public static RuleBuilder WhenBetween(
        this RuleBuilder builder,
        int min,
        int max,
        IEnumerable<Func<bool>> conditions,
        Action<RuleBuilder> addRules)
    {
        return builder.When(() =>
            {
                var trueCount = conditions.Count(c => c());

                return trueCount >= min && trueCount <= max;
            },
            addRules);
    }

    /// <summary>
    /// Adds multiple rules when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rules">The rules to add if the true condition count is within the range.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenBetween(1, 2, new[]
    ///     {
    ///         () => product.IsDigital,
    ///         () => product.HasSubscription,
    ///         () => product.IsOnSale
    ///     },
    ///     Rules.ApplyFlexibleDiscount(),
    ///     Rules.RequireDownloadUrl())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(
        this RuleBuilder builder,
        int min,
        int max,
        IEnumerable<Func<bool>> conditions,
        params IRule[] rules)
    {
        return builder.When(() =>
            {
                var trueCount = conditions.Count(c => c());

                return trueCount >= min && trueCount <= max;
            },
            rules);
    }

    /// <summary>
    /// Adds rules when the number of true conditions falls within the specified range asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenBetweenAsync(1, 2, new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenBetweenAsync(
        this RuleBuilder builder,
        int min,
        int max,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        Func<CancellationToken, Task<IRule>> rule)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        var trueCount = results.Count(r => r);
        if (trueCount >= min && trueCount <= max)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple rules when the number of true conditions falls within the specified range asynchronously.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenBetweenAsync(1, 2, new[] {
    ///         async (token) => await product.IsDigitalAsync(token),
    ///         async (token) => await product.HasSubscriptionAsync(token)
    ///     },
    ///     async (token) => await Rules.GetAsyncRule1(),
    ///     async (token) => await Rules.GetAsyncRule2())
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenBetweenAsync(
        this RuleBuilder builder,
        int min,
        int max,
        IEnumerable<Func<CancellationToken, Task<bool>>> conditions,
        params Func<CancellationToken, Task<IRule>>[] rules)
    {
        var results = await Task.WhenAll(conditions.Select(c => c(CancellationToken.None)));
        var trueCount = results.Count(r => r);
        if (trueCount < min || trueCount > max)
        {
            return builder;
        }

        foreach (var rule in rules)
        {
            builder.Add(await rule(CancellationToken.None));
        }

        return builder;
    }

    /// <summary>
    /// Adds a rule when all of the specified conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAll(new[] { true, true, true }, Rules.ApplyAllTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(this RuleBuilder builder, IEnumerable<bool> conditions, IRule rule)
    {
        return builder.When(conditions.All(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when all of the specified conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAll(new[] { true, true, true }, builder => builder
    ///         .Add(Rules.ApplyAllTrueRule())
    ///         .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAll(this RuleBuilder builder, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return builder.When(conditions.All(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule when any of the specified conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAny(new[] { true, false, false }, Rules.ApplyAnyTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(this RuleBuilder builder, IEnumerable<bool> conditions, IRule rule)
    {
        return builder.When(conditions.Any(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when any of the specified conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAny(new[] { true, false, false }, builder => builder
    ///         .Add(Rules.ApplyAnyTrueRule())
    ///         .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAny(this RuleBuilder builder, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return builder.When(conditions.Any(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule when none of the specified conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if none of the conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenNone(new[] { false, false, false }, Rules.ApplyNoneTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(this RuleBuilder builder, IEnumerable<bool> conditions, IRule rule)
    {
        return builder.When(!conditions.Any(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when none of the specified conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if none of the conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenNone(new[] { false, false, false }, builder => builder
    ///         .Add(Rules.ApplyNoneTrueRule())
    ///         .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenNone(this RuleBuilder builder, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return builder.When(!conditions.Any(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the exact count of conditions is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenExactly(2, new[] { true, true, false }, Rules.ApplyExactTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(this RuleBuilder builder, int count, IEnumerable<bool> conditions, IRule rule)
    {
        return builder.When(() => conditions.Count(c => c) == count, rule);
    }

    /// <summary>
    /// Adds multiple rules when exactly the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The exact number of conditions that must be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if the exact count of conditions is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenExactly(2, new[] { true, true, false }, builder => builder
    ///         .Add(Rules.ApplyExactTrueRule())
    ///         .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenExactly(this RuleBuilder builder, int count, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return builder.When(() => conditions.Count(c => c) == count, addRules);
    }

    /// <summary>
    /// Adds a rule when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if at least the specified count of conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAtLeast(1, new[] { true, false, false }, Rules.ApplyAtLeastTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(this RuleBuilder builder, int count, IEnumerable<bool> conditions, IRule rule)
    {
        return builder.When(() => conditions.Count(c => c) >= count, rule);
    }

    /// <summary>
    /// Adds multiple rules when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The minimum number of conditions that must be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if at least the specified count of conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAtLeast(1, new[] { true, false, false }, builder => builder
    ///         .Add(Rules.ApplyAtLeastTrueRule())
    ///         .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtLeast(this RuleBuilder builder, int count, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return builder.When(() => conditions.Count(c => c) >= count, addRules);
    }

    /// <summary>
    /// Adds a rule when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if at most the specified count of conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAtMost(1, new[] { true, false, false }, Rules.ApplyAtMostTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(this RuleBuilder builder, int count, IEnumerable<bool> conditions, IRule rule)
    {
        return builder.When(() => conditions.Count(c => c) <= count, rule);
    }

    /// <summary>
    /// Adds multiple rules when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="count">The maximum number of conditions that can be true.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if at most the specified count of conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenAtMost(1, new[] { true, false, false }, builder => builder
    ///         .Add(Rules.ApplyAtMostTrueRule())
    ///         .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenAtMost(this RuleBuilder builder, int count, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        return builder.When(() => conditions.Count(c => c) <= count, addRules);
    }

    /// <summary>
    /// Adds a rule when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the true condition count is within the range.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenBetween(1, 2, new[] { true, true, false }, Rules.ApplyBetweenTrueRule())
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(this RuleBuilder builder, int min, int max, IEnumerable<bool> conditions, IRule rule)
    {
        var trueCount = conditions.Count(c => c);

        return builder.When(trueCount >= min && trueCount <= max, rule);
    }

    /// <summary>
    /// Adds multiple rules when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="builder">The RuleBuilder instance.</param>
    /// /// <param name="min">The minimum number of true conditions.</param>
    /// <param name="max">The maximum number of true conditions.</param>
    /// <param name="conditions">The boolean conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if the true condition count is within the range.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// Rule
    ///     .WhenBetween(1, 2, new[] { true, true, false }, builder => builder
    ///         .Add(Rules.ApplyBetweenTrueRule())
    ///         .Add(Rules.AnotherRule()))
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder WhenBetween(this RuleBuilder builder, int min, int max, IEnumerable<bool> conditions, Action<RuleBuilder> addRules)
    {
        var trueCount = conditions.Count(c => c);

        return builder.When(trueCount >= min && trueCount <= max, addRules);
    }

    /// <summary>
    /// Adds multiple rules defined by async predicates when the specified condition is true.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .WhenAsync(async (token) => await product.HasSubscriptionAsync(token),
    ///         async (token) => await product.IsSubscriptionLevelValidAsync(token),
    ///         async (token) => await product.IsSubscriptionActiveAsync(token))
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> WhenAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        params Func<CancellationToken, Task<bool>>[] predicates)
    {
        var result = await condition(CancellationToken.None);
        if (!result)
        {
            return builder;
        }

        foreach (var predicate in predicates)
        {
            builder.Add(new AsyncFuncRule(predicate));
        }

        return builder;
    }

    /// <summary>
    /// Adds a rule defined by an async predicate when the specified condition is false.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .UnlessAsync(async (token) => await user.IsActiveAsync(token),
    ///         async (token) => await user.IsVerifiedAsync(token),
    ///         "User must be verified if not active.")
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> UnlessAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        Func<CancellationToken, Task<bool>> predicate,
        string message = null)
    {
        var result = await condition(CancellationToken.None);
        if (!result)
        {
            builder.Add(new AsyncFuncRule(predicate, message));
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple rules defined by async predicates when the specified condition is false.
    /// </summary>
    /// <example>
    /// <code>
    /// await Rule
    ///     .UnlessAsync(async (token) => await product.IsOnSaleAsync(token),
    ///         async (token) => await product.IsDiscountValidAsync(token),
    ///         async (token) => await product.IsPriceValidAsync(token))
    ///     .Apply();
    /// </code>
    /// </example>
    public static async Task<RuleBuilder> UnlessAsync(
        this RuleBuilder builder,
        Func<CancellationToken, Task<bool>> condition,
        params Func<CancellationToken, Task<bool>>[] predicates)
    {
        var result = await condition(CancellationToken.None);
        if (result)
        {
            return builder;
        }

        foreach (var predicate in predicates)
        {
            builder.Add(new AsyncFuncRule(predicate));
        }

        return builder;
    }
}