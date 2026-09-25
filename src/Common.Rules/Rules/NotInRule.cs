// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Specifies a validation rule that checks whether a given value is not within a specified set of disallowed values.
/// </summary>
public class NotInRule<T>(T value, IEnumerable<T> disallowedValues)
    : RuleBase
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public override string Message => $"Value must not be one of: {string.Join(", ", disallowedValues)}";

    /// <summary>
    /// Checks whether the value is absent from the configured set of disallowed values.
    /// </summary>
    /// <returns>A successful result when the disallowed values do not contain the value; otherwise, a failure result.</returns>
    public override Result Execute() => Result.SuccessIf(!disallowedValues.Contains(value));
}