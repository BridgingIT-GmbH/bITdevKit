// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.DateTime;

using System.Globalization;

[UnitTest("Common")]
public class HumanReadableTimeExtensionsTests
{
    public HumanReadableTimeExtensionsTests()
    {
        RelativeTimeLanguages.ResetDefaults();
    }

    [Theory]
    [InlineData(1, "1 second")]
    [InlineData(2, "2 seconds")]
    [InlineData(30, "30 seconds")]
    [InlineData(60, "1 minute")]
    public void ToDurationText_EnglishLongText_Works(int seconds, string expected)
    {
        TimeSpan.FromSeconds(seconds).ToDurationText(new RelativeTimeFormatOptions { Culture = CultureInfo.GetCultureInfo("en") }).ShouldBe(expected);
    }

    [Fact]
    public void ToDurationText_GermanLongText_Works()
    {
        TimeSpan.FromMinutes(3).ToDurationText(new RelativeTimeFormatOptions { Culture = CultureInfo.GetCultureInfo("de") }).ShouldBe("3 Minuten");
    }

    [Theory]
    [InlineData("fr-FR", "3 minutes", "dans 3 minutes")]
    [InlineData("nl-NL", "3 minuten", "over 3 minuten")]
    [InlineData("es-ES", "3 minutos", "en 3 minutos")]
    [InlineData("it-IT", "3 minuti", "tra 3 minuti")]
    public void ToDurationText_BuiltInLanguages_ResolveByCulture(string cultureName, string durationText, string futureText)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var reference = new System.DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        TimeSpan.FromMinutes(3)
            .ToDurationText(new RelativeTimeFormatOptions { Culture = culture })
            .ShouldBe(durationText);

        reference.AddMinutes(3)
            .ToRelativeTimeText(reference, new RelativeTimeFormatOptions { Culture = culture })
            .ShouldBe(futureText);
    }

    [Fact]
    public void ToDurationText_MillisecondDuration_UsesMilliseconds()
    {
        TimeSpan.FromMilliseconds(250)
            .ToDurationText(new RelativeTimeFormatOptions { Culture = CultureInfo.GetCultureInfo("en") })
            .ShouldBe("250 milliseconds");
    }

    [Fact]
    public void ToDurationText_MillisecondDuration_UsesShortText()
    {
        TimeSpan.FromMilliseconds(250)
            .ToDurationText(new RelativeTimeFormatOptions { Culture = CultureInfo.GetCultureInfo("en"), UseShortUnits = true })
            .ShouldBe("250ms");
    }

    [Fact]
    public void ToDurationText_UnsupportedCulture_FallsBackToEnglish()
    {
        var text = TimeSpan.FromMinutes(3).ToDurationText(new RelativeTimeFormatOptions { Culture = CultureInfo.GetCultureInfo("sv-SE") });

        text.ShouldBe("3 minutes");
    }

    [Fact]
    public void ToDurationText_CustomLanguageProvider_CanBeAdded()
    {
        var provider = new RelativeTimeTextProvider([new TestRelativeTimeLanguage()], "xx");
        var text = TimeSpan.FromMinutes(3).ToDurationText(
            new RelativeTimeFormatOptions { Culture = CultureInfo.GetCultureInfo("en-US") },
            provider);

        text.ShouldBe("3 unit");
    }

    [Fact]
    public void ToDurationText_GlobalLanguageRegistration_UsesCultureResolvedLanguage()
    {
        RelativeTimeLanguages.Register(new IcelandicTestRelativeTimeLanguage());

        var text = TimeSpan.FromMinutes(3).ToDurationText(
            new RelativeTimeFormatOptions { Culture = CultureInfo.GetCultureInfo("is-IS") });

        text.ShouldBe("3 global-unit");
    }

    [Fact]
    public void ToRelativeTimeText_DateTime_WorksForPastAndFuture()
    {
        var reference = new System.DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        reference.AddSeconds(-2).ToRelativeTimeText(reference).ShouldBe("just now");
        reference.AddSeconds(-30).ToRelativeTimeText(reference).ShouldBe("30 seconds ago");
        reference.AddMinutes(5).ToRelativeTimeText(reference).ShouldBe("in 5 minutes");
    }

    [Fact]
    public void ToRelativeTimeText_DateTime_UsesShortGermanText()
    {
        var reference = new System.DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new RelativeTimeFormatOptions
        {
            Culture = CultureInfo.GetCultureInfo("de-DE"),
            UseShortUnits = true
        };

        reference.AddHours(-3).ToRelativeTimeText(reference, options).ShouldBe("vor 3 Std.");
    }

    [Fact]
    public void ToRelativeTimeText_DateTime_WithLowNowThreshold_UsesMilliseconds()
    {
        var reference = new System.DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new RelativeTimeFormatOptions
        {
            Culture = CultureInfo.GetCultureInfo("en"),
            NowThreshold = TimeSpan.Zero
        };

        reference.AddMilliseconds(250).ToRelativeTimeText(reference, options).ShouldBe("in 250 milliseconds");
    }

    [Fact]
    public void ToRelativeTimeText_DateTimeOffset_UsesInstantComparison()
    {
        var reference = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var sameInstant = new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.FromHours(2));

        sameInstant.ToRelativeTimeText(reference).ShouldBe("just now");
    }

    [Fact]
    public void ToRelativeTimeText_DateOnly_UsesDateUnitsOnly()
    {
        var reference = new DateOnly(2026, 1, 10);

        reference.AddDays(-1).ToRelativeTimeText(reference).ShouldBe("1 day ago");
        reference.AddDays(14).ToRelativeTimeText(reference).ShouldBe("in 2 weeks");
    }

    [Fact]
    public void ToRelativeTimeText_TimeOnly_UsesSameDaySemantics()
    {
        var reference = new TimeOnly(12, 0);

        new TimeOnly(12, 5).ToRelativeTimeText(reference).ShouldBe("in 5 minutes");
    }

    private sealed class TestRelativeTimeLanguage : IRelativeTimeLanguage
    {
        public string LanguageCode => "xx";

        public string Now(bool shortText) => "now";

        public string FormatUnit(RelativeTimeUnit unit, long value, bool shortText) => $"{value} unit";

        public string FormatPast(string durationText, bool shortText) => $"past {durationText}";

        public string FormatFuture(string durationText, bool shortText) => $"future {durationText}";
    }

    private sealed class IcelandicTestRelativeTimeLanguage : IRelativeTimeLanguage
    {
        public string LanguageCode => "is";

        public string Now(bool shortText) => "global-now";

        public string FormatUnit(RelativeTimeUnit unit, long value, bool shortText) => $"{value} global-unit";

        public string FormatPast(string durationText, bool shortText) => $"global-past {durationText}";

        public string FormatFuture(string durationText, bool shortText) => $"global-future {durationText}";
    }
}
