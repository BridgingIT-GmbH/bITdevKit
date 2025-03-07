// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a given numeric value is not
/// contained within a specified set of disallowed values.
/// </summary>
/// <typeparam name="T">The type of the value being validated, which must implement IComparable<T
public class NumericNotInRule<T>(T value, IEnumerable<T> disallowedValues)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Gets the message associated with the rule indicating the reason why the rule was not satisfied.
    /// </summary>
    /// <remarks>
    /// This property provides the specific message that describes the constraint or validation error.
    /// Override this property in derived classes to provide a meaningful message for the rule.
    /// </remarks>
    public override string Message =>
        $"Value must not be one of: {string.Join(", ", disallowedValues)}";

    /// <summary>
    /// Executes a validation rule.
    /// </summary>
    /// <returns>Result indicating success or failure of the rule execution.</returns>
    public override Result Execute() =>
        Result.SuccessIf(disallowedValues.All(disallowed => value.CompareTo(disallowed) != 0));
}