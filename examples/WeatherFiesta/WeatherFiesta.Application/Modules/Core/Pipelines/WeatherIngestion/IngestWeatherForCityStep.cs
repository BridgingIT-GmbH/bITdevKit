// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using Microsoft.Extensions.Logging;

/// <summary>
/// Fetches weather data for a city and stores it in the pipeline context.
/// </summary>
public sealed class IngestWeatherForCityStep(
    IWeatherAgent weatherAgent,
    ILogger<IngestWeatherForCityStep> logger) : AsyncPipelineStep<WeatherIngestionContext>
{
    /// <inheritdoc />
    protected override async ValueTask<PipelineControl> ExecuteAsync(
        WeatherIngestionContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var city = context.City;
        if (city.Location is null)
        {
            logger.LogWarning("weather ingestion skipped for city {CityId} ({CityName}): location is missing", city.Id, city.Name);
            return PipelineControl.Terminate(Result.Failure().WithError("City location is missing."));
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

            return PipelineControl.Terminate(Result.Failure(ingestResult.Messages, ingestResult.Errors));
        }

        var data = ingestResult.Value;
        context.Data = data;

        return PipelineControl.Continue(result.WithMessage($"Weather ingested for city {city.Id}."));
    }
}