// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.WeatherReports;

using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

/// <summary>
/// Unit tests for <see cref="WeatherReportPromptBuilder" />.
/// </summary>
public sealed class WeatherReportPromptBuilderTests
{
    [Theory]
    [InlineData(WeatherReportType.Today, "today")]
    [InlineData(WeatherReportType.Tomorrow, "tomorrow")]
    [InlineData(WeatherReportType.Week, "7-day outlook")]
    [InlineData(WeatherReportType.NextBusinessDay, "next business day")]
    public void Build_ReportType_IncludesReportSpecificInstruction(WeatherReportType reportType, string expectedInstruction)
    {
        // Arrange
        var request = CreateRequest(reportType);

        // Act
        var result = WeatherReportPromptBuilder.Build(request);

        // Assert
        result.System.ShouldContain("Do not invent");
        result.System.ShouldContain("Return plain text only");
        result.User.ShouldContain(expectedInstruction);
        result.User.ShouldContain("Berlin");
    }

    private static WeatherReportTextGenerationRequest CreateRequest(WeatherReportType reportType)
    {
        return new WeatherReportTextGenerationRequest(
            "Berlin",
            reportType,
            new DateTime(2026, 06, 09, 00, 00, 00, DateTimeKind.Utc),
            new DateTime(2026, 06, 10, 00, 00, 00, DateTimeKind.Utc),
            new DateOnly(2026, 06, 09),
            new DateOnly(2026, 06, 10),
            [
                new WeatherForecastDayInput(
                    new DateOnly(2026, 06, 09),
                    2,
                    16m,
                    24m,
                    15m,
                    25m,
                    1.2m,
                    20,
                    18m,
                    31m,
                    225,
                    5m,
                    18000,
                    54000,
                    [])
            ],
            new CurrentWeatherInput(21m, 20m, 55, 2, 14m, 180, 28m, 0.1m, 40, 1015m, DateTime.UtcNow));
    }
}
