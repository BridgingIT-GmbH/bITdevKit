// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class TimeSpanExtensionsTests
{
    [Fact]
    public void Short_tests()
    {
        const short Value = 2;

        var ticks = Value.Ticks();
        ticks.Ticks.ShouldBe(Value);

        var milliSeconds = Value.Milliseconds();
        milliSeconds.TotalMilliseconds.ShouldBe(Value);

        var seconds = Value.Seconds();
        seconds.TotalSeconds.ShouldBe(Value);

        var minutes = Value.Minutes();
        minutes.TotalMinutes.ShouldBe(Value);

        var hours = Value.Hours();
        hours.TotalHours.ShouldBe(Value);

        var days = Value.Days();
        days.TotalDays.ShouldBe(Value);

        var weeks = Value.Weeks();
        weeks.TotalDays.ShouldBe(Value * 7);
    }

    [Fact]
    public void Int_tests()
    {
        const int Value = 2;

        var ticks = Value.Ticks();
        ticks.Ticks.ShouldBe(Value);

        var milliSeconds = Value.Milliseconds();
        milliSeconds.TotalMilliseconds.ShouldBe(Value);

        var seconds = Value.Seconds();
        seconds.TotalSeconds.ShouldBe(Value);

        var minutes = Value.Minutes();
        minutes.TotalMinutes.ShouldBe(Value);

        var hours = Value.Hours();
        hours.TotalHours.ShouldBe(Value);

        var days = Value.Days();
        days.TotalDays.ShouldBe(Value);

        var weeks = Value.Weeks();
        weeks.TotalDays.ShouldBe(Value * 7);
    }

    [Fact]
    public void Long_tests()
    {
        const long Value = 2;

        var ticks = Value.Ticks();
        ticks.Ticks.ShouldBe(Value);

        var milliSeconds = Value.Milliseconds();
        milliSeconds.TotalMilliseconds.ShouldBe(Value);

        var seconds = Value.Seconds();
        seconds.TotalSeconds.ShouldBe(Value);

        var minutes = Value.Minutes();
        minutes.TotalMinutes.ShouldBe(Value);

        var hours = Value.Hours();
        hours.TotalHours.ShouldBe(Value);

        var days = Value.Days();
        days.TotalDays.ShouldBe(Value);

        var weeks = Value.Weeks();
        weeks.TotalDays.ShouldBe(Value * 7);
    }
}