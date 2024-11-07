// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks whether a given string does not end with a specified suffix.
/// </summary>
public class DoesNotEndWithRule(string value, string suffix, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets the message associated with the validation rule.
    /// </summary>
    /// <remarks>
    /// The message provides a human-readable description of why the rule was not satisfied.
    /// Each specific rule implementation can override this property to provide a more detailed message.
    /// </remarks>
    public override string Message => $"Value must not end with '{suffix}'";

    /// <summary>
    /// Executes the rule logic and returns a result indicating the success or failure of the rule evaluation.
    /// </summary>
    /// <returns>
    /// A result object representing the outcome of the rule execution.
    /// </returns>
    protected override Result Execute() =>
        Result.SuccessIf(string.IsNullOrEmpty(value) ||
            !value.EndsWith(suffix, comparison));
}