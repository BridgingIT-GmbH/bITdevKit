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
/// Integration tests for OpenMeteoWeatherAgent against the real Open-Meteo API.
/// Tests the full pipeline: HTTP → OpenMeteoClient → OpenMeteoWeatherAgent → IWeatherAgent result.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "ExternalApi")]
public class OpenMeteoWeatherAgentTests
{
    private readonly IWeatherAgent weatherAgent;
    private readonly IWeatherGeocodingClient geocodingClient;

    public OpenMeteoWeatherAgentTests(ITestOutputHelper output)
    {
        var logger = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new XunitLoggerProvider(output));
        });
        var options = Options.Create(new OpenMeteoClientOptions());
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var openMeteoClient = new OpenMeteoClient(httpClient, options, logger.CreateLogger<OpenMeteoClient>());
        this.weatherAgent = new OpenMeteoWeatherAgent(openMeteoClient, logger.CreateLogger<OpenMeteoWeatherAgent>());
        this.geocodingClient = new OpenMeteoGeocodingClient(openMeteoClient);
    }

    [Fact]
    public async Task IngestWeatherAsync_Paris_ReturnsCurrentWeatherAndForecasts()
    {
        // Act
        var result = await this.weatherAgent.IngestWeatherAsync(
            "paris-test-id",
            48.8566,
            2.3522,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();

        var data = result.Value;
        data.CurrentWeather.ShouldNotBeNull();
        data.CurrentWeather.TemperatureCelsius.ShouldNotBe(0);
        data.CurrentWeather.WeatherCode.ShouldBeInRange(0, 99);
        data.CurrentWeather.RelativeHumidity.ShouldBeInRange(0, 100);
        data.CurrentWeather.WindSpeedKmh.ShouldBeGreaterThan(0);

        data.Forecasts.ShouldNotBeNull();
        data.Forecasts.Count.ShouldBe(7); // Default 7 days

        foreach (var forecast in data.Forecasts)
        {
            forecast.ForecastDate.ShouldNotBe(default);
            forecast.TemperatureMaxCelsius.ShouldBeGreaterThan(forecast.TemperatureMinCelsius);
            forecast.WeatherCode.ShouldBeInRange(0, 99);
        }
    }

    [Fact]
    public async Task IngestWeatherAsync_Amsterdam_ReturnsValidData()
    {
        var result = await this.weatherAgent.IngestWeatherAsync(
            "amsterdam-test-id",
            52.3676,
            4.9041,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CurrentWeather.ShouldNotBeNull();
        result.Value.CurrentWeather.TemperatureCelsius.ShouldBeInRange(-30, 50);
    }

    [Fact]
    public async Task IngestWeatherAsync_London_ReturnsHourlyForecasts()
    {
        var result = await this.weatherAgent.IngestWeatherAsync(
            "london-test-id",
            51.5074,
            -0.1278,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Forecasts.ShouldNotBeNull();

        var firstForecast = result.Value.Forecasts.First();
        firstForecast.HourlyForecasts.ShouldNotBeNull();
        firstForecast.HourlyForecasts.Count.ShouldBeGreaterThan(0);

        foreach (var hourly in firstForecast.HourlyForecasts)
        {
            hourly.Hour.ShouldNotBe(default);
            hourly.TemperatureCelsius.ShouldNotBe(0);
            hourly.WeatherCode.ShouldBeInRange(0, 99);
        }
    }

    [Fact]
    public async Task GeocodingClient_SearchCity_Paris_ReturnsCorrectData()
    {
        var result = await this.geocodingClient.SearchCityAsync("Paris", "FR");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Paris");
        result.Country.ShouldBe("France");
        result.CountryCode.ShouldBe("FR");
        result.Latitude.ShouldNotBe(0);
        result.Longitude.ShouldNotBe(0);
        result.TimeZone.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GeocodingClient_SearchCities_Berlin_ReturnsMultiple()
    {
        var response = await this.geocodingClient.SearchCitiesAsync("Berlin", "DE");

        response.ShouldNotBeNull();
        response.Results.ShouldNotBeNull();
        response.Results.Count.ShouldBeGreaterThan(0);

        var berlin = response.Results.FirstOrDefault(r => r.Name == "Berlin" && r.CountryCode == "DE");
        berlin.ShouldNotBeNull();
        berlin.Latitude.ShouldBeInRange(52m, 53m);
    }

    [Fact]
    public async Task IngestWeatherAsync_RemoteLocation_StillReturnsData()
    {
        // Use coordinates for a remote but valid location (mid-Atlantic, Azores)
        // Open-Meteo returns nearest grid point data
        var result = await this.weatherAgent.IngestWeatherAsync(
            "azores-test",
            38.7223,
            -27.2206,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CurrentWeather.ShouldNotBeNull();
    }
}
