// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Rule that checks if a boolean value is false.
/// </summary>
public class IsFalseRule(bool value, string message = null) : RuleBase
{
    private readonly string message = message ?? "Value must be false";

    public override string Message => this.message;

    public override Result Execute() =>
        Result.SuccessIf(!value);
}