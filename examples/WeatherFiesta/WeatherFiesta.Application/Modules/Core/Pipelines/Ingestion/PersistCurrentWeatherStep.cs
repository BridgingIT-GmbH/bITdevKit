// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Persists current weather fetched by the weather ingestion pipeline.
/// </summary>
public sealed class PersistCurrentWeatherStep(ILogger<PersistCurrentWeatherStep> logger) : AsyncPipelineStep<WeatherIngestionContext>
{
    /// <inheritdoc />
    protected override async ValueTask<PipelineControl> ExecuteAsync(
        WeatherIngestionContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var city = context.City;
        var currentWeatherData = context.Data?.CurrentWeather;
        if (currentWeatherData is null)
        {
            logger.LogWarning("weather persistence failed for city {CityId} ({CityName}): current weather is missing", city.Id, city.Name);
            return PipelineControl.Terminate(Result.Failure().WithError("Current weather is missing."));
        }

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
            return PipelineControl.Terminate(Result.Failure(existingCurrentWeather.Messages, existingCurrentWeather.Errors));
        }

        currentWeather.Temperature = (decimal)currentWeatherData.TemperatureCelsius;
        currentWeather.ApparentTemperature = (decimal)currentWeatherData.ApparentTemperatureCelsius;
        currentWeather.Humidity = (int)currentWeatherData.RelativeHumidity;
        currentWeather.WeatherCode = currentWeatherData.WeatherCode;
        currentWeather.WindSpeed = (decimal)currentWeatherData.WindSpeedKmh;
        currentWeather.WindGusts = (decimal)currentWeatherData.WindGustsKmh;
        currentWeather.WindDirection = (int)currentWeatherData.WindDirectionDegrees;
        currentWeather.Precipitation = (decimal)currentWeatherData.PrecipitationMm;
        currentWeather.CloudCover = (int)currentWeatherData.CloudCoverPercent;
        currentWeather.Pressure = (decimal)currentWeatherData.PressureHpa;
        currentWeather.RetrievedAt = currentWeatherData.RetrievedAt;

        var upsertResult = await currentWeather.UpsertAsync(cancellationToken);
        if (upsertResult.IsSuccess)
        {
            return PipelineControl.Continue(result.WithMessage($"Current weather persisted for city {city.Id}."));
        }

        logger.LogWarning("Failed to upsert current weather for city {CityId} ({CityName}): {Errors}",
            city.Id, city.Name, string.Join("; ", upsertResult.Errors.Select(e => e.Message)));
        return PipelineControl.Terminate(Result.Failure(upsertResult.Messages, upsertResult.Errors));
    }
}