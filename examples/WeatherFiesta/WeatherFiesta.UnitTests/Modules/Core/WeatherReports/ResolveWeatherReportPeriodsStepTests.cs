// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.WeatherReports;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit tests for <see cref="ResolveWeatherReportPeriodsStep" />.
/// </summary>
public sealed class ResolveWeatherReportPeriodsStepTests
{
    [Fact]
    public async Task Execute_EuropeBerlin_ResolvesCityLocalReportPeriods()
    {
        // Arrange
        var sut = new ResolveWeatherReportPeriodsStep(
            NullLogger<ResolveWeatherReportPeriodsStep>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 06, 09, 10, 00, 00, TimeSpan.Zero)));
        var context = new WeatherIngestionContext(CreateCity("Europe/Berlin"));

        // Act
        var control = await ((IPipelineStep)sut).ExecuteAsync(
            context,
            Result.Success(),
            new PipelineExecutionOptions(),
            CancellationToken.None);

        // Assert
        control.Result.IsSuccess.ShouldBeTrue();
        context.TodayReportPeriod.ForecastDateStart.ShouldBe(new DateOnly(2026, 06, 09));
        context.TodayReportPeriod.ForecastDateEndExclusive.ShouldBe(new DateOnly(2026, 06, 10));
        context.TodayReportPeriod.PeriodStartUtc.ShouldBe(new DateTime(2026, 06, 08, 22, 00, 00, DateTimeKind.Utc));
        context.TodayReportPeriod.PeriodEndUtc.ShouldBe(new DateTime(2026, 06, 09, 22, 00, 00, DateTimeKind.Utc));
        context.TomorrowReportPeriod.ForecastDateStart.ShouldBe(new DateOnly(2026, 06, 10));
        context.WeekReportPeriod.ForecastDateEndExclusive.ShouldBe(new DateOnly(2026, 06, 16));
    }

    [Fact]
    public async Task Execute_AmericaNewYork_ResolvesNextCityLocalDay()
    {
        // Arrange
        var sut = new ResolveWeatherReportPeriodsStep(
            NullLogger<ResolveWeatherReportPeriodsStep>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 06, 09, 03, 30, 00, TimeSpan.Zero)));
        var context = new WeatherIngestionContext(CreateCity("America/New_York"));

        // Act
        var control = await ((IPipelineStep)sut).ExecuteAsync(
            context,
            Result.Success(),
            new PipelineExecutionOptions(),
            CancellationToken.None);

        // Assert
        control.Result.IsSuccess.ShouldBeTrue();
        context.TomorrowReportPeriod.ForecastDateStart.ShouldBe(new DateOnly(2026, 06, 09));
        context.TomorrowReportPeriod.ForecastDateEndExclusive.ShouldBe(new DateOnly(2026, 06, 10));
        context.TomorrowReportPeriod.PeriodStartUtc.ShouldBe(new DateTime(2026, 06, 09, 04, 00, 00, DateTimeKind.Utc));
        context.TomorrowReportPeriod.PeriodEndUtc.ShouldBe(new DateTime(2026, 06, 10, 04, 00, 00, DateTimeKind.Utc));
    }

    private static City CreateCity(string timeZoneId)
    {
        var city = City.Create(
            "Test City",
            "Test Country",
            "TC",
            timeZoneId,
            Location.Create(51.5074m, -0.1278m).Value,
            123,
            11m);
        city.Id = CityId.Create(Guid.NewGuid());

        return city;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}