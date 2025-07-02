// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks whether a given value is less than or equal to a specified value.
/// </summary>
/// <typeparam name="T">The type of the values being compared. Must implement IComparable<T>.</typeparam>
public class LessThanOrEqualRule<T>(T value, T other)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets the message associated with the rule.
    /// </summary>
    public override string Message => $"Value must be less than or equal to {other}";

    /// <summary>
    /// Executes the defined rule and returns the result of the execution.
    /// </summary>
    /// <returns>The outcome of the rule execution as a Result object.</returns>
    public override Result Execute() =>
        Result.SuccessIf(value.CompareTo(other) <= 0);
}