// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Wraps a rule with an asynchronous condition that determines if the rule should be executed.
/// </summary>
public class AsyncConditionalRule : AsyncDomainRuleBase
{
    private readonly Func<CancellationToken, Task<bool>> condition;
    private readonly IDomainRule rule;

    /// <summary>
    /// Initializes a new instance of the ConditionalRule class.
    /// </summary>
    /// <param name="condition">An async function that determines if the rule should be executed.</param>
    /// <param name="rule">The rule to execute if the condition is met.</param>
    public AsyncConditionalRule(Func<CancellationToken, Task<bool>> condition, IDomainRule rule)
    {
        this.condition = condition;
        this.rule = rule;
    }

    /// <summary>
    /// Executes the rule associated with this instance asynchronously after evaluating the given condition.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the result of the rule execution.</returns>
    protected override async Task<Result> ExecuteRuleAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await this.condition(cancellationToken).ConfigureAwait(false))
            {
                return Result.Success();
            }

            return this.rule switch
            {
                AsyncDomainRuleBase => await this.rule.ApplyAsync(cancellationToken).ConfigureAwait(false),
                _ => this.rule.Apply()
            };
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError(this.GetType().Name));
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new DomainRuleError(this.GetType().Name, ex.Message));
        }
    }
}