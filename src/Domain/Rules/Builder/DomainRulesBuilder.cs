// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Provides a fluent interface for building and executing collections of domain rules.
/// </summary>
public class DomainRulesBuilder
{
    private readonly List<IDomainRule> rules = [];
    private bool aggregateErrors;

    /// <summary>
    /// Adds a rule to the builder.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder Add(IDomainRule rule)
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
    public DomainRulesBuilder Add(params IDomainRule[] rules)
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
    public static DomainRulesBuilder For()
    {
        return new DomainRulesBuilder();
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rule.
    /// </summary>
    /// <param name="rule">The initial rule.</param>
    /// <returns>A new rule builder instance.</returns>
    public static DomainRulesBuilder For(IDomainRule rule)
    {
        return new DomainRulesBuilder().Add(rule);
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rule.
    /// </summary>
    /// <param name="rules">The initial rules.</param>
    /// <returns>A new rule builder instance.</returns>
    public static DomainRulesBuilder For(params IDomainRule[] rules)
    {
        var builder = new DomainRulesBuilder();
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
    public DomainRulesBuilder WithErrorAggregation()
    {
        this.aggregateErrors = true;

        return this;
    }

    /// <summary>
    /// Adds a rule when the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="rule">The rule to add if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder When(bool condition, IDomainRule rule)
    {
        return condition ? this.Add(rule) : this;
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder When(bool condition, Action<DomainRulesBuilder> addRules)
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
    public DomainRulesBuilder Unless(bool condition, IDomainRule rule)
    {
        return this.When(!condition, rule);
    }

    /// <summary>
    /// Adds multiple rules when the specified condition is false.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="addRules">Action to add rules if the condition is false.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder Unless(bool condition, Action<DomainRulesBuilder> addRules)
    {
        return this.When(!condition, addRules);
    }

    /// <summary>
    /// Adds a rule with an asynchronous condition.
    /// </summary>
    /// <param name="condition">An async function that determines if the rule should be executed.</param>
    /// <param name="rule">The rule to execute if the condition is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, IDomainRule rule)
    {
        return this.Add(new AsyncConditionalRule(condition, rule));
    }

    /// <summary>
    /// Adds multiple rules with an asynchronous condition.
    /// </summary>
    /// <param name="condition">An async function that determines if the rules should be executed.</param>
    /// <param name="addRules">Action to add rules if the condition is met.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder WhenAsync(Func<CancellationToken, Task<bool>> condition, Action<DomainRulesBuilder> addRules)
    {
        var builder = new DomainRulesBuilder();
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
    public DomainRulesBuilder WhenAll(IEnumerable<bool> conditions, IDomainRule rule)
    {
        return this.When(conditions.All(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when all of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if all conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder WhenAll(IEnumerable<bool> conditions, Action<DomainRulesBuilder> addRules)
    {
        return this.When(conditions.All(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule when any of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder WhenAny(IEnumerable<bool> conditions, IDomainRule rule)
    {
        return this.When(conditions.Any(c => c), rule);
    }

    /// <summary>
    /// Adds multiple rules when any of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="addRules">Action to add rules if any condition is true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder WhenAny(IEnumerable<bool> conditions, Action<DomainRulesBuilder> addRules)
    {
        return this.When(conditions.Any(c => c), addRules);
    }

    /// <summary>
    /// Adds a rule when none of the specified conditions are true.
    /// </summary>
    /// <param name="conditions">The conditions to evaluate.</param>
    /// <param name="rule">The rule to add if no conditions are true.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public DomainRulesBuilder WhenNone(IEnumerable<bool> conditions, IDomainRule rule)
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
    public DomainRulesBuilder WhenExactly(int count, IEnumerable<bool> conditions, IDomainRule rule)
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
    public DomainRulesBuilder WhenAtLeast(int count, IEnumerable<bool> conditions, IDomainRule rule)
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
    public DomainRulesBuilder WhenAtMost(int count, IEnumerable<bool> conditions, IDomainRule rule)
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
    public DomainRulesBuilder WhenBetween(int min, int max, IEnumerable<bool> conditions, IDomainRule rule)
    {
        var trueCount = conditions.Count(c => c);

        return this.When(trueCount >= min && trueCount <= max, rule);
    }

    /// <summary>
    /// Applies the domain rules added to the builder.
    /// </summary>
    /// <param name="logger">Optional logger to use for logging rule execution outcomes. Defaults to a null logger if not provided.</param>
    /// <returns>A Result object indicating the success or failure of applying the rules.</returns>
    public Result Apply(ILogger logger = null)
    {
        logger ??= NullLogger.Instance;

        if (!this.rules.Any())
        {
            logger.LogInformation("{LogKey} domain rules - no rules defined", Constants.LogKey);

            return Result.Success();
        }

        if (!this.aggregateErrors)
        {
            foreach (var rule in this.rules) // Execute each rule in sequence, stopping at first failure
            {
                var result = DomainRules.Apply(rule);  // catches exceptions
                if (result.IsFailure)
                {
                    logger.LogWarning("{LogKey} domain rules - {DomainRule} result: {DomainRuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                    return result;
                }

                logger.LogInformation("{LogKey} domain rules - {DomainRule} result: {DomainRuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
            }

            return Result.Success();
        }

        var errors = new List<IResultError>(); // Aggregate all errors from all rules
        var hasFailures = false;

        foreach (var rule in this.rules)
        {
            var result = DomainRules.Apply(rule);  // catches exceptions
            if (!result.IsFailure)
            {
                logger.LogInformation("{LogKey} domain rules - {DomainRule} result: {DomainRuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                continue;
            }

            logger.LogWarning("{LogKey} domain rules - {DomainRule} result: {DomainRuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
            hasFailures = true;
            errors.AddRange(result.Errors);
        }

        return hasFailures
            ? Result.Failure().WithErrors(errors)
            : Result.Success();
    }

    /// <summary>
    /// Executes all rules asynchronously.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the result of the rule executions.</returns>
    public async Task<Result> ApplyAsync(ILogger logger = null, CancellationToken cancellationToken = default)
    {
        logger ??= NullLogger.Instance;

        if (!this.rules.Any())
        {
            logger.LogInformation("{LogKey} domain rules - no rules defined", Constants.LogKey);

            return Result.Success();
        }

        if (!this.aggregateErrors)
        {
            foreach (var rule in this.rules) // Execute each rule in sequence, stopping at first failure
            {
                var result = await DomainRules.ApplyAsync(rule, cancellationToken); // catches exceptions
                if (result.IsFailure)
                {
                    logger.LogWarning("{LogKey} domain rules - {DomainRule} result: {DomainRuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                    return result;
                }

                logger.LogInformation("{LogKey} domain rules - {DomainRule} result: {DomainRuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
            }

            return Result.Success();
        }

        var errors = new List<IResultError>(); // Aggregate all errors from all rules
        var hasFailures = false;

        foreach (var rule in this.rules)
        {
            var result = await DomainRules.ApplyAsync(rule, cancellationToken); // catches exceptions
            if (!result.IsFailure)
            {
                logger.LogInformation("{LogKey} domain rules - {DomainRule} result: {DomainRuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());

                continue;
            }

            logger.LogWarning("{LogKey} domain rules - {DomainRule} result: {DomainRuleResult}", Constants.LogKey, rule.GetType().Name, result.ToString());
            hasFailures = true;
            errors.AddRange(result.Errors);
        }

        return hasFailures
            ? Result.Failure().WithErrors(errors)
            : Result.Success();
    }
}