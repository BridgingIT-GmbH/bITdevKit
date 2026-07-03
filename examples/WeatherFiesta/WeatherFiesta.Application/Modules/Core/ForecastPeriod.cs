// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

internal static class ForecastPeriod
{
    public static Result<DateOnlyRange> Resolve(string range, int days)
    {
        if (!string.IsNullOrWhiteSpace(range))
        {
            return range.TryParseDateOnlyRange(out var parsed)
                ? Result<DateOnlyRange>.Success(parsed)
                : Result<DateOnlyRange>.Failure("Forecast range must use the ISO interval format yyyy-MM-dd/yyyy-MM-dd.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Result<DateOnlyRange>.Success(new DateOnlyRange(today, today.AddDays(Math.Max(days, 1))));
    }

    public static DateOnlyRange Limit(DateOnlyRange range, int maxDays)
    {
        if (maxDays <= 0 || !range.StartInclusive.HasValue)
        {
            return range;
        }

        var cappedEnd = range.StartInclusive.Value.AddDays(maxDays);
        if (!range.EndExclusive.HasValue || range.EndExclusive.Value > cappedEnd)
        {
            return new DateOnlyRange(range.StartInclusive, cappedEnd);
        }

        return range;
    }
}
