// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Persists forecast weather fetched by the weather ingestion pipeline.
/// </summary>
public sealed class PersistWeatherForecastsStep(ILogger<PersistWeatherForecastsStep> logger) : AsyncPipelineStep<WeatherIngestionContext>
{
    /// <inheritdoc />
    protected override async ValueTask<PipelineControl> ExecuteAsync(
        WeatherIngestionContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var city = context.City;
        var forecasts = context.Data?.Forecasts;
        if (forecasts is null)
        {
            logger.LogWarning("weather persistence failed for city {CityId} ({CityName}): forecasts are missing", city.Id, city.Name);
            return PipelineControl.Terminate(Result.Failure().WithError("Forecasts are missing."));
        }

        foreach (var forecastData in forecasts)
        {
            var upsertResult = await this.UpsertForecastAsync(city, forecastData, cancellationToken);
            if (upsertResult.IsFailure)
            {
                return PipelineControl.Terminate(upsertResult);
            }
        }

        return PipelineControl.Continue(result.WithMessage($"Weather forecasts persisted for city {city.Id}."));
    }

    private async Task<Result> UpsertForecastAsync(
        City city,
        ForecastData forecastData,
        CancellationToken cancellationToken)
    {
        var forecastDate = DateOnly.FromDateTime(forecastData.ForecastDate);
        var existingForecast = await WeatherForecast.FindOneAsync(new WeatherForecastByCityAndDateSpecification(city.Id, forecastDate), null, cancellationToken);
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
            return Result.Failure(existingForecast.Messages, existingForecast.Errors);
        }

        forecast.ForecastDate = forecastDate;
        forecast.DayWeatherCode = forecastData.WeatherCode;
        forecast.TemperatureMax = (decimal)forecastData.TemperatureMaxCelsius;
        forecast.TemperatureMin = (decimal)forecastData.TemperatureMinCelsius;
        forecast.ApparentTemperatureMax = (decimal)forecastData.ApparentTemperatureMaxCelsius;
        forecast.ApparentTemperatureMin = (decimal)forecastData.ApparentTemperatureMinCelsius;
        forecast.PrecipitationSum = (decimal)forecastData.PrecipitationSumMm;
        forecast.PrecipitationProbabilityMax = (int)forecastData.PrecipitationProbability;
        forecast.WindSpeedMax = (decimal)forecastData.WindSpeedMaxKmh;
        forecast.WindGustsMax = (decimal)forecastData.WindGustsMaxKmh;
        forecast.DominantWindDirection = forecastData.DominantWindDirectionDegrees;
        forecast.UvIndexMax = (decimal)(forecastData.UvIndex ?? 0);
        forecast.SunshineDurationSeconds = forecastData.SunshineDurationSeconds;
        forecast.DaylightDurationSeconds = forecastData.DaylightDurationSeconds;
        forecast.Sunrise = forecastData.Sunrise ?? DateTime.MinValue;
        forecast.Sunset = forecastData.Sunset ?? DateTime.MinValue;
        forecast.RetrievedAt = DateTime.UtcNow;
        forecast.HourlyForecasts.Clear();

        foreach (var hourlyForecast in forecastData.HourlyForecasts ?? [])
        {
            forecast.HourlyForecasts.Add(new HourlyForecast
            {
                Hour = hourlyForecast.Hour.Hour,
                Temperature = (decimal)hourlyForecast.TemperatureCelsius,
                ApparentTemperature = (decimal)hourlyForecast.ApparentTemperatureCelsius,
                WeatherCode = hourlyForecast.WeatherCode,
                PrecipitationProbability = (int)hourlyForecast.PrecipitationProbability,
                Precipitation = (decimal)hourlyForecast.PrecipitationMm,
                WindSpeed = (decimal)hourlyForecast.WindSpeedKmh,
                WindDirection = hourlyForecast.WindDirectionDegrees,
                WindGusts = (decimal)hourlyForecast.WindGustsKmh,
                RelativeHumidity = (int)hourlyForecast.RelativeHumidity,
                CloudCover = hourlyForecast.CloudCoverPercent,
                Visibility = (decimal)hourlyForecast.VisibilityMeters,
                IsDay = hourlyForecast.IsDay
            });
        }

        var forecastUpsertResult = await forecast.UpsertAsync(cancellationToken);
        if (forecastUpsertResult.IsSuccess)
        {
            return Result.Success();
        }

        logger.LogWarning("Failed to upsert forecast for city {CityId} ({CityName}) date {ForecastDate}: {Errors}",
            city.Id, city.Name, forecastData.ForecastDate,
            string.Join("; ", forecastUpsertResult.Errors.Select(e => e.Message)));
        return Result.Failure(forecastUpsertResult.Messages, forecastUpsertResult.Errors);
    }
}