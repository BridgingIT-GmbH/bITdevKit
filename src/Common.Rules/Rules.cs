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
    /// <summary>
    /// Applies a single rule synchronously to validate a value or state.
    /// Returns a success result if the rule passes, or a failure result if it fails.
    /// </summary>
    /// <param name="rule">The rule to apply.</param>
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
    public static Result Apply(IRule rule)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        var result = rule.Apply();
        if (result.IsFailure && !result.HasError())
        {
            result.WithError(new RuleError(rule));
        }

        return result;
    }

    /// <summary>
    /// Applies a single rule asynchronously to validate a value or state.
    /// Returns a success result if the rule passes, or a failure result if it fails.
    /// Supports both synchronous and asynchronous rules.
    /// </summary>
    /// <param name="rule">The rule to apply.</param>
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
    public static async Task<Result> ApplyAsync(IRule rule, CancellationToken cancellationToken = default)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        cancellationToken.ThrowIfCancellationRequested();

        Result result;
        if (rule is AsyncRuleBase)
        {
            result = await rule.ApplyAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            result = rule.Apply();
        }

        if (result.IsFailure && !result.HasError())
        {
            result.WithError(new RuleError(rule));
        }

        return result;
    }
}