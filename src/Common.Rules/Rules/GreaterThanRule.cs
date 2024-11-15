// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a value is greater than a specified value.
/// </summary>
/// <typeparam name="T">The type of the values being compared, which must implement IComparable{T}.</typeparam>
public class GreaterThanRule<T>(T value, T other)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets the message associated with the rule. This message provides information about the rule failure reason.
    /// </summary>
    public override string Message => $"Value must be greater than {other}";

    /// <summary>
    /// Executes a validation rule and returns the result.
    /// </summary>
    /// <returns>
    /// Result indicating the success or failure of the rule execution.
    /// </returns>
    protected override Result Execute() =>
        Result.SuccessIf(value.CompareTo(other) > 0);
}