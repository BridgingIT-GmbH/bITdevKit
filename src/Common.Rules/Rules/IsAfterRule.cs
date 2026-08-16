// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Rule that checks if a DateTime is after another specified date.
/// </summary>
public class IsAfterRule(DateTime value, DateTime comparisonDate, string message = null) : RuleBase
{
    private readonly string message = message ?? $"Value must be after {comparisonDate}";

    /// <summary>Gets the configured message or a default naming the exclusive lower date boundary.</summary>
    public override string Message => this.message;

    /// <summary>Returns success only when the value is strictly later than the comparison date.</summary>
    /// <returns>A result representing the exclusive date comparison.</returns>
    public override Result Execute() =>
        Result.SuccessIf(value > comparisonDate);
}
