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
    /// Executes a specified rule and returns the result.
    /// </summary>
    /// <param name="rule">The rule to be executed.</param>
    /// <return>Returns true if the rule executes successfully; otherwise, false.</return>
    protected override Result Execute() => Result.SuccessIf(!disallowedValues.Contains(value));
}