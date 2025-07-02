// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if two values are equal.
/// </summary>
/// <typeparam name="T">The type of the values being compared.</typeparam>
public class EqualRule<T>(T value, T other) : RuleBase
{
    /// <summary>
    /// Gets the message that describes the outcome of the rule evaluation.
    /// </summary>
    /// <remarks>
    /// In the context of the derived class <see cref="EqualRule{T}"/>, the message indicates that
    /// the value must be equal to the specified other value.
    /// </remarks>
    public override string Message => $"Value must be equal to {other}";

    /// <summary>
    /// Executes the rule logic associated with this rule.
    /// </summary>
    /// <returns>A Result object indicating the outcome of the rule execution.</returns>
    public override Result Execute() =>
        Result.SuccessIf(EqualityComparer<T>.Default.Equals(value, other));
}