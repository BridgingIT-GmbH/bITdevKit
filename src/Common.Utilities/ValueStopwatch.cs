// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Diagnostics;

public readonly struct ValueStopwatch // no alloc stopwatch
{
    private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

    private readonly long start;

    private ValueStopwatch(long start) =>
        this.start = start;

    public bool IsActive => this.start != 0;

    public static ValueStopwatch StartNew() =>
        new(GetTimestamp());

    public static long GetTimestamp() => Stopwatch.GetTimestamp();

    public static TimeSpan GetElapsedTime(long start, long end) =>
        new((long)(TimestampToTicks * (end - start)));

    public static long GetElapsedMilliseconds(long start, long end) =>
        (end - start) * 1000 / Stopwatch.Frequency;

    public TimeSpan GetElapsedTime() =>
        GetElapsedTime(this.start, GetTimestamp());

    public long GetElapsedMilliseconds() =>
        GetElapsedMilliseconds(this.start, GetTimestamp());
}