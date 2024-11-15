// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a validation rule that checks if a given text is not part of a specified list of disallowed values.
/// </summary>
/// <param name="value">The text value to be checked against the disallowed values.</param>
/// <param name="disallowedValues">A collection of disallowed text values that the <paramref name="value"/> should not match.</param>
/// <param name="comparison">Specifies the string comparison option to use when comparing the text value and the disallowed values.</param>
public class TextNotInRule(
    string value,
    IEnumerable<string> disallowedValues,
    StringComparison comparison) : RuleBase
{
    /// <summary>
    /// Gets the message that describes the result of the rule evaluation.
    /// </summary>
    /// <remarks>
    /// The <c>Message</c> property provides a locally overridden, generally user-friendly message
    /// that describes why a rule was not satisfied. If no local override is provided,
    /// a default message "Rule not satisfied" from the base class will be used.
    /// </remarks>
    public override string Message =>
        $"Text must not be one of: {string.Join(", ", disallowedValues)}";

    /// <summary>
    /// Executes the rule and returns the result.
    /// </summary>
    /// <returns>
    /// The result of the rule execution.
    /// </returns>
    protected override Result Execute() =>
        Result.SuccessIf(!disallowedValues.Any(disallowed =>
            string.Equals(value, disallowed, comparison)));
}