// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Wraps a rule with a synchronous condition that determines if the rule should be executed.
/// </summary>
public class ConditionalRule : RuleBase
{
    private readonly Func<bool> condition;
    private readonly IRule rule;

    /// <summary>
    /// Initializes a new instance of the SyncConditionalRule class.
    /// </summary>
    /// <param name="condition">A function that determines if the rule should be executed.</param>
    /// <param name="rule">The rule to execute if the condition is met.</param>
    public ConditionalRule(Func<bool> condition, IRule rule)
    {
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition)); ;
        this.rule = rule;
    }

    /// <summary>
    /// Executes the rule if the condition is met. Returns a success result if the condition is not met;
    /// otherwise, applies the rule and returns its result. Catches exceptions and returns a failure result
    /// with an error if an exception occurs.
    /// </summary>
    /// <returns>A result indicating the success or failure of the rule execution.</returns>
    protected override Result Execute()
    {
        if (!this.condition())
        {
            return Result.Success();
        }

        return this.rule.IsSatisfied();
    }
}