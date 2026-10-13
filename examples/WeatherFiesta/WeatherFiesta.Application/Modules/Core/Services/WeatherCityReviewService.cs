// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

/// <summary>Reviews stored city weather through real application handlers with bounded, aggregated profiling segments.</summary>
/// <param name="requester">The application request dispatcher.</param>
/// <param name="profiling">Optional operation capture.</param>
/// <example><code>var result = await reviews.ReviewAsync(3, 2, "dashboard", cancellationToken);</code></example>
public sealed class WeatherCityReviewService(
    IRequester requester,
    IOperationProfiler profiling = null
)
{
    /// <summary>Reads at most ten cities up to five times, joining an existing operation or starting a service operation.</summary>
    /// <param name="cityLimit">The maximum number of cities, between one and ten.</param>
    /// <param name="repetitions">The number of review passes, between one and five.</param>
    /// <param name="source">The adapter's bounded source label.</param>
    /// <param name="cancellationToken">Cancels the application work.</param>
    /// <returns>The city and weather counts, average temperature, and optional captured occurrence ID.</returns>
    /// <example><code>await reviews.ReviewAsync(5, 1, "api", cancellationToken);</code></example>
    public Task<Result<WeatherCityReviewSummary>> ReviewAsync(
        int cityLimit = 3,
        int repetitions = 1,
        string source = "service",
        CancellationToken cancellationToken = default
    )
    {
        if (cityLimit is < 1 or > 10 || repetitions is < 1 or > 5)
        {
            return Task.FromResult(
                Result<WeatherCityReviewSummary>.Failure(
                    new ValidationError("Choose 1–10 cities and 1–5 repetitions.")
                )
            );
        }

        return profiling.JoinOrStartAsync(
            "weather:city-review",
            OperationProfilingKind.Service,
            async (scope, token) =>
            {
                Guid? operationId = null;
                Observe(() =>
                {
                    var operation = profiling?.Current;
                    operation?.SetKey("weather:city-review");
                    operation?.SetDimension("cityLimit", cityLimit);
                    operation?.SetDimension("repetitions", repetitions);
                    operation?.SetDimension("source", source);
                    operationId = operation?.IsRecording == true ? operation.Id : null;
                });

                var cities = await scope.RunSegmentAsync(
                    "LoadCities",
                    (_, ct) => requester.SendAsync(new AdminCitiesQuery(), cancellationToken: ct),
                    token,
                    value => OperationProfilingHelpers.ClassifyResult(value)
                );
                if (cities.IsFailure)
                {
                    return cities.Wrap<WeatherCityReviewSummary>();
                }

                var selected = cities
                    .Value.OrderBy(city => city.Id, StringComparer.Ordinal)
                    .Take(cityLimit)
                    .ToArray();
                var temperatures = new List<decimal>();
                var missing = 0;
                for (var pass = 0; pass < repetitions; pass++)
                {
                    foreach (var city in selected)
                    {
                        token.ThrowIfCancellationRequested();
                        var weather = await scope.RunSegmentAsync(
                            "ReadWeather",
                            (_, ct) =>
                                requester.SendAsync(
                                    new AdminCityWeatherQuery(city.Id),
                                    cancellationToken: ct
                                ),
                            token,
                            value => OperationProfilingHelpers.ClassifyResult(value)
                        );
                        if (weather.IsSuccess)
                        {
                            temperatures.Add(weather.Value.Temperature);
                        }
                        else
                        {
                            missing++;
                        }
                    }
                }

                return scope.RunSegment(
                    "Summarize",
                    segment =>
                    {
                        Observe(() =>
                        {
                            segment.SetMeasurement(
                                "weatherReads",
                                temperatures.Count + missing,
                                "count"
                            );
                            profiling?.Current?.SetMeasurement("cities", selected.Length, "count");
                            profiling?.Current?.SetMeasurement(
                                "weatherReads",
                                temperatures.Count + missing,
                                "count"
                            );
                            profiling?.Current?.SetMeasurement("missingWeather", missing, "count");
                        });
                        return Result<WeatherCityReviewSummary>.Success(
                            new(
                                operationId,
                                selected.Length,
                                temperatures.Count,
                                missing,
                                temperatures.Count == 0 ? null : temperatures.Average()
                            )
                        );
                    }
                );
            },
            cancellationToken,
            value => OperationProfilingHelpers.ClassifyResult(value)
        );
    }

    private static void Observe(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        { /* Observation never changes the application result. */
        }
    }
}

/// <summary>Contains bounded stored-weather review results without retaining weather payloads in profiling storage.</summary>
/// <param name="OperationId">The admitted operation occurrence, or null when capture is unavailable.</param>
/// <param name="CityCount">The number of distinct reviewed cities.</param>
/// <param name="WeatherReadCount">The number of successful weather reads across all passes.</param>
/// <param name="MissingWeatherCount">The number of unsuccessful weather reads, also classified on their segments.</param>
/// <param name="AverageTemperature">The average observed Celsius temperature, or null when no weather is available.</param>
/// <example><code>var captured = summary.OperationId.HasValue;</code></example>
public sealed record WeatherCityReviewSummary(
    Guid? OperationId,
    int CityCount,
    int WeatherReadCount,
    int MissingWeatherCount,
    decimal? AverageTemperature
);
