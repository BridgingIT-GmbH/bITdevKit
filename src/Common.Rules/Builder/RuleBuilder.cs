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
///     return Rule.For()
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
/// </para>
/// </summary>
public class RuleBuilder
{
    private readonly List<IRule> rules = [];
    private bool continueOnRuleFailure = Rule.Settings.ContinueOnRuleFailure;
    private bool throwOnFailure = Rule.Settings.ThrowOnRuleFailure;
    private bool throwOnException = Rule.Settings.ThrowOnRuleException;

    // Expose rules collection for extension methods
    internal IEnumerable<IRule> Rules => this.rules;

    /// <summary>
    /// Adds a rule to the builder.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .Add(Rules.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Add(IRule rule)
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
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name), Rules.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Add(params IRule[] rules)
    {
        foreach (var rule in rules)
        {
            this.Add(rule);
        }

        return this;
    }

    /// <summary>
    /// Adds a rule defined by a boolean predicate to the builder.
    /// </summary>
    /// <param name="predicate">The boolean predicate to evaluate.</param>
    /// <param name="message"></param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(() => user.IsActive)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Add(Func<bool> predicate, string message = null)
    {
        return this.Add(new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean expressions to the builder.
    /// </summary>
    /// <param name="expressions">The boolean expressions to evaluate.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(() => user.IsActive, () => user.HasValidSubscription)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder Add(params Func<bool>[] expressions)
    {
        foreach (var predicate in expressions)
        {
            this.Add(new FuncRule(predicate));
        }

        return this;
    }

    /// <summary>
    /// Creates an empty rule builder.
    /// </summary>
    /// <returns>A new empty rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For()
    ///     .Add(Rules.IsNotEmpty(user.Name))
    ///     .Apply();
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
    /// var builder = Rule.For(Rules.IsNotEmpty(user.Email))
    ///     .Apply();
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
    /// var builder = Rule.For(
    ///     Rules.IsNotEmpty(order.Id),
    ///     Rules.NumericRange(order.Amount, 1, 1000))
    ///     .Apply();
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

    /// <summary>
    /// Creates a new rule builder starting with the specified boolean predicate.
    /// </summary>
    /// <param name="predicate">The boolean predicate to evaluate.</param>
    /// <param name="message"></param>
    /// <returns>A new rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For(() => user.IsActive)
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder For(Func<bool> predicate, string message = null)
    {
        return new RuleBuilder().Add(new FuncRule(predicate, message));
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified boolean expressions.
    /// </summary>
    /// <param name="expressions">The boolean expressions to evaluate.</param>
    /// <returns>A new rule builder instance.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule.For(
    ///     () => user.IsActive,
    ///     () => user.HasValidSubscription)
    ///     .Apply();
    /// </code>
    /// </example>
    public static RuleBuilder For(params Func<bool>[] expressions)
    {
        var builder = new RuleBuilder();
        foreach (var predicate in expressions)
        {
            builder.Add(new FuncRule(predicate));
        }
        return builder;
    }

    /// <summary>
    /// Enables error aggregation, collecting all rule failures instead of stopping at the first failure.
    /// </summary>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .ContinueOnFailure()
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder ContinueOnFailure()
    {
        this.continueOnRuleFailure = true;

        return this;
    }

    /// <summary>
    /// Sets whether to throw an exception if a rule fails.
    /// </summary>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception on rule failure.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .ThrowOnFailure(true)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder ThrowOnFailure(bool throwOnRuleFailure = true)
    {
        this.throwOnFailure = throwOnRuleFailure;

        return this;
    }

    /// <summary>
    /// Sets whether to throw an exception if a rule throws an exception.
    /// </summary>
    /// <param name="throwOnRuleException">Indicates whether to throw an exception on rule exception.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.For()
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .ThrowOnException(true)
    ///     .Apply();
    /// </code>
    /// </example>
    public RuleBuilder ThrowOnException(bool throwOnRuleException = true)
    {
        this.throwOnException = throwOnRuleException;

        return this;
    }

    /// <summary>
    /// Applies the rules synchronously to validate a single object or state.
    /// If continueOnFailure is disabled (default), stops at the first rule failure.
    /// If continueOnFailure is enabled, collects all rule failures.
    /// </summary>
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
    public Result Apply()
    {
        if (!this.rules.Any())
        {
            //logger.LogDebug("{LogKey} rules - no rules defined", Constants.LogKey);

            return Result.Success();
        }

        if (!this.continueOnRuleFailure)
        {
            foreach (var rule in this.rules)
            {
                var result = Rule.Apply(rule, this.throwOnFailure, this.throwOnException);
                if (result.IsFailure)
                {
                    //logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                    return result;
                }

                //logger.LogDebug("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
            }

            return Result.Success();
        }

        var errors = new List<IResultError>();
        var hasFailures = false;

        foreach (var rule in this.rules)
        {
            var result = Rule.Apply(rule, false);
            if (!result.IsFailure)
            {
                //logger.LogDebug("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                continue;
            }

            //logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

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
    ///     .ApplyAsync(cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (!this.rules.Any())
        {
            // logger.LogDebug("{LogKey} rules - no rules defined", Constants.LogKey);

            return Result.Success();
        }

        if (!this.continueOnRuleFailure)
        {
            foreach (var rule in this.rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await Rule.ApplyAsync(rule, this.throwOnFailure, this.throwOnException, cancellationToken).AnyContext();
                if (result.IsFailure)
                {
                    // logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                    return result;
                }

                // logger.LogDebug("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
            }

            return Result.Success();
        }

        var errors = new List<IResultError>();
        var hasFailures = false;

        foreach (var rule in this.rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await Rule.ApplyAsync(rule, this.throwOnFailure, this.throwOnException, cancellationToken).AnyContext();
            if (!result.IsFailure)
            {
                // logger.LogDebug("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                continue;
            }

            // logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
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
    public Result<IEnumerable<T>> Filter<T>(IEnumerable<T> items)
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

                var result = Rule.Apply(rule);
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
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result containing the filtered collection.</returns>
    /// <example>
    /// <code>
    /// var result = await Rules.For()
    ///     .Add(User u => RuleSet.IsNotEmpty(u.Email))
    ///     .Add(User u => RuleSet.IsValidEmail(u.Email))
    ///     .When(isActive, builder => builder
    ///         .Add(User u => RuleSet.IsNotNull(u.LastLoginDate)))
    ///     .FilterAsync(users, cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<T>>> FilterAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken = default)
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

                var result = await Rule.ApplyAsync(rule, false, false, cancellationToken).AnyContext();
                if (result.IsFailure)
                {
                    return Result.Failure();
                }
            }

            return Result.Success();
        }
    }

    /// <summary>
    /// Applies the rules with configurable failure handling.
    /// </summary>
    /// <param name="throwOnRuleFailure">When true, failures throw exceptions. When false, failures return Result.Failure</param>
    /// <returns>A Result indicating success or failure</returns>
    /// <exception>When validation fails and throwOnRuleFailure is true
    ///     <cref>RuleValidationException</cref>
    /// </exception>
    /// <exception cref="RuleException">When rule execution throws an error</exception>
    /// <example>
    /// <code>
    /// var result = Rule.For()
    ///     .Add(RuleSet.IsNotEmpty(product.Name))
    ///     .Add(RuleSet.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .When(!product.IsDigital, builder => builder
    ///         .Add(RuleSet.IsNotEmpty(product.ShippingAddress)))
    ///     .Throw(); // Throws if validation fails
    /// </code>
    /// </example>
    public Result Throw(bool throwOnRuleFailure = true)
    {
        return this
            .ThrowOnFailure(throwOnRuleFailure)
            .ThrowOnException()
            .Apply();
    }

    /// <summary>
    /// Asynchronously applies the rules with configurable failure handling.
    /// </summary>
    /// <param name="throwOnRuleFailure">When true, failures throw exceptions. When false, failures return Result.Failure</param>
    /// <returns>A Task containing a Result indicating success or failure</returns>
    /// <exception>When validation fails and throwOnRuleFailure is true</exception>
    /// <exception cref="RuleException">When rule execution throws an error</exception>
    /// <example>
    /// <code>
    /// var result = await Rule.For()
    ///     .Add(RuleSet.IsNotEmpty(order.CustomerId))
    ///     .Add(RuleSet.All(order.Items, item =>
    ///         RuleSet.GreaterThan(item.Quantity, 0)))
    ///     .WhenAsync(
    ///         async (token) => await IsCustomerActive(order.CustomerId, token),
    ///         RuleSet.IsNotNull(order.ShippingAddress))
    ///     .ThrowAsync(); // Throws if validation fails
    /// </code>
    /// </example>
    public async Task<Result> ThrowAsync(bool throwOnRuleFailure = true)
    {
        return await this
            .ThrowOnFailure(throwOnRuleFailure)
            .ThrowOnException()
            .ApplyAsync().AnyContext();
    }
}