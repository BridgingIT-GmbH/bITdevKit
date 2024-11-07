// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a fluent interface for building and executing collections of rules.
/// <para>
/// Usage example:
/// <code>
///     return DomaainRules.For()
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
///             !product.IsDigital,
///             product.Categories?.Count > 2
///         }, Rules.IsNotEmpty(product.ShippingAddress))
///         .Apply();
/// </code>
/// </para>
/// </summary>
public class RulesBuilder
{
    private readonly List<IRule> rules = [];
    private bool continueOnFailure;

    /// <summary>
    /// Adds a rule to the builder.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder Add(IRule rule)
    {
        if (rule is not null)
        {
            this.rules.Add(rule);
        }

        return this;
    }

    /// <summary>
    /// Adds multiple rules to the builder.
    /// </summary>
    /// <param name="rules">The rules to add.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder Add(params IRule[] rules)
    {
        foreach (var rule in rules)
        {
            this.Add(rule);
        }

        return this;
    }

    /// <summary>
    /// Creates an empty rule builder.
    /// </summary>
    /// <returns>A new empty rule builder instance.</returns>
    public static RulesBuilder For()
    {
        return new RulesBuilder();
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rule.
    /// </summary>
    /// <param name="rule">The initial rule.</param>
    /// <returns>A new rule builder instance.</returns>
    public static RulesBuilder For(IRule rule)
    {
        return new RulesBuilder().Add(rule);
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rule.
    /// </summary>
    /// <param name="rules">The initial rules.</param>
    /// <returns>A new rule builder instance.</returns>
    public static RulesBuilder For(params IRule[] rules)
    {
        var builder = new RulesBuilder();
        foreach (var rule in rules)
        {
            builder.Add(rule);
        }

        return builder;
    }

    /// <summary>
    /// Enables error aggregation, collecting all rule failures instead of stopping at the first failure.
    /// </summary>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder ContinueOnFailure()
    {
        this.continueOnFailure = true;

        return this;
    }

    /// <summary>
    /// Adds a rule when the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder When(bool condition, IRule rule)
    {
        return condition ? this.Add(rule) : this;
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder When(bool condition, Action<RulesBuilder> addRules)
    {
        if (condition)
        {
            addRules(this);
        }

        return this;
    }

    /// <summary>
    /// Adds a rule when the specified condition is false.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder Unless(bool condition, IRule rule)
    {
        return this.When(!condition, rule);
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is false.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder Unless(bool condition, Action<RulesBuilder> addRules)
    {
        return this.When(!condition, addRules);
    }

    /// <summary>
    /// Adds a rule with an asynchronous condition.
    /// </summary>
    /// <param name="condition">An async function that determines if the rule should be executed.</param>
    /// <param name="rule">The rule to execute if the condition is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, IRule rule)
    {
        return this.Add(new AsyncConditionalRule(condition, rule));
    }

    /// <summary>
    /// Adds multiple rules with an asynchronous condition.
    /// </summary>
    /// <param name="condition">An async function that determines if the rules should be executed.</param>
    /// <param name="addRules">Action to add rules if the condition is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, Action<RulesBuilder> addRules)
    {
        var builder = new RulesBuilder();
        addRules(builder);

        foreach (var rule in builder.rules)
        {
            this.Add(new AsyncConditionalRule(condition, rule));
        }

        return this;
    }

    /// <summary>
    /// Adds a rule when all of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenAll(IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.All(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when all of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenAll(IEnumerable<bool> conditions, Action<RulesBuilder> addRules)
    {
        return this.When(conditions.All(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule when any of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenAny(IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.Any(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when any of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenAny(IEnumerable<bool> conditions, Action<RulesBuilder> addRules)
    {
        return this.When(conditions.Any(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule when none of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if no conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenNone(IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(!conditions.Any(c => c), rule);
    }

    /// <summary>
    /// Adds a rule when the exact number of conditions are true.
    /// </summary>
    /// <param name="count">The expected number of true conditions.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the exact count matches.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenExactly(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.Count(c => c) == count, rule);
    }

    /// <summary>
    /// Adds a rule when at least the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The minimum number of true conditions required.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the minimum count is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenAtLeast(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.Count(c => c) >= count, rule);
    }

    /// <summary>
    /// Adds a rule when at most the specified number of conditions are true.
    /// </summary>
    /// <param name="count">The maximum number of true conditions allowed.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the count doesn't exceed the maximum.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenAtMost(int count, IEnumerable<bool> conditions, IRule rule)
    {
        return this.When(conditions.Count(c => c) <= count, rule);
    }

    /// <summary>
    /// Adds a rule when the number of true conditions falls within the specified range.
    /// </summary>
    /// <param name="min">The minimum number of true conditions required.</param>
    /// <param name="max">The maximum number of true conditions allowed.</param>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if the count falls within range.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public RulesBuilder WhenBetween(int min, int max, IEnumerable<bool> conditions, IRule rule)
    {
        var trueCount = conditions.Count(c => c);

        return this.When(trueCount >= min && trueCount <= max, rule);
    }

    /// <summary>
    /// Applies the rules synchronously to validate a single object or state.
    /// If continueOnFailure is disabled (default), stops at the first rule failure.
    /// If continueOnFailure is enabled, collects all rule failures.
    /// </summary>
    /// <param name="throwOnFailure"></param>
    /// <param name="logger">Optional logger to use for logging rule execution outcomes.</param>
    /// <returns>A Result object indicating the success or failure of applying the rules.</returns>
    /// <example>
    /// <code>
    /// var result = Rules.For()
    ///     .Add(RuleSet.IsNotEmpty(product.Name))
    ///     .Add(RuleSet.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .When(!product.IsDigital, builder => builder
    ///         .Add(RuleSet.IsNotEmpty(product.ShippingAddress))
    ///         .Add(RuleSet.HasStringLength(product.ShippingAddress, 10, 200)))
    ///     .ContinueOnFailure()
    ///     .Apply();
    /// </code>
    /// </example>
    public Result Apply(bool throwOnFailure = false, ILogger logger = null) // TODO: setup the logger like Result (IResultLogger -> IRulesLogger)
    {
        logger ??= Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        if (!this.rules.Any())
        {
            logger.LogInformation("{LogKey} rules - no rules defined", Constants.LogKey);

            return Result.Success();
        }

        if (!this.continueOnFailure) // never throws
        {
            foreach (var rule in this.rules)
            {
                var result = Rules.Apply(rule, false);
                if (result.IsFailure)
                {
                    logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}",
                        Constants.LogKey,
                        rule.GetType().Name,
                        result.ToString());

                    return result;
                }

                logger.LogInformation("{LogKey} rules - {Rule} result: {RuleResult}",
                    Constants.LogKey,
                    rule.GetType().Name,
                    result.ToString());
            }

            return Result.Success();
        }

        var errors = new List<IResultError>();
        var hasFailures = false;

        foreach (var rule in this.rules)
        {
            var result = Rules.Apply(rule, throwOnFailure);
            if (!result.IsFailure)
            {
                logger.LogInformation("{LogKey} rules - {Rule} result: {RuleResult}",
                    Constants.LogKey,
                    rule.GetType().Name,
                    result.ToString());

                continue;
            }

            logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}",
                Constants.LogKey,
                rule.GetType().Name,
                result.ToString());
            hasFailures = true;
            errors.AddRange(result.Errors);
        }

        return hasFailures
            ? Result.Failure().WithErrors(errors)
            : Result.Success();
    }

    /// <summary>
    /// Applies the rules asynchronously to validate a single object or state.
    /// If continueOnFailure is disabled (default), stops at the first rule failure.
    /// If continueOnFailure is enabled, collects all rule failures.
    /// Supports async conditions and rules.
    /// </summary>
    /// <param name="throwOnFailure"></param>
    /// <param name="logger">Optional logger to use for logging rule execution outcomes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result object indicating the success or failure of applying the rules.</returns>
    /// <example>
    /// <code>
    /// var result = await Rules.For()
    ///     .Add(RuleSet.IsNotEmpty(order.CustomerId))
    ///     .Add(RuleSet.All(order.Items, item =>
    ///         RuleSet.GreaterThan(item.Quantity, 0)))
    ///     .WhenAsync(
    ///         async (token) => await IsCustomerActive(order.CustomerId, token),
    ///         RuleSet.IsNotNull(order.ShippingAddress))
    ///     .ContinueOnFailure()
    ///     .ApplyAsync(logger, cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result> ApplyAsync(bool throwOnFailure = false, ILogger logger = null, CancellationToken cancellationToken = default)
    {
        logger ??= Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        if (!this.rules.Any())
        {
            logger.LogInformation("{LogKey} rules - no rules defined", Constants.LogKey);

            return Result.Success();
        }

        if (!this.continueOnFailure) // never throws
        {
            foreach (var rule in this.rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await Rules.ApplyAsync(rule, false, cancellationToken);
                if (result.IsFailure)
                {
                    logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}",
                        Constants.LogKey,
                        rule.GetType().Name,
                        result.ToString());

                    return result;
                }

                logger.LogInformation("{LogKey} rules - {Rule} result: {RuleResult}",
                    Constants.LogKey,
                    rule.GetType().Name,
                    result.ToString());
            }

            return Result.Success();
        }

        var errors = new List<IResultError>();
        var hasFailures = false;

        foreach (var rule in this.rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await Rules.ApplyAsync(rule, throwOnFailure, cancellationToken);
            if (!result.IsFailure)
            {
                logger.LogInformation("{LogKey} rules - {Rule} result: {RuleResult}",
                    Constants.LogKey,
                    rule.GetType().Name,
                    result.ToString());

                continue;
            }

            logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}",
                Constants.LogKey,
                rule.GetType().Name,
                result.ToString());
            hasFailures = true;
            errors.AddRange(result.Errors);
        }

        return hasFailures
            ? Result.Failure().WithErrors(errors)
            : Result.Success();
    }

    /// <summary>
    /// Filters a collection of items based on the defined rules synchronously.
    /// Items are excluded from the result if they fail any rule validation.
    /// </summary>
    /// <typeparam name="T">The type of items to filter.</typeparam>
    /// <param name="items">The collection of items to filter.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A Result containing the filtered collection.</returns>
    /// <example>
    /// <code>
    /// var result = Rules.For()
    ///     .Add(Product p => RuleSet.IsNotEmpty(p.Name))
    ///     .Add(Product p => RuleSet.NumericRange(p.Price, 0.01m, 999.99m))
    ///     .When(!isDigital, builder => builder
    ///         .Add(Product p => RuleSet.IsNotEmpty(p.ShippingAddress)))
    ///     .Filter(products);
    /// </code>
    /// </example>
    public Result<IEnumerable<T>> Filter<T>(
        IEnumerable<T> items,
        ILogger logger = null)
    {
        if (!this.rules.Any())
        {
            return HandleEmptyInput();
        }

        var filteredItems = new List<T>();

        foreach (var item in items)
        {
            var result = EvaluateRules(item);
            if (result.IsSuccess)
            {
                filteredItems.Add(item);
            }
        }

        return Result<IEnumerable<T>>.Success(filteredItems);

        Result<IEnumerable<T>> HandleEmptyInput()
        {
            return items is null
                ? Result<IEnumerable<T>>.Success(Array.Empty<T>())
                : Result<IEnumerable<T>>.Success(items);
        }

        Result EvaluateRules(T item)
        {
            foreach (var rule in this.rules)
            {
                if (rule is ItemRule<T> itemRule)
                {
                    itemRule.SetItem(item);
                }

                var result = Rules.Apply(rule);
                if (result.IsFailure)
                {
                    return Result.Failure();
                }
            }

            return Result.Success();
        }
    }

    /// <summary>
    /// Filters a collection of items based on the defined rules asynchronously.
    /// Items are excluded from the result if they fail any rule validation.
    /// </summary>
    /// <typeparam name="T">The type of items to filter.</typeparam>
    /// <param name="items">The collection of items to filter.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result containing the filtered collection.</returns>
    /// <example>
    /// <code>
    /// var result = await Rules.For()
    ///     .Add(User u => RuleSet.IsNotEmpty(u.Email))
    ///     .Add(User u => RuleSet.IsValidEmail(u.Email))
    ///     .When(isActive, builder => builder
    ///         .Add(User u => RuleSet.IsNotNull(u.LastLoginDate)))
    ///     .FilterAsync(users, logger, cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<T>>> FilterAsync<T>(
        IEnumerable<T> items,
        ILogger logger = null,
        CancellationToken cancellationToken = default)
    {
        if (!this.rules.Any())
        {
            return HandleEmptyInput();
        }

        var filteredItems = new List<T>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await EvaluateRules(item);
            if (result.IsSuccess)
            {
                filteredItems.Add(item);
            }
        }

        return Result<IEnumerable<T>>.Success(filteredItems);

        Result<IEnumerable<T>> HandleEmptyInput()
        {
            return items is null
                ? Result<IEnumerable<T>>.Success(Array.Empty<T>())
                : Result<IEnumerable<T>>.Success(items);
        }

        async Task<Result> EvaluateRules(T item)
        {
            foreach (var rule in this.rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (rule is ItemRule<T> itemRule)
                {
                    itemRule.SetItem(item);
                }

                var result = await Rules.ApplyAsync(rule, false, cancellationToken);
                if (result.IsFailure)
                {
                    return Result.Failure();
                }
            }

            return Result.Success();
        }
    }
}