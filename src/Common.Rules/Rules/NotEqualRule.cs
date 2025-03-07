// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if two values are not equal.
/// </summary>
/// <typeparam name="T">The type of the values being compared.</typeparam>
public class NotEqualRule<T>(T value, T other) : RuleBase
{
    /// <summary>
    /// Represents a simple message with a sender and content.
    /// </summary>
    public override string Message => $"Value must not be equal to {other}";

    /// <summary>
    /// Executes the validation rule, returning a success result if the rule passes and a failure result if it does not.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the rule.</returns>
    public override Result Execute() =>
        Result.SuccessIf(!EqualityComparer<T>.Default.Equals(value, other));
}