// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Rule that checks if a DateTime is after another specified date.
/// </summary>
public class IsAfterRule(DateTime value, DateTime comparisonDate) : RuleBase
{
    public override string Message => $"Value must be after {comparisonDate}";

    protected override Result Execute() =>
        Result.SuccessIf(value > comparisonDate);
}