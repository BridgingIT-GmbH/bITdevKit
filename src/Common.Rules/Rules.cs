// // MIT-License
// // Copyright BridgingIT GmbH - All Rights Reserved
// // Use of this source code is governed by an MIT-style license that can be
// // found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
//

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides core methods for executing rules.
/// </summary>
public static partial class Rules
{
    public static RuleSettings Settings { get; private set; }

    static Rules()
    {
        Settings = new RuleSettingsBuilder().Build();
    }

    /// <summary>
    /// Applies a single rule synchronously to validate a value or state.
    /// Returns a success result if the rule passes, or a failure result if it fails.
    /// </summary>
    /// <param name="rule">The rule to apply.</param>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception if the rule fails.</param>
    /// <returns>A Result indicating success or failure of the rule.</returns>
    /// <example>
    /// <code>
    /// // Single rule validation
    /// var nameRule = RuleSet.IsNotEmpty(user.Name);
    /// var result = Rules.Apply(nameRule);
    ///
    /// // Multiple rules with builder
    /// var result = Rules.For()
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
    public static Result Apply(IRule rule, bool? throwOnRuleFailure = null)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        try
        {
            var result = rule.Apply();
            if (result.IsFailure)
            {
                if (throwOnRuleFailure ?? Settings.ThrowOnRuleFailure)
                {
                    var ruleException = Settings.RuleFailureExceptionFactory ??= rule => new RuleException(rule);

                    throw ruleException(rule);
                }

                return result.WithError(new RuleError(rule));
            }

            return result;
        }
        catch (Exception ex)
        {
            if (throwOnRuleFailure ?? Settings.ThrowOnRuleException)
            {
                var ruleException = Settings.RuleFailureExceptionFactory ??= rule => new RuleException(rule, ex);

                throw ruleException(rule);
            }

            // return failure result with exception as error (IResultError)
            var error = Settings.RuleExceptionErrorFactory ??= (rule, ex) => new RuleExceptionError(rule, ex);

            return Result.Failure()
                .WithMessage(ex.Message)
                .WithError(error(rule, ex));
        }
    }

    /// <summary>
    /// Applies a single rule asynchronously to validate a value or state.
    /// Returns a success result if the rule passes, or a failure result if it fails.
    /// Supports both synchronous and asynchronous rules.
    /// </summary>
    /// <param name="rule">The rule to apply.</param>
    /// <param name="throwOnRuleFailure">Indicates whether to throw an exception if the rule fails.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the Result indicating success or failure of the rule.</returns>
    /// <example>
    /// <code>
    /// // Single async rule validation
    /// var userExistsRule = new UserExistsRule(userId);
    /// var result = await Rules.ApplyAsync(userExistsRule, cancellationToken);
    ///
    /// // Multiple rules with async conditions
    /// var result = await Rules.For()
    ///     .Add(RuleSet.IsNotEmpty(order.CustomerId))
    ///     .WhenAsync(
    ///         async (token) => await IsCustomerActive(order.CustomerId, token),
    ///         builder => builder
    ///             .Add(RuleSet.IsNotNull(order.ShippingAddress))
    ///             .Add(RuleSet.All(order.Items, item =>
    ///                 RuleSet.GreaterThan(item.Quantity, 0))))
    ///     .ApplyAsync(cancellationToken);
    ///
    /// if (result.IsSuccess)
    /// {
    ///     // Validation passed
    /// }
    /// </code>
    /// </example>
    public static async Task<Result> ApplyAsync(IRule rule, bool? throwOnRuleFailure = null, CancellationToken cancellationToken = default)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Result result;
            if (rule is AsyncRuleBase)
            {
                result = await rule.ApplyAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                result = rule.Apply();
            }

            if (result.IsFailure)
            {
                if (throwOnRuleFailure ?? Settings.ThrowOnRuleFailure)
                {
                    var ruleException = Settings.RuleFailureExceptionFactory ??= rule => new RuleException(rule);
                    throw ruleException(rule);
                }

                return result.WithError(new RuleError(rule));
            }

            return result;
        }
        catch (Exception ex)
        {
            if (throwOnRuleFailure ?? Settings.ThrowOnRuleException)
            {
                var ruleException = Settings.RuleFailureExceptionFactory ??= rule => new RuleException(rule, ex);
                throw ruleException(rule);
            }

            var error = Settings.RuleExceptionErrorFactory ??= (rule, ex) => new RuleExceptionError(rule, ex);
            return Result.Failure()
                .WithMessage(ex.Message)
                .WithError(error(rule, ex));
        }
    }
}