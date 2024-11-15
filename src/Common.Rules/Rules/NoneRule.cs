// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Implements a validation rule that always returns true, indicating no validation checks are performed.
/// </summary>
public class NoneRule<T>(IEnumerable<T> collection, Func<T, IRule> ruleFactory)
    : RuleBase
{
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>
    /// A string representing the content of the message.
    /// </value>
    public override string Message =>
        "Some elements in the collection satisfy the condition when none should";

    /// <summary>
    /// Executes a specified rule.
    /// </summary>
    /// <param name="ruleName">The name of the rule to execute.</param>
    /// <param name="context">The context in which the rule is to be executed.</param>
    /// <returns>The result of the rule execution.</returns>
    protected override Result Execute()
    {
        if (collection?.Any() != true)
        {
            return Result.Success();
        }

        return Result.SuccessIf(collection.All(item => !ruleFactory(item).IsSatisfied().IsSuccess));
    }
}