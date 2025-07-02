// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a collection of all the rules to be applied for a specific operation or validation process.
/// </summary>
public class AllRule<T>(IEnumerable<T> collection, Func<T, IRule> ruleFactory)
    : RuleBase
{
    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    /// <value>
    /// The text content of the message.
    /// </value>
    public override string Message => "Not all elements in the collection satisfy the condition";

    /// <summary>
    /// Executes the defined rule and returns a <see cref="Result"/> indicating success or failure.
    /// </summary>
    /// <returns>A <see cref="Result"/> object representing the outcome of the rule execution.</returns>
    public override Result Execute()
    {
        if (collection?.Any() != true)
        {
            return Result.Failure();
        }

        return Result.SuccessIf(collection.All(item => ruleFactory(item).IsSatisfied().IsSuccess));
    }
}