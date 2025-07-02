// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a given value is empty.
/// </summary>
public class IsEmptyRule(string value) : RuleBase
{
    /// <summary>
    /// Gets the message that will be displayed when the rule fails.
    /// </summary>
    public override string Message => "Value must be empty";

    /// <summary>
    /// Executes the rule associated with the current instance.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the rule execution.</returns>
    public override Result Execute() =>
        Result.SuccessIf(string.IsNullOrEmpty(value));
}

/// <summary>
/// Represents a validation rule that checks if a given value is empty.
/// </summary>
public class IsEmptyRule<T>(IEnumerable<T> value) : RuleBase
{
    /// <summary>
    /// Gets the message that will be displayed when the rule fails.
    /// </summary>
    public override string Message => "Value must be empty";

    /// <summary>
    /// Executes the rule associated with the current instance.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the rule execution.</returns>
    public override Result Execute() =>
        Result.SuccessIf(value.IsNullOrEmpty());
}