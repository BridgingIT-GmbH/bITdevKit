// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Validates that a value is not empty according to specific rules for different types.
/// </summary>
public class IsNotEmptyRule(string value) : RuleBase
{
    /// <summary>
    /// Gets the message describing the outcome of the rule execution.
    /// </summary>
    public override string Message => "Value must not be empty";

    /// <summary>
    /// Executes a rule, returning a result indicating success or failure.
    /// </summary>
    /// <returns>A Result object representing the outcome of the rule execution.</returns>
    protected override Result Execute() =>
        Result.SuccessIf(!string.IsNullOrEmpty(value));
}

/// <summary>
/// Validates that a value is not empty according to specific rules for different types.
/// </summary>
public class IsNotEmptyRule<T>(IEnumerable<T> value) : RuleBase
{
    /// <summary>
    /// Gets the message describing the outcome of the rule execution.
    /// </summary>
    public override string Message => "Value must not be empty";

    /// <summary>
    /// Executes a rule, returning a result indicating success or failure.
    /// </summary>
    /// <returns>A Result object representing the outcome of the rule execution.</returns>
    protected override Result Execute() =>
        Result.SuccessIf(!value.IsNullOrEmpty());
}