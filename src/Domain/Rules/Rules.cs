// // MIT-License
// // Copyright BridgingIT GmbH - All Rights Reserved
// // Use of this source code is governed by an MIT-style license that can be
// // found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
//

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Provides core methods for executing domain rules.
/// </summary>
public static class Rules
{
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
    /// Applies a single rule synchronously.
    /// </summary>
    /// <param name="rule">The rule to apply.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the rule.</returns>
    public static Result Apply(IRule rule)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        try
        {
            var result = rule.Apply();
            if (result.IsFailure && !result.HasError())
            {
                result.WithError(new RuleError(rule));
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError(rule.GetType().Name));
        }
        catch (Exception ex) when (ex is not AggregateException)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex, $"[{rule.GetType().Name}] {rule.Message}".Trim()));
        }
        catch (AggregateException ex)
        {
            var innerEx = ex.InnerExceptions.FirstOrDefault() ?? ex;
            if (innerEx is OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError(rule.GetType().Name));
            }

            return Result.Failure()
                .WithError(new ExceptionError(ex, $"[{rule.GetType().Name}] {rule.Message}".Trim()));
        }
    }

    /// <summary>
    /// Applies a single rule asynchronously.
    /// </summary>
    /// <param name="rule">The rule to apply.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the <see cref="Result"/> indicating success or failure of the rule.</returns>
    public static async Task<Result> ApplyAsync(IRule rule, CancellationToken cancellationToken = default)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result result = null;
            if (rule is AsyncRuleBase)
            {
                result = await rule.ApplyAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                result = rule.Apply();
            }

            if (result.IsFailure && !result.HasError())
            {
                result.WithError(new RuleError(rule));
            }

            return result;
        }
        catch (OperationCanceledException) // TODO: maybe don't hide this inside an error?
        {
            return Result.Failure()
                .WithError(new OperationCancelledError(rule.GetType().Name));
        }
        catch (Exception ex) when (ex is not AggregateException)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex, $"[{rule.GetType().Name}] {rule.Message}".Trim()));
        }
        catch (AggregateException ex)
        {
            var innerEx = ex.InnerExceptions.FirstOrDefault() ?? ex;
            if (innerEx is OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError(rule.GetType().Name));
            }

            return Result.Failure()
                .WithError(new ExceptionError(ex, $"[{rule.GetType().Name}] {rule.Message}".Trim()));
        }
    }
}