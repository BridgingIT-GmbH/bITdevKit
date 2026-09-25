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
    /// Determines whether none of the collection elements satisfy their generated rules.
    /// </summary>
    /// <returns>A successful result when the collection is empty or no generated rule is satisfied; otherwise, a failure result.</returns>
    public override Result Execute()
    {
        if (collection?.Any() != true)
        {
            return Result.Success();
        }

        return Result.SuccessIf(collection.All(item => !ruleFactory(item).IsSatisfied().IsSuccess));
    }
}