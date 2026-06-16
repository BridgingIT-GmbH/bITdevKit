// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.Logging;

/// <summary>
/// Generates related WeatherFiesta error logs for testing the dashboard Errors page.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WeatherErrorSimulationJob"/> class.
/// </remarks>
/// <param name="logger">The logger.</param>
/// <example>
/// Register the job with a manual trigger and dispatch it from the Jobs dashboard.
/// </example>
public class WeatherErrorSimulationJob(ILogger<WeatherErrorSimulationJob> logger) : JobBase
{
    /// <inheritdoc />
    public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
    {
        for (var index = 1; index <= 3; index++)
        {
            logger.LogInformation("Weather error simulation started: #{Index}", index);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                throw new InvalidOperationException($"Synthetic WeatherFiesta error #{index} for dashboard validation.");
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Synthetic weather error {ErrorIndex} generated for dashboard validation.",
                    index);
            }
        }

        logger.LogCritical("Synthetic critical weather failure generated for dashboard validation.");

        const string message = "Generated synthetic WeatherFiesta errors.";
        context.Messages.Add(message);

        return Task.FromResult(Result.Success(message));
    }
}
