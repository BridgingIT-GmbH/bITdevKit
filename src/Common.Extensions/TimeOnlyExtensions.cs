// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static class TimeOnlyExtensions
{
    [DebuggerStepThrough]
    public static TimeOnly Add(this TimeOnly time, TimeUnit unit, int amount)
    {
        return unit switch
        {
            TimeUnit.Minute => time.AddMinutes(amount),
            TimeUnit.Hour => time.AddHours(amount),
            _ => throw new ArgumentException("Unsupported TimeUnit.", nameof(unit))
        };
    }

    [DebuggerStepThrough]
    public static bool IsInRange(this TimeOnly time, TimeOnly start, TimeOnly end, bool inclusive = true)
    {
        return inclusive ? time >= start && time <= end : time > start && time < end;
    }

    [DebuggerStepThrough]
    public static bool IsInRelativeRange(this TimeOnly time, TimeUnit unit, int amount, DateTimeDirection direction, bool inclusive = true)
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var referenceTime = direction == DateTimeDirection.Past ? time.Add(unit, -amount) : now.Add(unit, amount);

        return direction == DateTimeDirection.Past
            ? (inclusive ? time <= now && time >= referenceTime : time < now && time > referenceTime)
            : (inclusive ? time >= now && time <= referenceTime : time > now && time < referenceTime);
    }

    [DebuggerStepThrough]
    public static TimeOnly RoundToNearest(this TimeOnly timeOnly, TimeUnit timeUnit)
    {
        switch (timeUnit)
        {
            case TimeUnit.Minute:
                return new TimeOnly(timeOnly.Hour, timeOnly.Minute, 0);
            case TimeUnit.Hour:
                return new TimeOnly(timeOnly.Hour, 0, 0);
            default:
                throw new ArgumentException("Unsupported TimeUnit.", nameof(timeUnit));
        }
    }
}