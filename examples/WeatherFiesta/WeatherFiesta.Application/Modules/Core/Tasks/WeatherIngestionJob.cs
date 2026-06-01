// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Tasks;

using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

/// <summary>
/// Quartz scheduled job that periodically ingests weather data for all cities with stale data.
/// Uses <see cref="IWeatherAgent"/> to fetch weather data and persists results via ActiveEntity upserts.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WeatherIngestionJob"/> class.
/// </remarks>
/// <param name="weatherAgent">The weather agent for fetching weather data.</param>
/// <param name="loggerFactory">The logger factory.</param>
[DisallowConcurrentExecution]
public class WeatherIngestionJob(
    IWeatherAgent weatherAgent,
    ILoggerFactory loggerFactory) : JobBase(loggerFactory)
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);

    /// <inheritdoc />
    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        this.Logger.LogInformation("Core ingestion job started at {StartTime}", DateTime.UtcNow);

        var staleSpec = new StaleCitiesForIngestionSpecification(StaleThreshold);
        var citiesResult = await City.FindAllAsync(staleSpec, null, cancellationToken);

        if (citiesResult.IsFailure)
        {
            this.Logger.LogError("Failed to retrieve stale cities: {Errors}", string.Join("; ", citiesResult.Errors.Select(e => e.Message)));
            return;
        }

        var cities = citiesResult.Value.ToList();
        this.Logger.LogInformation("Found {CityCount} stale cities to ingest", cities.Count);

        var successCount = 0;
        var failureCount = 0;

        foreach (var city in cities)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                this.Logger.LogWarning("Ingestion cancelled after processing {ProcessedCount} of {TotalCount} cities", successCount + failureCount, cities.Count);
                break;
            }

            try
            {
                var ingestResult = await weatherAgent.IngestWeatherAsync(
                    city.Id.ToString(),
                    (double)city.Location.Latitude,
                    (double)city.Location.Longitude,
                    cancellationToken);

                if (ingestResult.IsFailure)
                {
                    this.Logger.LogWarning("Weather ingestion failed for city {CityId} ({CityName}): {Errors}",
                        city.Id, city.Name, string.Join("; ", ingestResult.Errors.Select(e => e.Message)));
                    failureCount++;
                    continue;
                }

                var data = ingestResult.Value;

                // Upsert current weather
                if (data.CurrentWeather is not null)
                {
                    var currentWeather = CurrentWeather.Create(city.Id);
                    currentWeather.Temperature = (decimal)data.CurrentWeather.TemperatureCelsius;
                    currentWeather.ApparentTemperature = (decimal)data.CurrentWeather.ApparentTemperatureCelsius;
                    currentWeather.Humidity = (int)data.CurrentWeather.RelativeHumidity;
                    currentWeather.WeatherCode = data.CurrentWeather.WeatherCode;
                    currentWeather.WindSpeed = (decimal)data.CurrentWeather.WindSpeedKmh;
                    currentWeather.WindDirection = (int)data.CurrentWeather.WindDirectionDegrees;
                    currentWeather.Precipitation = (decimal)data.CurrentWeather.PrecipitationMm;
                    currentWeather.CloudCover = (int)data.CurrentWeather.CloudCoverPercent;
                    currentWeather.Pressure = (decimal)data.CurrentWeather.PressureHpa;
                    currentWeather.RetrievedAt = data.CurrentWeather.RetrievedAt;

                    var upsertResult = await currentWeather.UpsertAsync(cancellationToken);
                    if (upsertResult.IsFailure)
                    {
                        this.Logger.LogWarning("Failed to upsert current weather for city {CityId} ({CityName}): {Errors}",
                            city.Id, city.Name, string.Join("; ", upsertResult.Errors.Select(e => e.Message)));
                    }
                }

                // Upsert forecasts
                if (data.Forecasts is not null)
                {
                    foreach (var forecastData in data.Forecasts)
                    {
                        var forecast = WeatherForecast.Create(city.Id);
                        forecast.ForecastDate = DateOnly.FromDateTime(forecastData.ForecastDate);
                        forecast.DayWeatherCode = forecastData.WeatherCode;
                        forecast.TemperatureMax = (decimal)forecastData.TemperatureMaxCelsius;
                        forecast.TemperatureMin = (decimal)forecastData.TemperatureMinCelsius;
                        forecast.PrecipitationProbabilityMax = (int)forecastData.PrecipitationProbability;
                        forecast.WindSpeedMax = (decimal)forecastData.WindSpeedMaxKmh;
                        forecast.UvIndexMax = (decimal)(forecastData.UvIndex ?? 0);
                        forecast.Sunrise = forecastData.Sunrise ?? DateTime.MinValue;
                        forecast.Sunset = forecastData.Sunset ?? DateTime.MinValue;
                        forecast.RetrievedAt = DateTime.UtcNow;
                        forecast.HourlyForecasts = forecastData.HourlyForecasts?.Select(h => new HourlyForecast
                        {
                            Hour = h.Hour.Hour,
                            Temperature = (decimal)h.TemperatureCelsius,
                            WeatherCode = h.WeatherCode,
                            PrecipitationProbability = (int)h.PrecipitationProbability,
                            WindSpeed = (decimal)h.WindSpeedKmh,
                            RelativeHumidity = (int)h.RelativeHumidity
                        }).ToList() ?? [];

                        var forecastUpsertResult = await forecast.UpsertAsync(cancellationToken);
                        if (forecastUpsertResult.IsFailure)
                        {
                            this.Logger.LogWarning("Failed to upsert forecast for city {CityId} ({CityName}) date {ForecastDate}: {Errors}",
                                city.Id, city.Name, forecastData.ForecastDate,
                                string.Join("; ", forecastUpsertResult.Errors.Select(e => e.Message)));
                        }
                    }
                }

                successCount++;
                this.Logger.LogInformation("Successfully ingested weather for city {CityId} ({CityName})", city.Id, city.Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Unexpected error ingesting weather for city {CityId} ({CityName}): {ErrorMessage}",
                    city.Id, city.Name, ex.Message);
                failureCount++;
            }
        }

        this.Logger.LogInformation(
            "Core ingestion job completed at {EndTime}. Success={SuccessCount}, Failed={FailureCount}, Total={TotalCount}",
            DateTime.UtcNow, successCount, failureCount, cities.Count);
    }
}
