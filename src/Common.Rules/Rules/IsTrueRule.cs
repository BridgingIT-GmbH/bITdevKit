// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Rule that checks if a boolean value is true.
/// </summary>
public class IsTrueRule(bool value, string message = null) : RuleBase
{
    private readonly string message = message ?? "Value must be true";

    /// <summary>Gets the configured message or the default message requiring a true value.</summary>
    public override string Message => this.message;

    /// <summary>Returns success only when the supplied value is <see langword="true"/>.</summary>
    /// <returns>A result representing the Boolean condition.</returns>
    public override Result Execute() =>
        Result.SuccessIf(value);
}
