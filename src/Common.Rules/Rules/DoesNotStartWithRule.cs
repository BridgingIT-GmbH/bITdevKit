// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a value does not start with a specified prefix.
/// </summary>
public class DoesNotStartWithRule(string value, string prefix, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets a descriptive message associated with the rule, which typically describes why the rule failed.
    /// </summary>
    public override string Message => $"Value must not start with '{prefix}'";

    /// <summary>
    /// Executes a specified business rule and returns the result of the execution.
    /// </summary>
    /// <param name="rule">The business rule to execute.</param>
    /// <param name="context">The context in which the rule is executed.</param>
    /// <return>Returns the result of the rule execution.</return>
    public override Result Execute() =>
        Result.SuccessIf(string.IsNullOrEmpty(value) ||
            !value.StartsWith(prefix, comparison));
}