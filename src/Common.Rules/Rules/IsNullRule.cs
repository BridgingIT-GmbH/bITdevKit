// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Rule for validating if a given value is null.
/// </summary>
public class IsNullRule<T>(T value) : RuleBase
{
    /// <summary>
    /// Gets the message associated with the rule. Provides a human-readable explanation
    /// of why the rule failed or what condition the rule checks for.
    /// </summary>
    public override string Message => "Value must be null";

    /// <summary>
    /// Executes a validation rule.
    /// </summary>
    /// <returns>A Result indicating the outcome of the rule execution.</returns>
    protected override Result Execute() =>
        Result.SuccessIf(value is null);
}