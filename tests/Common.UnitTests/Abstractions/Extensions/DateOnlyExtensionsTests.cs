// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class DateOnlyExtensionsTests
{
    private readonly Faker faker = new();

    [Fact]
    public void StartOfDay_GivenDateOnly_ReturnsSameDate()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.StartOfDay();

        // Assert
        result.ShouldBe(date); // DateOnly has no time component
    }

    [Fact]
    public void EndOfDay_GivenDateOnly_ReturnsSameDate()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.EndOfDay();

        // Assert
        result.ShouldBe(date); // DateOnly has no time component
    }

    [Fact]
    public void StartOfWeek_GivenDateOnly_ReturnsStartOfWeek()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15); // Friday

        // Act
        var result = date.StartOfWeek(); // Default is Monday

        // Assert
        result.ShouldBe(new DateOnly(2024, 3, 11)); // Should be Monday
        result.DayOfWeek.ShouldBe(DayOfWeek.Monday);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Saturday)]
    public void StartOfWeek_WithCustomFirstDay_ReturnsCorrectStartOfWeek(DayOfWeek firstDayOfWeek)
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15); // Friday

        // Act
        var result = date.StartOfWeek(firstDayOfWeek);

        // Assert
        result.DayOfWeek.ShouldBe(firstDayOfWeek);
        result.ShouldBeLessThanOrEqualTo(date);
    }

    [Fact]
    public void EndOfWeek_GivenDateOnly_ReturnsEndOfWeek()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15); // Friday

        // Act
        var result = date.EndOfWeek();

        // Assert
        result.ShouldBe(new DateOnly(2024, 3, 17)); // Should be Sunday
        result.DayOfWeek.ShouldBe(DayOfWeek.Sunday);
    }

    [Fact]
    public void StartOfMonth_GivenDateOnly_ReturnsStartOfMonth()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.StartOfMonth();

        // Assert
        result.ShouldBe(new DateOnly(2024, 3, 1));
    }

    [Theory]
    [InlineData(2024, 2, 15, 29)] // Leap year February
    [InlineData(2023, 2, 15, 28)] // Non-leap year February
    [InlineData(2024, 3, 15, 31)] // 31-day month
    [InlineData(2024, 4, 15, 30)] // 30-day month
    public void EndOfMonth_GivenDateOnly_ReturnsEndOfMonth(int year, int month, int day, int expectedLastDay)
    {
        // Arrange
        var date = new DateOnly(year, month, day);

        // Act
        var result = date.EndOfMonth();

        // Assert
        result.ShouldBe(new DateOnly(year, month, expectedLastDay));
    }

    [Fact]
    public void StartOfYear_GivenDateOnly_ReturnsStartOfYear()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.StartOfYear();

        // Assert
        result.ShouldBe(new DateOnly(2024, 1, 1));
    }

    [Fact]
    public void EndOfYear_GivenDateOnly_ReturnsEndOfYear()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.EndOfYear();

        // Assert
        result.ShouldBe(new DateOnly(2024, 12, 31));
    }

    [Theory]
    [InlineData(2024, 3, 15, DateUnit.Day, 5, 2024, 3, 20)]
    [InlineData(2024, 3, 15, DateUnit.Week, 1, 2024, 3, 22)]
    [InlineData(2024, 3, 15, DateUnit.Month, 1, 2024, 4, 15)]
    [InlineData(2024, 3, 15, DateUnit.Year, 1, 2025, 3, 15)]
    [InlineData(2024, 3, 31, DateUnit.Month, 1, 2024, 4, 30)] // End of month case
    public void Add_WithDifferentUnits_AddsCorrectly(
        int year, int month, int day,
        DateUnit unit, int amount,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        // Arrange
        var date = new DateOnly(year, month, day);
        var expected = new DateOnly(expectedYear, expectedMonth, expectedDay);

        // Act
        var result = date.Add(unit, amount);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(2024, 3, 15, 2024, 3, 1, 2024, 3, 31, true, true)]  // Within range inclusive
    [InlineData(2024, 3, 15, 2024, 3, 15, 2024, 3, 31, true, true)] // At start inclusive
    [InlineData(2024, 3, 31, 2024, 3, 1, 2024, 3, 31, true, true)]  // At end inclusive
    [InlineData(2024, 3, 15, 2024, 3, 1, 2024, 3, 31, false, true)] // Within range exclusive
    [InlineData(2024, 3, 1, 2024, 3, 1, 2024, 3, 31, false, false)] // At start exclusive
    public void IsInRange_WithVariousScenarios_ReturnsExpectedResult(
        int year, int month, int day,
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay,
        bool inclusive, bool expected)
    {
        // Arrange
        var date = new DateOnly(year, month, day);
        var start = new DateOnly(startYear, startMonth, startDay);
        var end = new DateOnly(endYear, endMonth, endDay);

        // Act
        var result = date.IsInRange(start, end, inclusive);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void IsInRelativeRange_InFutureRange_ReturnsTrue()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Now);
        var future = today.AddDays(5);

        // Act & Assert
        future.IsInRelativeRange(DateUnit.Day, 10, DateTimeDirection.Future, true).ShouldBeTrue();
        future.IsInRelativeRange(DateUnit.Day, 3, DateTimeDirection.Future, true).ShouldBeFalse();
    }

    [Theory]
    [InlineData(2024, 1, 1, 1)]  // First week of year
    [InlineData(2024, 12, 31, 53)] // Last week of year
    public void GetWeekOfYear_ReturnsCorrectWeek(int year, int month, int day, int expectedWeek)
    {
        // Arrange
        var date = new DateOnly(year, month, day);

        // Act
        var result = date.GetWeekOfYear();

        // Assert
        result.ShouldBe(expectedWeek);
    }

    [Theory]
    [InlineData(2024, true)]  // Leap year
    [InlineData(2023, false)] // Non-leap year
    public void IsLeapYear_ReturnsCorrectResult(int year, bool expected)
    {
        // Arrange
        var date = new DateOnly(year, 1, 1);

        // Act
        var result = date.IsLeapYear();

        // Assert
        result.ShouldBe(expected);
    }

    // does not work on devops CI
    // [Fact]
    // public void ToUnixTimeSeconds_ReturnsCorrectTimestamp()
    // {
    //     // Arrange
    //     var date = new DateOnly(2024, 1, 1);
    //     var expected = 1704063600L; // 2024-01-01 00:00:00 UTC
    //
    //     // Act
    //     var result = date.ToUnixTimeSeconds();
    //
    //     // Assert
    //     result.ShouldBe(expected);
    // }

    [Fact]
    public void ToDateTimeOffset_WithOffset_ReturnsCorrectValue()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);
        var offset = TimeSpan.FromHours(2);

        // Act
        var result = date.ToDateTimeOffset(offset);

        // Assert
        result.Date.ShouldBe(date.ToDateTime(TimeOnly.MinValue).Date);
        result.Offset.ShouldBe(offset);
    }

    [Fact]
    public void TimeSpanTo_ReturnsCorrectDuration()
    {
        // Arrange
        var start = new DateOnly(2024, 3, 15);
        var end = new DateOnly(2024, 3, 20);

        // Act
        var result = start.TimeSpanTo(end);

        // Assert
        result.ShouldBe(TimeSpan.FromDays(5));
    }

    [Theory]
    [InlineData(DateUnit.Day)]
    [InlineData(DateUnit.Week)]
    [InlineData(DateUnit.Month)]
    [InlineData(DateUnit.Year)]
    public void RoundToNearest_WithDateUnit_ReturnsCorrectDate(DateUnit unit)
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.RoundToNearest(unit);

        // Assert
        switch (unit)
        {
            case DateUnit.Day:
                result.ShouldBe(date); // DateOnly is already at day level
                break;
            case DateUnit.Week:
                result.ShouldBe(new DateOnly(2024, 3, 11)); // Monday
                break;
            case DateUnit.Month:
                result.ShouldBe(new DateOnly(2024, 3, 1));
                break;
            case DateUnit.Year:
                result.ShouldBe(new DateOnly(2024, 1, 1));
                break;
        }
    }

    [Fact]
    public void RoundToNearest_UnsupportedDateUnit_ThrowsArgumentException()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);
        const int invalidUnit = 999;

        // Act & Assert
        Should.Throw<ArgumentException>(() => date.RoundToNearest((DateUnit)invalidUnit))
            .Message.ShouldContain("Unsupported DateUnit");
    }

    [Fact]
    public void ToUnixTimeSeconds_UsesStartOfDayAtZeroOffset()
    {
        var source = new DateOnly(2026, 1, 1);

        source.ToUnixTimeSeconds().ShouldBe(1767225600L);
    }

    [Fact]
    public void ToUnixTimeMilliseconds_UsesStartOfDayAtZeroOffset()
    {
        var source = new DateOnly(2026, 1, 1);

        source.ToUnixTimeMilliseconds().ShouldBe(1767225600000L);
    }

    [Fact]
    public void AtTime_UsesProvidedTimeAndOffset()
    {
        var source = new DateOnly(2026, 1, 1);

        var result = source.AtTime(new TimeOnly(13, 45, 30), TimeSpan.FromHours(2));

        result.DateTime.ShouldBe(new DateTime(2026, 1, 1, 13, 45, 30));
        result.Offset.ShouldBe(TimeSpan.FromHours(2));
    }

    [Fact]
    public void ToIsoDateString_ReturnsDeterministicString()
    {
        new DateOnly(2026, 6, 29).ToIsoDateString().ShouldBe("2026-06-29");
    }

    [Fact]
    public void BusinessCalendar_DefaultCalendar_TreatsWeekendAsNonWorking()
    {
        var calendar = new BusinessCalendar();

        calendar.IsBusinessDay(new DateOnly(2026, 6, 27)).ShouldBeFalse();
        calendar.IsBusinessDay(new DateOnly(2026, 6, 29)).ShouldBeTrue();
    }

    [Fact]
    public void BusinessCalendar_CustomRules_CanOverrideWeekend()
    {
        var saturday = new DateOnly(2026, 6, 27);
        var calendar = new BusinessCalendar(
            rules:
            [
                new CustomBusinessDayRule(date => date == saturday
                    ? new BusinessDayRuleResult(BusinessDayRuleResultKind.WorkingDay, "Override")
                    : BusinessDayRuleResult.NoMatch)
            ]);

        calendar.IsBusinessDay(saturday).ShouldBeTrue();
    }

    [Fact]
    public void BusinessCalendar_HolidaysAndRanges_WorkAsHalfOpenCalendar()
    {
        var calendar = new BusinessCalendar(
            holidays: [new DateOnly(2026, 1, 1)],
            rules: [new FixedHolidayRule([new FixedHoliday(12, 25, "Christmas")])]);

        calendar.IsBusinessDay(new DateOnly(2026, 1, 1)).ShouldBeFalse();
        calendar.IsBusinessDay(new DateOnly(2026, 12, 25)).ShouldBeFalse();
        calendar.AddBusinessDays(new DateOnly(2025, 12, 31), 1).ShouldBe(new DateOnly(2026, 1, 2));
        calendar.CountBusinessDays(new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 3)).ShouldBe(2);
    }

    [Fact]
    public void BusinessCalendar_ObservedHolidayRule_HonorsObservedHoliday()
    {
        var calendar = new BusinessCalendar(
            rules: [new ObservedHolidayRule([new FixedHoliday(7, 4, "Independence Day")])]);

        calendar.IsBusinessDay(new DateOnly(2026, 7, 3)).ShouldBeFalse();
        calendar.GetBusinessDayInfo(new DateOnly(2026, 7, 3)).Reason.ShouldBe("Observed holiday: Independence Day");
    }

    [Fact]
    public void BusinessCalendar_AddBusinessDays_BackwardSkipsWeekendAndHoliday()
    {
        var calendar = new BusinessCalendar(holidays: [new DateOnly(2026, 1, 1)]);

        calendar.AddBusinessDays(new DateOnly(2026, 1, 5), -2).ShouldBe(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void BusinessCalendar_NextAndPreviousBusinessDay_HonorIncludeCurrent()
    {
        var calendar = new BusinessCalendar();
        var friday = new DateOnly(2026, 6, 26);
        var saturday = new DateOnly(2026, 6, 27);

        calendar.NextBusinessDay(friday, includeCurrent: true).ShouldBe(friday);
        calendar.NextBusinessDay(saturday, includeCurrent: true).ShouldBe(new DateOnly(2026, 6, 29));
        calendar.PreviousBusinessDay(friday, includeCurrent: true).ShouldBe(friday);
        calendar.PreviousBusinessDay(saturday, includeCurrent: true).ShouldBe(friday);
    }

    [Fact]
    public void IsBusinessDay_GlobalCalendarRegistration_ResolvesByCultureCountry()
    {
        var culture = CultureInfo.GetCultureInfo("nl-NL");
        var calendar = new BusinessCalendar(nonWorkingDays: [DayOfWeek.Monday]);
        BusinessCalendars.RegisterCountry("NL", calendar);

        new DateOnly(2026, 6, 29).IsBusinessDay(culture).ShouldBeFalse();
    }

    [Fact]
    public void AddBusinessDays_GlobalCalendarRegistration_ResolvesByCulture()
    {
        var culture = CultureInfo.GetCultureInfo("is-IS");
        var calendar = new BusinessCalendar(nonWorkingDays: [DayOfWeek.Tuesday]);
        BusinessCalendars.Register(culture, calendar);

        new DateOnly(2026, 6, 29).AddBusinessDays(1, culture).ShouldBe(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public void DynamicBusinessCalendar_CalculatedHolidayProvider_CalculatesHolidaysForMultipleYears()
    {
        var calendar = new DynamicBusinessCalendar(
            new CalculatedHolidayProvider([
                new CalculatedHoliday("Good Friday", year => HolidayCalculations.GregorianEasterSunday(year).AddDays(-2))
            ]));

        calendar.GetBusinessDayInfo(new DateOnly(2026, 4, 3)).ShouldSatisfyAllConditions(
            info => info.IsBusinessDay.ShouldBeFalse(),
            info => info.Reason.ShouldBe("Holiday: Good Friday"));
        calendar.GetBusinessDayInfo(new DateOnly(2027, 3, 26)).ShouldSatisfyAllConditions(
            info => info.IsBusinessDay.ShouldBeFalse(),
            info => info.Reason.ShouldBe("Holiday: Good Friday"));
    }

    [Fact]
    public void AddBusinessCalendars_ServiceRegistration_RegistersStaticAndResolverCalendars()
    {
        BusinessCalendars.ResetDefaults();
        var services = new ServiceCollection();
        services.AddBusinessCalendars(calendars => calendars
            .RegisterCountry("NL", new BusinessCalendar(nonWorkingDays: [DayOfWeek.Monday]))
            .Register(CultureInfo.GetCultureInfo("fr-BE"), new BusinessCalendar(nonWorkingDays: [DayOfWeek.Tuesday])));

        services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(BusinessCalendarStartupDiagnosticsService)).ShouldBe(1);

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IBusinessCalendarResolver>();

        new DateOnly(2026, 6, 29).IsBusinessDay(CultureInfo.GetCultureInfo("nl-NL")).ShouldBeFalse();
        resolver.Resolve(CultureInfo.GetCultureInfo("fr-BE")).IsBusinessDay(new DateOnly(2026, 6, 30)).ShouldBeFalse();
        resolver.Resolve("NL").IsBusinessDay(new DateOnly(2026, 6, 29)).ShouldBeFalse();
        BusinessCalendars.Resolve("NL").IsBusinessDay(new DateOnly(2026, 6, 29)).ShouldBeFalse();
    }

    [Fact]
    public void AddBusinessCalendars_ServiceBackedRegistration_ResolvesCalendarWithScopedDependencies()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new TestCalendarDependency(DayOfWeek.Wednesday));
        services.AddBusinessCalendars(calendars => calendars.Register(
            CultureInfo.GetCultureInfo("es-ES"),
            serviceProvider => new BusinessCalendar(nonWorkingDays: [serviceProvider.GetRequiredService<TestCalendarDependency>().NonWorkingDay])));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var calendar = scope.ServiceProvider
            .GetRequiredService<IBusinessCalendarResolver>()
            .Resolve(CultureInfo.GetCultureInfo("es-ES"));

        calendar.IsBusinessDay(new DateOnly(2026, 7, 1)).ShouldBeFalse();
    }

    [Fact]
    public async Task BusinessCalendarStartupDiagnosticsService_WhenStarted_LogsRegistrationsUnderBdkCategoryAtDebug()
    {
        var services = new ServiceCollection();
        services.AddBusinessCalendars(calendars => calendars
            .SetDefault(new BusinessCalendar())
            .RegisterCountry("NL", new BusinessCalendar(nonWorkingDays: [DayOfWeek.Monday]))
            .Register(CultureInfo.GetCultureInfo("fr-BE"), new BusinessCalendar(nonWorkingDays: [DayOfWeek.Tuesday])));
        using var provider = services.BuildServiceProvider();
        var loggerFactory = new RecordingLoggerFactory();
        var sut = new BusinessCalendarStartupDiagnosticsService(provider.GetServices<BusinessCalendarOptions>(), loggerFactory);

        await sut.StartAsync(CancellationToken.None);

        var entry = loggerFactory.Entries.ShouldHaveSingleItem();
        entry.Category.ShouldBe("BDK");
        entry.Level.ShouldBe(LogLevel.Debug);
        entry.Message.ShouldContain("business calendars registered");
        entry.Message.ShouldContain("NL=");
        entry.Message.ShouldContain("fr-BE|BE|fr=");
        entry.Message.ShouldContain(typeof(BusinessCalendar).FullName);
    }

    [Fact]
    public void BusinessCalendar_CountBusinessDays_EndBeforeStart_ThrowsArgumentException()
    {
        var calendar = new BusinessCalendar();

        Should.Throw<ArgumentException>(() => calendar.CountBusinessDays(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1)));
    }

    private sealed record TestCalendarDependency(DayOfWeek NonWorkingDay);

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName)
            => new RecordingLogger(categoryName, this.Entries);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(string category, List<LogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            entries.Add(new LogEntry(category, logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
