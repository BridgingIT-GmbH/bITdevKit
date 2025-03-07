// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a rule that validates if any element in a collection satisfies a specified condition.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
/// <param name="collection">The collection of elements to be validated.</param>
/// <param name="ruleFactory">A function that generates a rule for each element in the collection.</param>
public class AnyRule<T>(IEnumerable<T> collection, Func<T, IRule> ruleFactory)
    : RuleBase
{
    /// <summary>
    /// Provides a description or reason why the rule was not satisfied.
    /// </summary>
    public override string Message => "No element in the collection satisfies the condition";

    /// <summary>
    /// Executes a specified rule and returns the result of the execution.
    /// </summary>
    /// <typeparam name="TRule">The type of the rule to execute.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the rule.</typeparam>
    /// <param name="rule">The rule instance to execute.</param>
    /// <returns>The result of executing the rule.</returns>
    public override Result Execute()
    {
        if (collection?.Any() != true)
        {
            return Result.Failure();
        }

        return Result.SuccessIf(collection.Any(item => ruleFactory(item).IsSatisfied().IsSuccess));
    }
}