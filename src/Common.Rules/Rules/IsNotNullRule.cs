// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines a validation rule that checks whether a given value is not null.
/// The rule returns a success result if the value is not null; otherwise, it returns a failure result.
/// </summary>
/// <typeparam name="T">The type of the value to be validated.</typeparam>
public class IsNotNullRule<T>(T value, string message = null) : RuleBase
{
    private readonly string message = message ?? "Value must not be null";

    /// <summary>
    /// Gets the message returned when the rule is not satisfied.
    /// </summary>
    public override string Message => this.message;

    /// <summary>
    /// Checks whether the configured value is not null.
    /// </summary>
    /// <returns>A successful result when the value is not null; otherwise, a failure result.</returns>
    public override Result Execute() =>
        Result.SuccessIf(value is not null);
}