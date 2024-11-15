// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a numeric value falls within a specified range.
/// </summary>
/// <typeparam name="T">The type of the numeric value, which must implement IComparable&lt;T&gt;.</typeparam>
public class NumericRangeRule<T>(T value, T min, T max)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Message providing additional information about the rule's execution result.
    /// Derived classes can override this property to provide a more specific message context.
    /// </summary>
    public override string Message => $"Value must be between {min} and {max}";

    /// <summary>
    /// Executes the specific rule defined in the derived class.
    /// </summary>
    /// <returns>A <c>Result</c> object representing the success or failure of the rule execution.</returns>
    protected override Result Execute() =>
        Result.SuccessIf(value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0);
}