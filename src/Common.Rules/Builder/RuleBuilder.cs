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
    /// var builder = Rule
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .Add(Rules.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .Check();
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
    /// var builder = Rule
    ///     .Add(Rules.IsNotEmpty(product.Name), Rules.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .Check();
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
    /// var builder = Rule
    ///     .Add(() => user.IsActive)
    ///     .Check();
    /// </code>
    /// </example>
    public RuleBuilder Add(Func<bool> predicate, string message = null)
    {
        return this.Add(new FuncRule(predicate, message));
    }

    /// <summary>
    /// Adds multiple rules defined by boolean predicates to the builder.
    /// </summary>
    /// <param name="predicates">The boolean predicates to evaluate.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = Rule
    ///     .Add(() => user.IsActive, () => user.HasValidSubscription)
    ///     .Check();
    /// </code>
    /// </example>
    public RuleBuilder Add(params Func<bool>[] predicates)
    {
        foreach (var predicate in predicates)
        {
            this.Add(new FuncRule(predicate));
        }

        return this;
    }

    /// <summary>
    /// Enables error aggregation, collecting all rule failures instead of stopping at the first failure.
    /// </summary>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = Rule
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .ContinueOnFailure()
    ///     .Check();
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
    /// var result = Rule
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .ThrowOnFailure(true)
    ///     .Check();
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
    /// var result = Rule
    ///     .Add(Rules.IsNotEmpty(product.Name))
    ///     .ThrowOnException(true)
    ///     .Check();
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
    /// var result = Rules
    ///     .Add(RuleSet.IsNotEmpty(product.Name))
    ///     .Add(RuleSet.NumericRange(product.Price, 0.01m, 999.99m))
    ///     .When(!product.IsDigital, builder => builder
    ///         .Add(RuleSet.IsNotEmpty(product.ShippingAddress))
    ///         .Add(RuleSet.HasStringLength(product.ShippingAddress, 10, 200)))
    ///     .ContinueOnFailure()
    ///     .Check();
    /// </code>
    /// </example>
    public Result Check()
    {
        if (this.rules.Count == 0)
        {
            return Result.Success();
        }

        var messages = new List<string>();
        var errors = new List<IResultError>();
        var hasFailures = false;

        if (!this.continueOnRuleFailure)
        {
            foreach (var rule in this.rules)
            {
                var result = Rule.Check(rule, this.throwOnFailure, this.throwOnException);
                if (result.IsFailure)
                {
                    return result;
                }

                messages.AddRange(result.Messages);
                errors.AddRange(result.Errors);
            }

            return Result.Success()
                .WithMessages(messages).WithErrors(errors);
        }

        foreach (var rule in this.rules)
        {
            var result = Rule.Check(rule, false);
            if (!result.IsFailure)
            {
                //logger.LogDebug("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                continue;
            }

            //logger.LogWarning("{LogKey} rules - {Rule} result: {RuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

            hasFailures = true;
            messages.AddRange(result.Messages);
            errors.AddRange(result.Errors);
        }

        return hasFailures
            ? Result.Failure().WithMessages(messages).WithErrors(errors)
            : Result.Success().WithMessages(messages).WithErrors(errors);
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
    /// var result = await Rules
    ///     .Add(RuleSet.IsNotEmpty(order.CustomerId))
    ///     .Add(RuleSet.All(order.Items, item =>
    ///         RuleSet.GreaterThan(item.Quantity, 0)))
    ///     .WhenAsync(
    ///         async (token) => await IsCustomerActive(order.CustomerId, token),
    ///         RuleSet.IsNotNull(order.ShippingAddress))
    ///     .ContinueOnFailure()
    ///     .CheckAsync(cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (this.rules.Count == 0)
        {
            // logger.LogDebug("{LogKey} rules - no rules defined", Constants.LogKey);

            return Result.Success();
        }

        if (!this.continueOnRuleFailure)
        {
            foreach (var rule in this.rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await Rule.CheckAsync(rule, this.throwOnFailure, this.throwOnException, cancellationToken).AnyContext();
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

            var result = await Rule.CheckAsync(rule, this.throwOnFailure, this.throwOnException, cancellationToken).AnyContext();
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

    // /// <summary>
    // /// Filters a collection of items based on the defined rules synchronously.
    // /// Items are excluded from the result if they fail any rule validation.
    // /// </summary>
    // /// <typeparam name="T">The type of items to filter.</typeparam>
    // /// <param name="items">The collection of items to filter.</param>
    // /// <returns>A Result containing the filtered collection.</returns>
    // /// <example>
    // /// <code>
    // /// var result = Rules
    // ///     .Add(Product p => RuleSet.IsNotEmpty(p.Name))
    // ///     .Add(Product p => RuleSet.NumericRange(p.Price, 0.01m, 999.99m))
    // ///     .When(!isDigital, builder => builder
    // ///         .Add(Product p => RuleSet.IsNotEmpty(p.ShippingAddress)))
    // ///     .Filter(products);
    // /// </code>
    // /// </example>
    // public Result<IEnumerable<T>> Filter<T>(IEnumerable<T> items)
    // {
    //     if (this.rules.Count == 0)
    //     {
    //         return HandleEmptyInput();
    //     }
    //
    //     var filteredItems = new List<T>();
    //
    //     foreach (var item in items)
    //     {
    //         var result = EvaluateRules(item);
    //         if (result.IsSuccess)
    //         {
    //             filteredItems.Add(item);
    //         }
    //     }
    //
    //     return Result<IEnumerable<T>>.Success(filteredItems);
    //
    //     Result<IEnumerable<T>> HandleEmptyInput()
    //     {
    //         return items is null
    //             ? Result<IEnumerable<T>>.Success(Array.Empty<T>())
    //             : Result<IEnumerable<T>>.Success(items);
    //     }
    //
    //     Result EvaluateRules(T item)
    //     {
    //         foreach (var rule in this.rules)
    //         {
    //             if (rule is IItemRule<T> itemRule)
    //             {
    //                 itemRule.SetItem(item);
    //             }
    //
    //             var result = Rule.Check(rule);
    //             if (result.IsFailure)
    //             {
    //                 return Result.Failure();
    //             }
    //         }
    //
    //         return Result.Success();
    //     }
    // }
    //
    // /// <summary>
    // /// Filters a collection of items based on the defined rules asynchronously.
    // /// Items are excluded from the result if they fail any rule validation.
    // /// </summary>
    // /// <typeparam name="T">The type of items to filter.</typeparam>
    // /// <param name="items">The collection of items to filter.</param>
    // /// <param name="cancellationToken">A token to cancel the operation.</param>
    // /// <returns>A Result containing the filtered collection.</returns>
    // /// <example>
    // /// <code>
    // /// var result = await Rules
    // ///     .Add(User u => RuleSet.IsNotEmpty(u.Email))
    // ///     .Add(User u => RuleSet.IsValidEmail(u.Email))
    // ///     .When(isActive, builder => builder
    // ///         .Add(User u => RuleSet.IsNotNull(u.LastLoginDate)))
    // ///     .FilterAsync(users, cancellationToken);
    // /// </code>
    // /// </example>
    // public async Task<Result<IEnumerable<T>>> FilterAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken = default)
    // {
    //     if (this.rules.Count == 0)
    //     {
    //         return HandleEmptyInput();
    //     }
    //
    //     var filteredItems = new List<T>();
    //
    //     foreach (var item in items)
    //     {
    //         cancellationToken.ThrowIfCancellationRequested();
    //
    //         var result = await EvaluateRules(item);
    //         if (result.IsSuccess)
    //         {
    //             filteredItems.Add(item);
    //         }
    //     }
    //
    //     return Result<IEnumerable<T>>.Success(filteredItems);
    //
    //     Result<IEnumerable<T>> HandleEmptyInput()
    //     {
    //         return items is null
    //             ? Result<IEnumerable<T>>.Success(Array.Empty<T>())
    //             : Result<IEnumerable<T>>.Success(items);
    //     }
    //
    //     async Task<Result> EvaluateRules(T item)
    //     {
    //         foreach (var rule in this.rules)
    //         {
    //             cancellationToken.ThrowIfCancellationRequested();
    //
    //             if (rule is IItemRule<T> itemRule)
    //             {
    //                 itemRule.SetItem(item);
    //             }
    //
    //             var result = await Rule.CheckAsync(rule, false, false, cancellationToken).AnyContext();
    //             if (result.IsFailure)
    //             {
    //                 return Result.Failure();
    //             }
    //         }
    //
    //         return Result.Success();
    //     }
    // }

    /// <summary>
    /// Splits the items into two groups based on the defined rules and processes them with separate handlers.
    /// </summary>
    /// <typeparam name="T">The type of items to process.</typeparam>
    /// <param name="items">The collection of items to filter.</param>
    /// <param name="matchHandler">Handler for items that match all rules.</param>
    /// <param name="unmatchHandler">Handler for items that fail any rule.</param>
    /// <returns>A Result indicating success of both handlers or failure with combined errors.</returns>
    /// <example>
    /// <code>
    /// var result = Rule
    ///     .Add&lt;Product&gt;(p => RuleSet.GreaterThan(p.Price, 10))
    ///     .Add&lt;Product&gt;(p => RuleSet.IsNotEmpty(p.Name))
    ///     .Switch(products,
    ///         validProducts => ProcessValidProducts(validProducts),
    ///         invalidProducts => ProcessInvalidProducts(invalidProducts));
    /// </code>
    /// </example>
    public Result Switch<T>(
        IEnumerable<T> items,
        Func<IEnumerable<T>, Result> matchHandler,
        Func<IEnumerable<T>, Result> unmatchHandler)
    {
        var (matchedItems, unmachedItems) = this.Classify(items);

        var matchResult = matchHandler(matchedItems);
        var unmatchResult = unmatchHandler(unmachedItems);

        return Result.Combine(matchResult, unmatchResult);
    }

    /// <summary>
    /// Asynchronously splits the items into two groups based on the defined rules and processes them with separate handlers.
    /// </summary>
    /// <typeparam name="T">The type of items to process.</typeparam>
    /// <param name="items">The collection of items to filter.</param>
    /// <param name="matchHandler">Async handler for items that match all rules.</param>
    /// <param name="unmatchHandler">Async handler for items that fail any rule.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task containing a Result indicating success of both handlers or failure with combined errors.</returns>
    /// <example>
    /// <code>
    /// var result = await Rule
    ///     .Add&lt;Product&gt;(p => RuleSet.GreaterThan(p.Price, 10))
    ///     .Add&lt;Product&gt;(p => RuleSet.IsNotEmpty(p.Name))
    ///     .SwitchAsync(products,
    ///         async validProducts => await ProcessValidProductsAsync(validProducts),
    ///         async invalidProducts => await ProcessInvalidProductsAsync(invalidProducts));
    /// </code>
    /// </example>
    public async Task<Result> SwitchAsync<T>(
        IEnumerable<T> items,
        Func<IEnumerable<T>, Task<Result>> matchHandler,
        Func<IEnumerable<T>, Task<Result>> unmatchHandler,
        CancellationToken cancellationToken = default)
    {
        var (matchedItems, unMatchedItems) = await this.ClassifyAsync(items, cancellationToken);

        var matchResult = await matchHandler(matchedItems);
        var unmatchResult = await unmatchHandler(unMatchedItems);

        return Result.Combine(matchResult, unmatchResult);
    }

    public async Task<Result> SwitchAsync<T>(
        IEnumerable<T> items,
        Func<IEnumerable<T>, Result> matchHandler,
        Func<IEnumerable<T>, Result> unmatchHandler,
        CancellationToken cancellationToken = default)
    {
        var (matchedItems, unMatchedItems) = await this.ClassifyAsync(items, cancellationToken);

        var matchResult = matchHandler(matchedItems);
        var unmatchResult = unmatchHandler(unMatchedItems);

        return Result.Combine(matchResult, unmatchResult);
    }

    /// <summary>
    /// Filters a collection of items based on the defined rules.
    /// </summary>
    /// <typeparam name="T">The type of items to filter.</typeparam>
    /// <param name="items">The collection of items to filter.</param>
    /// <returns>A Result containing the filtered collection.</returns>
    /// <example>
    /// <code>
    /// var result = Rule
    ///     .Add&lt;Product&gt;(p => RuleSet.GreaterThan(p.Price, 10))
    ///     .Add&lt;Product&gt;(p => RuleSet.IsNotEmpty(p.Name))
    ///     .Filter(products);
    ///
    /// if (result.IsSuccess)
    /// {
    ///     var validProducts = result.Value;
    ///     // Process valid products...
    /// }
    /// </code>
    /// </example>
    public Result<IEnumerable<T>> Filter<T>(IEnumerable<T> items)
    {
        var (matchedItems, _) = this.Classify(items);

        return Result<IEnumerable<T>>.Success(matchedItems);
    }

    /// <summary>
    /// Asynchronously filters a collection of items based on the defined rules.
    /// </summary>
    /// <typeparam name="T">The type of items to filter.</typeparam>
    /// <param name="items">The collection of items to filter.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task containing the Result with the filtered collection.</returns>
    /// <example>
    /// <code>
    /// var result = await Rule
    ///     .Add&lt;Product&gt;(p => RuleSet.GreaterThan(p.Price, 10))
    ///     .Add&lt;Product&gt;(p => RuleSet.IsNotEmpty(p.Name))
    ///     .FilterAsync(products);
    ///
    /// if (result.IsSuccess)
    /// {
    ///     var validProducts = result.Value;
    ///     // Process valid products...
    /// }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<T>>> FilterAsync<T>(
        IEnumerable<T> items,
        CancellationToken cancellationToken = default)
    {
        var (matchedItems, _) = await this.ClassifyAsync(items, cancellationToken);

        return Result<IEnumerable<T>>.Success(matchedItems);
    }

    private (IEnumerable<T> ValidItems, IEnumerable<T> InvalidItems) Classify<T>(IEnumerable<T> items)
    {
        if (this.rules.Count == 0)
        {
            return (new List<T>(items ?? Array.Empty<T>()), new List<T>());
        }

        var matchedItems = new List<T>();
        var unMatchedItems = new List<T>();

        foreach (var item in items ?? Array.Empty<T>())
        {
            var isValid = true;
            foreach (var rule in this.rules)
            {
                if (rule is IItemRule<T> itemRule)
                {
                    itemRule.SetItem(item);
                }

                var result = rule.IsSatisfied();
                if (result.IsFailure)
                {
                    isValid = false;

                    break;
                }
            }

            if (isValid)
            {
                matchedItems.Add(item);
            }
            else
            {
                unMatchedItems.Add(item);
            }
        }

        return (matchedItems, unMatchedItems);
    }

    private async Task<(List<T> ValidItems, List<T> InvalidItems)> ClassifyAsync<T>(
        IEnumerable<T> items,
        CancellationToken cancellationToken)
    {
        if (this.rules.Count == 0)
        {
            return ([.. items ?? Array.Empty<T>()], []);
        }

        var matchedItems = new List<T>();
        var unMatchedItems = new List<T>();

        foreach (var item in items ?? Array.Empty<T>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isValid = true;
            foreach (var rule in this.rules)
            {
                if (rule is IItemRule<T> itemRule)
                {
                    itemRule.SetItem(item);
                }

                var result = rule is AsyncRuleBase asyncRule
                    ? await asyncRule.IsSatisfiedAsync(cancellationToken)
                    // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                    : rule.IsSatisfied();

                if (result.IsFailure)
                {
                    isValid = false;

                    break;
                }
            }

            if (isValid)
            {
                matchedItems.Add(item);
            }
            else
            {
                unMatchedItems.Add(item);
            }
        }

        return (matchedItems, unMatchedItems);
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
    /// var result = Rule
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
            .Check();
    }

    /// <summary>
    /// Asynchronously applies the rules with configurable failure handling.
    /// </summary>
    /// <param name="throwOnRuleFailure">When true, failures throw exceptions. When false, failures return Result.Failure</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A Task containing a Result indicating success or failure</returns>
    /// <exception>When validation fails and throwOnRuleFailure is true</exception>
    /// <exception cref="RuleException">When rule execution throws an error</exception>
    /// <example>
    /// <code>
    /// var result = await Rule
    ///     .Add(RuleSet.IsNotEmpty(order.CustomerId))
    ///     .Add(RuleSet.All(order.Items, item =>
    ///         RuleSet.GreaterThan(item.Quantity, 0)))
    ///     .WhenAsync(
    ///         async (token) => await IsCustomerActive(order.CustomerId, token),
    ///         RuleSet.IsNotNull(order.ShippingAddress))
    ///     .ThrowAsync(); // Throws if validation fails
    /// </code>
    /// </example>
    public async Task<Result> ThrowAsync(bool throwOnRuleFailure = true, CancellationToken cancellationToken = default)
    {
        return await this
            .ThrowOnFailure(throwOnRuleFailure)
            .ThrowOnException()
            .CheckAsync(cancellationToken).AnyContext();
    }
}