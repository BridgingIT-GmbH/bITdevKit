// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using Microsoft.Extensions.Logging;
using AppCurrentWeather = Application.Modules.Core.CurrentWeatherData;
using AppHourlyForecast = Application.Modules.Core.HourlyForecastData;

/// <summary>
/// Implements <see cref="IWeatherAgent"/> by delegating to the Open-Meteo API
/// via <see cref="IOpenMeteoClient"/> and mapping raw DTOs to the application-level
/// <see cref="WeatherIngestionResult"/>.
/// </summary>
public class OpenMeteoWeatherAgent : IWeatherAgent
{
    private const string DefaultTimeZone = "auto";
    private const int DefaultForecastDays = 7;

    private readonly IOpenMeteoClient openMeteoClient;
    private readonly ILogger<OpenMeteoWeatherAgent> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenMeteoWeatherAgent"/> class.
    /// </summary>
    /// <param name="openMeteoClient">The Open-Meteo HTTP client.</param>
    /// <param name="logger">The logger.</param>
    public OpenMeteoWeatherAgent(
        IOpenMeteoClient openMeteoClient,
        ILogger<OpenMeteoWeatherAgent> logger)
    {
        this.openMeteoClient = openMeteoClient;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<WeatherIngestionResult>> IngestWeatherAsync(
        string cityId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        try
        {
            this.logger.LogInformation(
                "[OpenMeteoWeatherAgent] Ingesting weather for cityId={CityId} (lat={Latitude}, lon={Longitude})",
                cityId, latitude, longitude);

            var weatherData = await this.openMeteoClient.GetWeatherAsync(
                (decimal)latitude,
                (decimal)longitude,
                DefaultTimeZone,
                DefaultForecastDays,
                cancellationToken);

            if (weatherData is null)
            {
                this.logger.LogWarning(
                    "[OpenMeteoWeatherAgent] No weather data returned for cityId={CityId}",
                    cityId);

                return Result<WeatherIngestionResult>.Failure("No weather data returned from provider.");
            }

            var result = new WeatherIngestionResult
            {
                CurrentWeather = MapCurrentWeather(weatherData.Current),
                Forecasts = weatherData.Daily?.Select(d => MapDailyForecast(d, weatherData.Hourly)).ToList() ?? []
            };

            this.logger.LogInformation(
                "[OpenMeteoWeatherAgent] Weather ingested for cityId={CityId}: temp={Temperature}C, forecasts={ForecastCount}",
                cityId, result.CurrentWeather?.TemperatureCelsius, result.Forecasts.Count);

            return Result<WeatherIngestionResult>.Success(result);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex,
                "[OpenMeteoWeatherAgent] Failed to ingest weather for cityId={CityId}: {ErrorMessage}",
                cityId, ex.Message);

            return Result<WeatherIngestionResult>.Failure($"Weather ingestion failed: {ex.Message}");
        }
    }

    private static AppCurrentWeather MapCurrentWeather(
        CurrentWeatherData source)
    {
        if (source is null)
        {
            return null;
        }

        return new AppCurrentWeather
        {
            TemperatureCelsius = (double)source.Temperature,
            ApparentTemperatureCelsius = (double)source.ApparentTemperature,
            RelativeHumidity = source.Humidity,
            WindSpeedKmh = (double)source.WindSpeed,
            WindDirectionDegrees = source.WindDirection,
            WeatherCode = source.WeatherCode,
            PressureHpa = (double)source.Pressure,
            CloudCoverPercent = source.CloudCover,
            PrecipitationMm = (double)source.Precipitation,
            RetrievedAt = DateTime.UtcNow
        };
    }

    private static ForecastData MapDailyForecast(
        DailyForecastData daily,
        List<HourlyForecastData> allHourly)
    {
        var forecast = new ForecastData
        {
            ForecastDate = daily.Date.ToDateTime(TimeOnly.MinValue),
            TemperatureMaxCelsius = (double)daily.TemperatureMax,
            TemperatureMinCelsius = (double)daily.TemperatureMin,
            WeatherCode = daily.WeatherCode,
            PrecipitationProbability = daily.PrecipitationProbabilityMax,
            WindSpeedMaxKmh = (double)daily.WindSpeedMax,
            Sunrise = daily.Sunrise,
            Sunset = daily.Sunset,
            UvIndex = (double)daily.UvIndexMax
        };

        if (allHourly is not null)
        {
            var dayStart = daily.Date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = daily.Date.ToDateTime(new TimeOnly(23, 59, 59));

            forecast.HourlyForecasts = allHourly
                .Where(h => h.Time >= dayStart && h.Time <= dayEnd)
                .Select(MapHourlyForecast)
                .ToList();
        }

        return forecast;
    }

    private static AppHourlyForecast MapHourlyForecast(
        HourlyForecastData source)
    {
        return new AppHourlyForecast
        {
            Hour = source.Time,
            TemperatureCelsius = (double)source.Temperature,
            ApparentTemperatureCelsius = (double)source.ApparentTemperature,
            WeatherCode = source.WeatherCode,
            PrecipitationProbability = source.PrecipitationProbability,
            PrecipitationMm = (double)source.Precipitation,
            WindSpeedKmh = (double)source.WindSpeed,
            WindDirectionDegrees = source.WindDirection,
            WindGustsKmh = (double)source.WindGusts,
            RelativeHumidity = source.RelativeHumidity,
            CloudCoverPercent = source.CloudCover,
            VisibilityMeters = (double)source.Visibility,
            IsDay = source.IsDay,
            UvIndex = null // Open-Meteo hourly does not provide UV index
        };
    }
}
