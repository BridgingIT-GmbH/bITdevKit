// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a rule that is defined by a user-specified predicate function.
/// </summary>
/// <remarks>
/// This class derives from <see cref="RuleBase"/> and allows for the creation of custom validation rules
/// based on a <see cref="Func{bool}"/> predicate. If the predicate evaluates to true, the rule is considered satisfied;
/// otherwise, the rule fails. An optional message can be provided to describe the rule.
/// </remarks>
public class FuncRule(Func<bool> predicate, string message = "Predicate rule not satisfied") : RuleBase
{
    /// <summary>
    /// Gets the message associated with the rule.
    /// </summary>
    /// <value>
    /// A string representing the message that provides information about the rule's purpose or failure reason.
    /// </value>
    public override string Message { get; } = message;

    /// <summary>
    /// Executes the predicate function and returns a Result based on its outcome.
    /// </summary>
    /// <returns>
    /// A Result indicating success if the predicate function evaluates to true, otherwise a Result indicating failure.
    /// </returns>
    public override Result Execute() =>
        Result.SuccessIf(predicate());
}