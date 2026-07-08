// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Enqueues document-storage archive work after weather ingestion data has been persisted.
/// </summary>
/// <param name="queueBroker">The queue broker used to enqueue archive work.</param>
/// <example>
/// <code>
/// builder.AddStep&lt;EnqueueOpenMeteoWeatherArchiveStep&gt;();
/// </code>
/// </example>
public sealed class EnqueueOpenMeteoWeatherArchiveStep(
    IQueueBroker queueBroker) : AsyncPipelineStep<WeatherIngestionContext>
{
    /// <inheritdoc />
    protected override async ValueTask<PipelineControl> ExecuteAsync(
        WeatherIngestionContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken)
    {
        if (context.Data is null)
        {
            return PipelineControl.Terminate(Result.Failure().WithError("Weather ingestion data is missing."));
        }

        await queueBroker.Enqueue(new OpenMeteoWeatherArchiveMessage(context.City, context.Data), cancellationToken);

        return PipelineControl.Continue(result.WithMessage($"Open-Meteo weather archive queued for city {context.City.Id}."));
    }
}
