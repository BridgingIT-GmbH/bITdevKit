// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;
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
    IPipelineFactory pipelineFactory,
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
        var pipeline = pipelineFactory.Create<WeatherIngestionPipeline, WeatherIngestionContext>();

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
                var pipelineResult = await pipeline.ExecuteAsync(
                    new WeatherIngestionContext(city),
                    (PipelineExecutionOptions)null,
                    cancellationToken);
                if (pipelineResult.IsFailure)
                {
                    failureCount++;
                    logger.LogWarning("weather ingestion pipeline failed for city {CityId} ({CityName}): {Errors}",
                        city.Id, city.Name, string.Join("; ", pipelineResult.Errors.Select(e => e.Message)));
                    continue;
                }

                successCount++;
                logger.LogInformation("Successfully ingested weather for city {CityId} ({CityName})", city.Id, city.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error ingesting weather for city {CityId} ({CityName}): {ErrorMessage}", city.Id, city.Name, ex.Message);
                failureCount++;
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
