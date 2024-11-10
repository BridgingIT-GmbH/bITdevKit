// // MIT-License
// // Copyright BridgingIT GmbH - All Rights Reserved
// // Use of this source code is governed by an MIT-style license that can be
// // found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
//

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides core methods for executing rules.
/// </summary>
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

    public static Result Throw(IRule rule)
    {
        return Apply(rule, true, true);
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
    /// var result = Rules.Apply(nameRule);
    ///
    /// if (result.IsSuccess)
    /// {
    ///     // Validation passed
    /// }
    /// </code>
    /// </example>
    public static Result Apply(IRule rule, bool? throwOnRuleFailure = null, bool? throwOnRuleException = null)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        throwOnRuleFailure ??= Settings.ThrowOnRuleFailure;
        throwOnRuleException ??= Settings.ThrowOnRuleException;

        try
        {
            var result = rule.Apply();
            //Settings.Logger?.Log(string.Empty, "applied", rule, result, LogLevel.Debug);

            return HandleResult(rule, result, throwOnRuleFailure);
        }
        catch (Exception ex) when (!(ex is RuleException))
        {
            return HandleException(rule, ex, throwOnRuleException);
        }
    }

    public async static Task<Result> ThrowAsync(IRule rule)
    {
        return await ApplyAsync(rule, true, true).AnyContext();
    }

    /// <summary>
    /// Applies a single rule asynchronously to validate a value or state.
    /// Returns a success result if the rule passes, or a failure result if it fails.
    /// Supports both synchronous and asynchronous rules.
    /// </summary>
    /// <param name="rule">The rule to apply.</param>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception if the rule fails.</param>
    /// <param name="throwOnRuleException">Indicates whether to throw an exception if the rule throws an exception.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the Result indicating success or failure of the rule.</returns>
    /// <example>
    /// <code>
    /// var userExistsRule = new UserExistsRule(userId);
    /// var result = await Rules.ApplyAsync(userExistsRule, cancellationToken);
    ///
    /// if (result.IsSuccess)
    /// {
    ///     // Validation passed
    /// }
    /// </code>
    /// </example>
    public static async Task<Result> ApplyAsync(IRule rule, bool? throwOnRuleFailure = null, bool? throwOnRuleException = null, CancellationToken cancellationToken = default)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        cancellationToken.ThrowIfCancellationRequested();
        throwOnRuleFailure ??= Settings.ThrowOnRuleFailure;
        throwOnRuleException ??= Settings.ThrowOnRuleException;

        try
        {
            Result result;
            if (rule is AsyncRuleBase)
            {
                result = await rule.ApplyAsync(cancellationToken).AnyContext();
            }
            else
            {
                result = rule.Apply();
            }
            //Settings.Logger?.Log(string.Empty, "applied", rule, result, LogLevel.Debug);

            return HandleResult(rule, result, throwOnRuleFailure);
        }
        catch (Exception ex) when (!(ex is RuleException))
        {
            return HandleException(rule, ex, throwOnRuleException);
        }
    }

    private static Result HandleResult(IRule rule, Result result, bool? throwOnRuleFailure)
    {
        if (result.IsFailure)
        {
            if (throwOnRuleFailure == true)
            {
                var ruleException = Settings.RuleFailureExceptionFactory ??= r => new RuleException(r, result.Errors.ToString(", "));

                throw ruleException(rule);
            }

            result = result.HasError() ? result : result.WithError(new RuleError(rule));
        }

        Settings.Logger?.Log(string.Empty, "applied", rule, result, LogLevel.Debug);

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

        Settings.Logger?.Log(string.Empty, "exception", rule, result, LogLevel.Debug);

        return result;
    }
}