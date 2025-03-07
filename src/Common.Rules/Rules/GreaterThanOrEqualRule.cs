// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a value is greater than or equal to a specified value.
/// Uses the <see cref="IComparable{T}"/> interface for comparison.
/// </summary>
/// <typeparam name="T">The type of the values being compared.</typeparam>
public class GreaterThanOrEqualRule<T>(T value, T other)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets the message associated with the rule.
    /// </summary>
    public override string Message => $"Value must be greater than or equal to {other}";

    /// <summary>
    /// Executes the rule and returns the result of the execution.
    /// </summary>
    /// <returns>
    /// A result indicating the success or failure of the rule execution.
    /// </returns>
    public override Result Execute() =>
        Result.SuccessIf(value.CompareTo(other) >= 0);
}