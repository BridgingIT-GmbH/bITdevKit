// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Infrastructure;

using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

/// <summary>
/// Integration tests for OpenMeteoClient against the real Open-Meteo API.
/// These tests require network access and verify actual API responses.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "ExternalApi")]
public class OpenMeteoClientTests
{
    private readonly OpenMeteoClient client;
    private readonly ILogger<OpenMeteoClient> logger;

    public OpenMeteoClientTests(ITestOutputHelper output)
    {
        this.logger = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new XunitLoggerProvider(output));
        }).CreateLogger<OpenMeteoClient>();
        var options = Options.Create(new OpenMeteoClientOptions());
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        this.client = new OpenMeteoClient(httpClient, options, this.logger);
    }

    [Fact]
    public async Task SearchCitiesAsync_Paris_ReturnsResults()
    {
        // Act
        var result = await this.client.SearchCitiesAsync("Paris", "FR");

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Paris");
        result.Country.ShouldBe("France");
        result.CountryCode.ShouldBe("FR");
        result.Latitude.ShouldNotBe(0);
        result.Longitude.ShouldNotBe(0);
        result.TimeZone.ShouldNotBeNullOrEmpty();
        result.ExternalId.ShouldNotBeNull();
    }

    [Fact]
    public async Task SearchCitiesAsync_London_ReturnsResults()
    {
        var result = await this.client.SearchCitiesAsync("London");

        result.ShouldNotBeNull();
        result.Name.ShouldContain("London");
        result.Country.ShouldNotBeNullOrEmpty();
        result.Latitude.ShouldNotBe(0);
        result.Longitude.ShouldNotBe(0);
    }

    [Fact]
    public async Task SearchCitiesAsync_NonExistentCity_ReturnsNull()
    {
        var result = await this.client.SearchCitiesAsync("ZZZZZZZZZZZ");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SearchCitiesAllAsync_Berlin_ReturnsMultipleResults()
    {
        var response = await this.client.SearchCitiesAllAsync("Berlin");

        response.ShouldNotBeNull();
        response.Results.ShouldNotBeNull();
        response.Results.Count.ShouldBeGreaterThan(0);

        var berlin = response.Results.FirstOrDefault(r => r.CountryCode == "DE" && r.Name == "Berlin");
        berlin.ShouldNotBeNull();
        berlin.CountryCode.ShouldBe("DE");
        berlin.Latitude.ShouldBeInRange(52m, 53m);
        berlin.Longitude.ShouldBeInRange(13m, 14m);
    }

    [Fact]
    public async Task SearchCitiesAllAsync_WithCountryCode_ReturnsResults()
    {
        var response = await this.client.SearchCitiesAllAsync("Nice", "FR");

        response.ShouldNotBeNull();
        response.Results.ShouldNotBeNull();
        response.Results.Count.ShouldBeGreaterThan(0);

        // Open-Meteo geocoding API does not filter server-side by country_code;
        // the parameter is appended to the URL but results include all matches.
        // Verify at least one result is from the requested country.
        response.Results.ShouldContain(r => r.CountryCode == "FR");
    }

    [Fact]
    public async Task LookupCityAsync_KnownId_ReturnsCity()
    {
        // First search for Paris to get a valid Open-Meteo ID
        var searchResult = await this.client.SearchCitiesAsync("Paris", "FR");
        searchResult.ShouldNotBeNull();
        searchResult.ExternalId.ShouldNotBeNull();

        // Now look up by the discovered ID
        var result = await this.client.LookupCityAsync(searchResult.ExternalId.Value);

        result.ShouldNotBeNull();
        result.Name.ShouldContain("Paris");
        result.CountryCode.ShouldBe("FR");
    }

    [Fact]
    public async Task GetWeatherAsync_Paris_ReturnsCurrentAndDailyAndHourly()
    {
        // Paris coordinates
        var result = await this.client.GetWeatherAsync(48.8566m, 2.3522m, "Europe/Paris", 3);

        // Current weather
        result.ShouldNotBeNull();
        result.Current.ShouldNotBeNull();
        result.Current.Temperature.ShouldNotBe(0); // Should have a real temperature
        result.Current.WeatherCode.ShouldBeInRange(0, 99); // WMO code range
        result.Current.WindSpeed.ShouldBeGreaterThan(0);
        result.Current.Humidity.ShouldBeInRange(0, 100);
        result.Current.Pressure.ShouldBeGreaterThan(900); // hPa range
        result.Current.CloudCover.ShouldBeInRange(0, 100);

        // Daily forecasts
        result.Daily.ShouldNotBeNull();
        result.Daily.Count.ShouldBe(3); // 3 forecast days requested

        foreach (var day in result.Daily)
        {
            day.Date.ShouldNotBe(default);
            day.WeatherCode.ShouldBeInRange(0, 99);
            day.TemperatureMax.ShouldBeGreaterThan(day.TemperatureMin);
            day.WindSpeedMax.ShouldBeGreaterThan(0);
            day.Sunrise.ShouldNotBe(default);
            day.Sunset.ShouldNotBe(default);
        }

        // Hourly forecasts
        result.Hourly.ShouldNotBeNull();
        result.Hourly.Count.ShouldBeGreaterThan(0);

        foreach (var hour in result.Hourly)
        {
            hour.Temperature.ShouldNotBe(0);
            hour.WeatherCode.ShouldBeInRange(0, 99);
            hour.RelativeHumidity.ShouldBeInRange(0, 100);
        }
    }

    [Fact]
    public async Task GetWeatherAsync_Amsterdam_ReturnsValidData()
    {
        var result = await this.client.GetWeatherAsync(52.3676m, 4.9041m, "Europe/Amsterdam", 1);

        result.ShouldNotBeNull();
        result.Current.ShouldNotBeNull();
        result.Current.Temperature.ShouldBeInRange(-30m, 50m); // Reasonable temperature range
    }

    [Fact]
    public async Task GetWeatherAsync_NullableFields_HandleGracefully()
    {
        // Verify that nullable fields (precipitation_probability_max, uv_index_max, etc.)
        // don't crash the parser
        var result = await this.client.GetWeatherAsync(51.5074m, -0.1278m, "Europe/London", 7);

        result.ShouldNotBeNull();
        result.Daily.ShouldNotBeNull();
        result.Daily.Count.ShouldBe(7);

        // These fields may be null for some days — verify they don't throw
        foreach (var day in result.Daily)
        {
            // PrecipitationProbabilityMax can be null for some forecast days
            // UvIndexMax can be null at night
            // The parser should handle these gracefully with defaults
        }
    }
}
