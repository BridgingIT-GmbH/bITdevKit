// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a value is less than a specified value.
/// </summary>
/// <typeparam name="T">The type of the values being compared, which must implement IComparable&lt;T&gt;.</typeparam>
public class LessThanRule<T>(T value, T other)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets a message describing the outcome of the rule execution.
    /// </summary>
    public override string Message => $"Value must be less than {other}";

    /// <summary>
    /// Executes the rule and returns a success result if validation passes.
    /// </summary>
    /// <returns>A Result indicating the success or failure of the rule execution.</returns>
    protected override Result Execute() =>
        Result.SuccessIf(value.CompareTo(other) < 0);
}