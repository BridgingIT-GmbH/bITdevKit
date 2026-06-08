// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Job that periodically ingests weather data for all cities with stale data.
/// Uses <see cref="IWeatherAgent"/> to fetch weather data and persists results via ActiveEntity upserts.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WeatherIngestionJob"/> class.
/// </remarks>
/// <param name="weatherAgent">The weather agent for fetching weather data.</param>
/// <param name="logger">The logger.</param>
public class WeatherIngestionJob(
    IWeatherAgent weatherAgent,
    ILogger<WeatherIngestionJob> logger,
    IOptions<CoreModuleConfiguration> moduleConfiguration) : JobBase
{
    /// <inheritdoc />
    public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("weather ingestion job started at {StartTime}", DateTime.UtcNow);

        var staleSpec = new StaleCitiesForIngestionSpecification(TimeSpan.FromMinutes(moduleConfiguration.Value.StaleThresholdMinutes));
        var citiesResult = await City.FindAllAsync(staleSpec, null, cancellationToken);

        if (citiesResult.IsFailure)
        {
            logger.LogError("Failed to retrieve stale cities: {Errors}", string.Join("; ", citiesResult.Errors.Select(e => e.Message)));
            return Result.Failure(citiesResult.Messages, citiesResult.Errors);
        }

        var cities = citiesResult.Value.ToList();
        logger.LogInformation("Found {CityCount} stale cities to ingest", cities.Count);

        var successCount = 0;
        var failureCount = 0;

        foreach (var city in cities)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("weather ingestion cancelled after processing {ProcessedCount} of {TotalCount} cities", successCount + failureCount, cities.Count);
                break;
            }

            await Task.Delay(1000, cancellationToken); // Artificial delay to simulate work and allow cancellation

            try
            {
                if (city.Location is null)
                {
                    logger.LogWarning("weather ingestion skipped for city {CityId} ({CityName}): location is missing", city.Id, city.Name);
                    failureCount++;
                    continue;
                }

                var ingestResult = await weatherAgent.IngestWeatherAsync(
                    city.Id.ToString(),
                    (double)city.Location.Latitude,
                    (double)city.Location.Longitude,
                    cancellationToken);

                if (ingestResult.IsFailure)
                {
                    logger.LogWarning("weather ingestion failed for city {CityId} ({CityName}): {Errors}",
                        city.Id, city.Name, string.Join("; ", ingestResult.Errors.Select(e => e.Message)));
                    failureCount++;
                    continue;
                }

                var data = ingestResult.Value;

                // Upsert current weather
                if (data.CurrentWeather is not null)
                {
                    var existingCurrentWeather = await CurrentWeather.FindOneAsync(new CurrentWeatherByCitySpecification(city.Id), null, cancellationToken);
                    CurrentWeather currentWeather = null;
                    if (existingCurrentWeather.IsSuccess)
                    {
                        currentWeather = existingCurrentWeather.Value;
                    }
                    else if (existingCurrentWeather.Errors.Any(e => e is NotFoundError))
                    {
                        currentWeather = CurrentWeather.Create(city.Id);
                    }
                    else
                    {
                        logger.LogWarning("Failed to find existing current weather for city {CityId} ({CityName}): {Errors}",
                            city.Id, city.Name, string.Join("; ", existingCurrentWeather.Errors.Select(e => e.Message)));
                        failureCount++;
                    }

                    if (currentWeather is not null)
                    {
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
                            logger.LogWarning("Failed to upsert current weather for city {CityId} ({CityName}): {Errors}",
                                city.Id, city.Name, string.Join("; ", upsertResult.Errors.Select(e => e.Message)));
                        }
                    }
                }

                // Upsert forecasts
                if (data.Forecasts is not null)
                {
                    foreach (var forecastData in data.Forecasts)
                    {
                        var existingForecast = await WeatherForecast.FindOneAsync(new WeatherForecastByCityAndDateSpecification(city.Id, DateOnly.FromDateTime(forecastData.ForecastDate)), null, cancellationToken);
                        WeatherForecast forecast = null;
                        if (existingForecast.IsSuccess)
                        {
                            forecast = existingForecast.Value;
                        }
                        else if (existingForecast.Errors.Any(e => e is NotFoundError))
                        {
                            forecast = WeatherForecast.Create(city.Id);
                        }
                        else
                        {
                            logger.LogWarning("Failed to find existing forecast for city {CityId} ({CityName}) date {ForecastDate}: {Errors}",
                                city.Id, city.Name, forecastData.ForecastDate,
                                string.Join("; ", existingForecast.Errors.Select(e => e.Message)));
                            failureCount++;
                            continue;
                        }

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
                        forecast.HourlyForecasts.Clear();
                        foreach (var h in forecastData.HourlyForecasts ?? [])
                        {
                            forecast.HourlyForecasts.Add(new HourlyForecast
                            {
                                Hour = h.Hour.Hour,
                                Temperature = (decimal)h.TemperatureCelsius,
                                ApparentTemperature = (decimal)h.ApparentTemperatureCelsius,
                                WeatherCode = h.WeatherCode,
                                PrecipitationProbability = (int)h.PrecipitationProbability,
                                Precipitation = (decimal)h.PrecipitationMm,
                                WindSpeed = (decimal)h.WindSpeedKmh,
                                WindDirection = h.WindDirectionDegrees,
                                WindGusts = (decimal)h.WindGustsKmh,
                                RelativeHumidity = (int)h.RelativeHumidity,
                                CloudCover = h.CloudCoverPercent,
                                Visibility = (decimal)h.VisibilityMeters,
                                IsDay = h.IsDay
                            });
                        }

                        var forecastUpsertResult = await forecast.UpsertAsync(cancellationToken);
                        if (forecastUpsertResult.IsFailure)
                        {
                            logger.LogWarning("Failed to upsert forecast for city {CityId} ({CityName}) date {ForecastDate}: {Errors}",
                                city.Id, city.Name, forecastData.ForecastDate,
                                string.Join("; ", forecastUpsertResult.Errors.Select(e => e.Message)));
                        }
                    }
                }

                successCount++;
                logger.LogInformation("Successfully ingested weather for city {CityId} ({CityName})", city.Id, city.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error ingesting weather for city {CityId} ({CityName}): {ErrorMessage}", city.Id, city.Name, ex.Message); failureCount++;
            }
        }

        var message = $"weather ingestion job completed. Success={successCount}, Failed={failureCount}, Total={cities.Count}";
        context.Messages.Add(message);

        logger.LogInformation(
            "weather ingestion job completed at {EndTime}. Success={SuccessCount}, Failed={FailureCount}, Total={TotalCount}",
            DateTime.UtcNow, successCount, failureCount, cities.Count);

        return Result.Success(message);
    }
}
