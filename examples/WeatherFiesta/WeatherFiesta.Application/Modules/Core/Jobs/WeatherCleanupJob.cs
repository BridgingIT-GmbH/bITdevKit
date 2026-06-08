// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Job that deletes weather data older than the configured retention window.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WeatherCleanupJob"/> class.
/// </remarks>
/// <param name="logger">The logger.</param>
/// <param name="moduleConfiguration">The core module configuration.</param>
/// <example>
/// Register with the Jobs scheduler and dispatch it manually or through a cron trigger.
/// </example>
public class WeatherCleanupJob(
    ILogger<WeatherCleanupJob> logger,
    IOptions<CoreModuleConfiguration> moduleConfiguration) : JobBase
{
    /// <inheritdoc />
    public override async Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
    {
        var retentionDays = moduleConfiguration.Value.Jobs.CleanupRetentionDays;
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        logger.LogInformation(
            "weather cleanup job started at {StartTime}. RetentionDays={RetentionDays}, Cutoff={Cutoff}",
            DateTime.UtcNow,
            retentionDays,
            cutoff);

        var currentWeatherResult = await CurrentWeather.FindAllAsync(
            new Specification<CurrentWeather>(weather => weather.RetrievedAt < cutoff),
            null,
            cancellationToken);
        if (currentWeatherResult.IsFailure)
        {
            logger.LogError(
                "Failed to retrieve stale current weather: {Errors}",
                string.Join("; ", currentWeatherResult.Errors.Select(e => e.Message)));

            return Result.Failure(currentWeatherResult.Messages, currentWeatherResult.Errors);
        }

        var deleteErrors = new List<IResultError>();
        var deleteMessages = new List<string>();

        var currentWeathers = currentWeatherResult.Value.ToList();
        var currentWeatherDeleted = 0;
        foreach (var weather in currentWeathers)
        {
            var deleteResult = await weather.DeleteAsync(cancellationToken);
            if (deleteResult.IsFailure)
            {
                var deleteMessage = $"Failed to delete stale current weather {weather.Id}.";
                logger.LogError(
                    "Failed to delete stale current weather {WeatherId}: {Errors}",
                    weather.Id,
                    string.Join("; ", deleteResult.Errors.Select(e => e.Message)));

                deleteMessages.Add(deleteMessage);
                deleteMessages.AddRange(deleteResult.Messages);
                deleteErrors.AddRange(deleteResult.Errors);

                continue;
            }

            currentWeatherDeleted++;
        }

        var forecastResult = await WeatherForecast.FindAllAsync(
            new Specification<WeatherForecast>(forecast => forecast.RetrievedAt < cutoff),
            null,
            cancellationToken);
        if (forecastResult.IsFailure)
        {
            logger.LogError(
                "Failed to retrieve stale weather forecasts: {Errors}",
                string.Join("; ", forecastResult.Errors.Select(e => e.Message)));

            return Result.Failure(forecastResult.Messages, forecastResult.Errors);
        }

        var forecasts = forecastResult.Value.ToList();
        var forecastsDeleted = 0;
        foreach (var forecast in forecasts)
        {
            var deleteResult = await forecast.DeleteAsync(cancellationToken);
            if (deleteResult.IsFailure)
            {
                var deleteMessage = $"Failed to delete stale weather forecast {forecast.Id}.";
                logger.LogError(
                    "Failed to delete stale weather forecast {ForecastId}: {Errors}",
                    forecast.Id,
                    string.Join("; ", deleteResult.Errors.Select(e => e.Message)));

                deleteMessages.Add(deleteMessage);
                deleteMessages.AddRange(deleteResult.Messages);
                deleteErrors.AddRange(deleteResult.Errors);

                continue;
            }

            forecastsDeleted++;
        }

        if (deleteErrors.Count > 0)
        {
            var failureMessage = $"weather cleanup job failed. CurrentWeatherDeleted={currentWeatherDeleted}, ForecastsDeleted={forecastsDeleted}, Failures={deleteErrors.Count}, RetentionDays={retentionDays}";
            var failureMessages = new List<string> { failureMessage };
            failureMessages.AddRange(deleteMessages);
            context.Messages.Add(failureMessage);

            return Result.Failure(failureMessages, deleteErrors);
        }

        var message = $"weather cleanup job completed. CurrentWeatherDeleted={currentWeatherDeleted}, ForecastsDeleted={forecastsDeleted}, RetentionDays={retentionDays}";
        context.Messages.Add(message);

        logger.LogInformation(
            "weather cleanup job completed at {EndTime}. CurrentWeatherDeleted={CurrentWeatherDeleted}, ForecastsDeleted={ForecastsDeleted}",
            DateTime.UtcNow,
            currentWeatherDeleted,
            forecastsDeleted);

        return Result.Success(message);
    }
}
