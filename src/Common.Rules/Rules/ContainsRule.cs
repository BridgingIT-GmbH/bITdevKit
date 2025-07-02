// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Validates that a specified string contains a given substring with a specified string comparison option.
/// </summary>
/// <remarks>
/// This rule checks whether the main string contains the specified substring using the comparison rules provided.
/// If checked string is empty or null, the validation fails.
/// </remarks>
public class ContainsRule(string value, string substring, StringComparison comparison)
    : RuleBase
{
    /// <summary>
    /// Gets the message that describes the result of applying the rule.
    /// </summary>
    public override string Message => $"Value must contain '{substring}'";

    /// <summary>
    /// Executes the specified rule.
    /// </summary>
    /// <returns>The result of the executed rule.</returns>
    public override Result Execute() =>
        Result.SuccessIf(!string.IsNullOrEmpty(value) &&
            value.Contains(substring, comparison));
}