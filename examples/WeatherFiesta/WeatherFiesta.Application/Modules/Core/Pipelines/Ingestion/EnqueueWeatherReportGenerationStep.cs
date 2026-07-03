// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Enqueues generated weather report requests after weather data has been persisted.
/// </summary>
public sealed class EnqueueWeatherReportGenerationStep(
    IQueueBroker queueBroker) : AsyncPipelineStep<WeatherIngestionContext>
{
    /// <inheritdoc />
    protected override async ValueTask<PipelineControl> ExecuteAsync(
        WeatherIngestionContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var city = context.City;
        if (context.TodayReportPeriod is null || context.TomorrowReportPeriod is null || context.WeekReportPeriod is null || context.NextBusinessDayReportPeriod is null)
        {
            return PipelineControl.Terminate(Result.Failure().WithError("Weather report periods were not resolved."));
        }

        await this.EnqueueAsync(city.Id, WeatherReportType.Today, context.TodayReportPeriod, cancellationToken);
        await this.EnqueueAsync(city.Id, WeatherReportType.Tomorrow, context.TomorrowReportPeriod, cancellationToken);
        await this.EnqueueAsync(city.Id, WeatherReportType.Week, context.WeekReportPeriod, cancellationToken);
        await this.EnqueueAsync(city.Id, WeatherReportType.NextBusinessDay, context.NextBusinessDayReportPeriod, cancellationToken);

        return PipelineControl.Continue(result.WithMessage($"Weather report generation queued for city {city.Id}."));
    }

    private Task EnqueueAsync(
        CityId cityId,
        WeatherReportType reportType,
        WeatherReportPeriod period,
        CancellationToken cancellationToken)
    {
        return queueBroker.Enqueue(
            new WeatherReportGenerationMessage(cityId, reportType, period),
            cancellationToken);
    }
}
