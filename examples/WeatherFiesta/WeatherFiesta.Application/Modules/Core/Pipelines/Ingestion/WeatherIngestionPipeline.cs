// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>
/// Pipeline that ingests weather data for one city and queues report generation work afterwards.
/// </summary>
public sealed class WeatherIngestionPipeline : PipelineDefinition<WeatherIngestionContext>
{
    /// <inheritdoc />
    protected override void Configure(IPipelineDefinitionBuilder<WeatherIngestionContext> builder)
    {
        builder
            .AddStep<IngestWeatherForCityStep>()
            .AddStep<PersistCurrentWeatherStep>()
            .AddStep<PersistWeatherForecastsStep>()
            .AddStep<ResolveWeatherReportPeriodsStep>()
            .AddStep<EnqueueWeatherReportGenerationStep>();
    }
}