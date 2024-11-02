// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Wraps a rule with a synchronous condition that determines if the rule should be executed.
/// </summary>
public class ConditionalRule : DomainRuleBase
{
    private readonly Func<bool> condition;
    private readonly IDomainRule rule;

    /// <summary>
    /// Initializes a new instance of the SyncConditionalRule class.
    /// </summary>
    /// <param name="condition">A function that determines if the rule should be executed.</param>
    /// <param name="rule">The rule to execute if the condition is met.</param>
    public ConditionalRule(Func<bool> condition, IDomainRule rule)
    {
        this.condition = condition;
        this.rule = rule;
    }

    /// <summary>
    /// Executes the rule if the condition is met. Returns a success result if the condition is not met;
    /// otherwise, applies the rule and returns its result. Catches exceptions and returns a failure result
    /// with an error if an exception occurs.
    /// </summary>
    /// <returns>A result indicating the success or failure of the rule execution.</returns>
    protected override Result ExecuteRule()
    {
        try
        {
            if (!this.condition())
            {
                return Result.Success();
            }

            return this.rule.Apply();
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new DomainRuleError(this.GetType().Name, ex.Message));
        }
    }
}