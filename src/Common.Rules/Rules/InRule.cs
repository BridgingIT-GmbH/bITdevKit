// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a single validation rule that can be applied to input values.
/// </summary>
public class InRule<T>(T value, IEnumerable<T> allowedValues)
    : RuleBase
{
    /// <summary>
    /// Gets or sets the message text.
    /// </summary>
    /// <value>
    /// The message text content.
    /// </value>
    public override string Message => $"Value must be one of: {string.Join(", ", allowedValues)}";

    /// <summary>
    /// Checks whether the value is in the configured set of allowed values.
    /// </summary>
    /// <returns>A successful result when the allowed values contain the value; otherwise, a failure result.</returns>
    public override Result Execute() => Result.SuccessIf(allowedValues.Contains(value));
}