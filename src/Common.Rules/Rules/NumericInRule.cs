// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a numeric value is within a set of allowed values.
/// </summary>
/// <typeparam name="T">The type of the value being validated. Must implement IComparable&lt;T&gt;.</typeparam>
public class NumericInRule<T>(T value, IEnumerable<T> allowedValues)
    : RuleBase
    where T : IComparable<T>
{
    /// <summary>
    /// Represents the validation message returned by a rule when it is not satisfied.
    /// </summary>
    public override string Message =>
        $"Value must be one of: {string.Join(", ", allowedValues)}";

    /// <summary>
    /// Executes the rule to produce a result.
    /// </summary>
    /// <returns>The result of the rule execution.</returns>
    protected override Result Execute() =>
        Result.SuccessIf(allowedValues.Any(allowed =>
            value.CompareTo(allowed) == 0));
}