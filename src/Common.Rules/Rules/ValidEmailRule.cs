// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.RegularExpressions;

/// <summary>
/// Validates whether a given string is a valid email address format.
/// </summary>
public partial class ValidEmailRule(string value) : RuleBase
{
    // Using source generation for regex compilation
    /// <summary>
    /// Represents a regular expression pattern used to validate email addresses.
    /// </summary>
    /// <returns>True if the input string is a valid email address; otherwise, false.</returns>
    [GeneratedRegex(
        @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
        @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 3000)]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Gets or sets the message associated with this instance.
    /// </summary>
    /// <value>
    /// The message providing details or information.
    /// </value>
    public override string Message => "Invalid email address";

    /// <summary>
    /// Executes a given business rule, performing its associated action.
    /// </summary>
    /// <returns>Returns true if the rule execution is successful; otherwise, false.</returns>
    public override Result Execute()
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure();
        }

        return Result.SuccessIf(EmailRegex().IsMatch(value));
    }
}