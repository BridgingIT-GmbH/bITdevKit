// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>Exposes the bounded stored-weather review through the real HTTP profiling adapter when capture is enabled.</summary>
/// <example><code>services.AddEndpoints&lt;WeatherProfilingEndpoints&gt;();</code></example>
public sealed class WeatherProfilingEndpoints : EndpointsBase
{
    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetService<ProfilingOptions>();
        if (options?.OperationEnabled != true || options.Requests.Enabled != true)
        {
            return;
        }

        app.MapPost(
                "/api/core/profiling/review",
                async (
                    [FromServices] WeatherCityReviewService reviews,
                    [FromBody] WeatherProfilingReviewRequest request,
                    CancellationToken cancellationToken
                ) =>
                    (
                        await reviews.ReviewAsync(
                            request.CityLimit,
                            request.Repetitions,
                            "api",
                            cancellationToken
                        )
                    ).MapHttpOk()
            )
            .RequireAuthorization(policy => policy.RequireRole(Role.Administrators))
            .WithTags("Core.Profiling")
            .WithName("Core.Profiling.Review")
            .WithDescription(
                "Reviews bounded stored weather data, enriches the request key and records repeated/nested segments."
            )
            .Produces<WeatherCityReviewSummary>()
            .ProducesResultProblem(StatusCodes.Status400BadRequest);
    }
}

/// <summary>Defines the bounded city-review input shared by the dashboard and authenticated HTTP example.</summary>
/// <param name="CityLimit">The maximum distinct cities, between one and ten.</param>
/// <param name="Repetitions">The number of read passes, between one and five.</param>
/// <example><code>var request = new WeatherProfilingReviewRequest(3, 2);</code></example>
public sealed record WeatherProfilingReviewRequest(int CityLimit = 3, int Repetitions = 1);
