// // MIT-License
// // Copyright BridgingIT GmbH - All Rights Reserved
// // Use of this source code is governed by an MIT-style license that can be
// // found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
//
namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Provides core methods for executing domain rules.
/// </summary>
public static class DomainRules
{
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
        foreach(var rule in rules)
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
    public static Result Apply(IDomainRule rule)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        try
        {
            return rule.Apply();
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError(rule.GetType().Name));
        }
        catch (Exception ex) when (ex is not AggregateException)
        {
            return Result.Failure()
                .WithError(new DomainRuleError(rule.GetType().Name, ex.Message));
        }
        catch (AggregateException ex)
        {
            var innerException = ex.InnerExceptions.FirstOrDefault() ?? ex;
            if (innerException is OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError(rule.GetType().Name));
            }

            return Result.Failure()
                .WithError(new DomainRuleError(rule.GetType().Name, innerException.Message));
        }
    }

    /// <summary>
    /// Applies a single rule asynchronously.
    /// </summary>
    /// <param name="rule">The rule to apply.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the <see cref="Result"/> indicating success or failure of the rule.</returns>
    public static async Task<Result> ApplyAsync(IDomainRule rule, CancellationToken cancellationToken = default)
    {
        if (rule is null)
        {
            return Result.Success();
        }

        try
        {
            if (rule is AsyncDomainRuleBase)
            {
                return await rule.ApplyAsync(cancellationToken).ConfigureAwait(false);
            }

            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            return rule.Apply();
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError(rule.GetType().Name));
        }
        catch (Exception ex) when (ex is not AggregateException)
        {
            return Result.Failure()
                .WithError(new DomainRuleError(rule.GetType().Name, ex.Message));
        }
        catch (AggregateException ex)
        {
            var innerException = ex.InnerExceptions.FirstOrDefault() ?? ex;
            if (innerException is OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError(rule.GetType().Name));
            }

            return Result.Failure()
                .WithError(new DomainRuleError(rule.GetType().Name, innerException.Message));
        }
    }
}