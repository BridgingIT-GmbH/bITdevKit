// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides core methods for executing business rules and validation logic.
/// This static class serves as the main entry point for rule execution and validation.
/// </summary>
/// <remarks>
/// The Rule class supports both synchronous and asynchronous rule execution with configurable
/// exception handling and logging behavior.
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// Rule.Setup(settings => settings
///     .ThrowOnRuleFailure(false)
///     .ThrowOnRuleException(true))
///     .UseLogger(logger)
///
/// var result = await Rule.Check(new MinimumAgeRule(person));
/// var result = await Rule.Throw(new MinimumAgeRule(person));
/// </code>
/// </example>
public static partial class Rule
{
    public static RuleSettings Settings { get; private set; }

    static Rule()
    {
        Settings = new RuleSettingsBuilder().Build();
    }

    /// <summary>
    /// Configures the global settings for the <see cref="Rule"/> type.
    /// </summary>
    /// <param name="settings">A delegate to configure the <see cref="RuleSettingsBuilder"/>.</param>
    public static void Setup(Action<RuleSettingsBuilder> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var builder = new RuleSettingsBuilder();
        settings(builder);

        Settings = builder.Build();
    }

    /// <summary>
    /// Applies a single rule synchronously to validate a value or state.
    /// Returns a success result if the rule passes, or a failure result if it fails.
    /// </summary>
    /// <param name="rule">The rule to apply.</param>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception if the rule fails.</param>
    /// <param name="throwOnRuleException">Indicates whether to throw an exception if the rule throws an exception.</param>
    /// <returns>A Result indicating success or failure of the rule.</returns>
    /// <example>
    /// <code>
    /// var nameRule = RuleSet.IsNotEmpty(user.Name);
    /// var result = Rules.Check(nameRule);
    ///
    /// if (result.IsSuccess)
    /// {
    ///     Validation passed
    /// }
    /// </code>
    /// </example>
    public static Result Check(IRule rule, bool? throwOnRuleFailure = null, bool? throwOnRuleException = null)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        var settings = Settings;
        var shouldThrowOnFailure = throwOnRuleFailure ?? settings.ThrowOnRuleFailure;
        var shouldThrowOnException = throwOnRuleException ?? settings.ThrowOnRuleException;

        try
        {
            var result = rule.IsSatisfied();
            //Settings.Logger?.Log(string.Empty, "applied", rule, result, LogLevel.Debug);

            return HandleResult(rule, result, shouldThrowOnFailure);
        }
        catch (RuleException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HandleException(rule, ex, shouldThrowOnException);
        }
    }

    /// <summary>
    /// Applies a rule defined by a boolean expression synchronously and returns the result.
    /// </summary>
    /// <param name="expression">The boolean expression to evaluate.</param>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception if the rule fails.</param>
    /// <param name="throwOnRuleException">Indicates whether to throw an exception if the rule throws an exception.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the rule.</returns>
    /// <example>
    /// <code>
    /// var result = Rule.Check(() => user.Age > 18);
    /// if (result.IsSuccess)
    /// {
    ///     // Validation passed
    /// }
    /// </code>
    /// </example>
    public static Result Check(Func<bool> expression, bool? throwOnRuleFailure = null, bool? throwOnRuleException = null)
    {
        return expression is null ? Result.Success() : Check(new FuncRule(expression), throwOnRuleFailure, throwOnRuleException);
    }

    /// <summary>
    /// Applies a single rule asynchronously to validate a value or state.
    /// Returns a success result if the rule passes, or a failure result if it fails.
    /// Supports both synchronous and asynchronous rules.
    /// </summary>
    /// <param name="rule">The rule to check.</param>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception if the rule fails.</param>
    /// <param name="throwOnRuleException">Indicates whether to throw an exception if the rule throws an exception.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the Result indicating success or failure of the rule.</returns>
    /// <example>
    /// <code>
    /// var userExistsRule = new UserExistsRule(userId);
    /// var result = await Rules.CheckAsync(userExistsRule, cancellationToken);
    ///
    /// if (result.IsSuccess)
    /// {
    ///      Validation passed
    /// }
    /// </code>
    /// </example>
    public static async Task<Result> CheckAsync(IRule rule, bool? throwOnRuleFailure = null, bool? throwOnRuleException = null, CancellationToken cancellationToken = default)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        // Cache settings locally to avoid multiple property access
        var settings = Settings;
        var shouldThrowOnFailure = throwOnRuleFailure ?? settings.ThrowOnRuleFailure;
        var shouldThrowOnException = throwOnRuleException ?? settings.ThrowOnRuleException;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = rule is AsyncRuleBase asyncRule
                ? await asyncRule.IsSatisfiedAsync(cancellationToken).AnyContext()
                : rule.IsSatisfied();

            return HandleResult(rule, result, shouldThrowOnFailure);
        }
        catch (RuleException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HandleException(rule, ex, shouldThrowOnException);
        }
    }

    /// <summary>
    /// Applies a rule defined by a boolean expression asynchronously and returns the result.
    /// </summary>
    /// <param name="expression">The boolean expression to evaluate.</param>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception if the rule fails.</param>
    /// <param name="throwOnRuleException">Indicates whether to throw an exception if the rule throws an exception.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the <see cref="Result"/> indicating success or failure of the rule.</returns>
    /// <example>
    /// <code>
    /// var result = await Rule.CheckAsync(() => CheckUserPermissions(userId));
    /// if (result.IsSuccess)
    /// {
    ///     // Validation passed
    /// }
    /// </code>
    /// </example>
    public static async Task<Result> CheckAsync(
        Func<bool> expression,
        bool? throwOnRuleFailure = null,
        bool? throwOnRuleException = null,
        CancellationToken cancellationToken = default)
    {
        return await CheckAsync(
            new FuncRule(expression),
            throwOnRuleFailure,
            throwOnRuleException,
            cancellationToken).AnyContext();
    }

    /// <summary>
    /// Applies a rule and always converts failures into exceptions.
    /// </summary>
    /// <param name="rule">The rule to validate</param>
    /// <param name="throwOnRuleFailure"></param>
    /// <returns>A success Result if validation passes</returns>
    /// <exception cref="RuleException">When rule execution throws an error</exception>
    /// <example>
    /// <code>
    /// var result = Rule.Throw(new MinAgeRule(person));
    /// Throws if validation fails, otherwise returns success
    /// </code>
    /// </example>
    public static Result Throw(IRule rule, bool throwOnRuleFailure = true)
    {
        return Check(rule, throwOnRuleFailure, true);
    }

    /// <summary>
    /// Applies a rule defined by a boolean expression synchronously and throws on failure.
    /// </summary>
    /// <param name="expression">The boolean expression to evaluate.</param>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception if the rule fails.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the rule.</returns>
    /// <exception>Thrown when rule validation fails and <paramref name="throwOnRuleFailure"/> is true.</exception>
    /// <exception cref="RuleException">Thrown when rule execution throws an error.</exception>
    /// <example>
    /// <code>
    /// Rule.Throw(() => user.Age > 18);
    /// </code>
    /// </example>
    public static Result Throw(Func<bool> expression, bool throwOnRuleFailure = true)
    {
        return Throw(new FuncRule(expression), throwOnRuleFailure);
    }

    /// <summary>
    /// Asynchronously applies a rule with configurable failure handling.
    /// </summary>
    /// <param name="rule">The rule to validate</param>
    /// <param name="throwOnRuleFailure">When true, failures throw exceptions. When false, failures return Result.Failure</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A Task containing a Result indicating success or failure</returns>
    /// <exception>When validation fails and throwOnRuleFailure is true</exception>
    /// <exception cref="RuleException">When rule execution throws an error</exception>
    /// <example>
    /// <code>
    /// Return failure Result instead of throwing
    /// var result = await Rule.ThrowAsync(new AsyncMinAgeRule(person), throwOnRuleFailure: false);
    /// if(result.IsFailure) {
    ///      Handle validation failure
    /// }
    /// </code>
    /// </example>
    public async static Task<Result> ThrowAsync(IRule rule, bool throwOnRuleFailure = true, CancellationToken cancellationToken = default)
    {
        return await CheckAsync(rule, throwOnRuleFailure, true, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Applies a rule defined by a boolean expression asynchronously and throws on failure.
    /// </summary>
    /// <param name="expression">The boolean expression to evaluate.</param>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception if the rule fails.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the <see cref="Result"/> indicating success or failure of the rule.</returns>
    /// <exception>Thrown when rule validation fails and <paramref name="throwOnRuleFailure"/> is true.</exception>
    /// <exception cref="RuleException">Thrown when rule execution throws an error.</exception>
    /// <example>
    /// <code>
    /// await Rule.ThrowAsync(() => CheckUserPermissions(userId));
    /// </code>
    /// </example>
    public static async Task<Result> ThrowAsync(Func<bool> expression, bool throwOnRuleFailure = true, CancellationToken cancellationToken = default)
    {
        return await ThrowAsync(new FuncRule(expression), throwOnRuleFailure, cancellationToken).AnyContext();
    }

    private static Result HandleResult(IRule rule, Result result, bool throwOnRuleFailure)
    {
        if (result.IsFailure)
        {
            if (throwOnRuleFailure)
            {
                var ruleException = Settings.RuleFailureExceptionFactory ??= r => new RuleException(r, result.Errors.ToString(", "));

                throw ruleException(rule);
            }

            result = result.HasError() ? result : result.WithError(new RuleError(rule));
        }

        Settings.Logger?.Log(string.Empty, result.IsSuccess ? "success" : "failure", rule, result, LogLevel.Debug);

        return result;
    }

    private static Result HandleException(IRule rule, Exception exception, bool? throwOnRuleException)
    {
        if (throwOnRuleException == true)
        {
            var ruleException = Settings.RuleFailureExceptionFactory ??= r => new RuleException(r, string.Empty, exception);

            throw ruleException(rule);
        }

        var error = Settings.RuleExceptionErrorFactory ??= (r, ex) => new RuleExceptionError(r, ex);

        var result = Result.Failure()
            .WithMessage(exception.Message)
            .WithError(error(rule, exception));

        Settings.Logger?.Log(string.Empty, "failure", rule, result, LogLevel.Debug);

        return result;
    }
}