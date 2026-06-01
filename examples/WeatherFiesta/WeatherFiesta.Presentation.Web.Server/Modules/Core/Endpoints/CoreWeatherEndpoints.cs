// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>
/// Endpoints for weather-related operations including alerts, sun data, comparison, and export.
/// </summary>
public class CoreWeatherEndpoints : EndpointsBase
{
    /// <summary>
    /// Maps the weather-related endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used to define routes.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/core/cities")
            .RequireAuthorization()
            .WithTags("Core.Weather");

        // GET /api/core/cities/alerts
        group.MapGet("/alerts",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityAlertsQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Weather.Alerts")
            .WithDescription("Gets weather alerts for all subscribed cities.")
            .Produces<List<CityAlertsModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities/{cityId}/sun
        group.MapGet("/{cityId}/sun",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   [FromQuery] int? days,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CitySunQuery(cityId, days), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Weather.Sun")
            .WithDescription("Gets sunrise/sunset data for a subscribed city.")
            .Produces<CitySunResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities/compare
        group.MapGet("/compare",
            async ([FromServices] IRequester requester,
                   [FromBody] List<string> cityIds,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityCompareQuery { CityIds = cityIds }, cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Weather.Compare")
            .WithDescription("Compares current weather across multiple subscribed cities.")
            .Produces<CityCompareResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities/export
        group.MapGet("/export",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityExportQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Weather.Export")
            .WithDescription("Exports current weather for all subscribed cities as CSV.")
            .Produces<CityExportResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities/{cityId}/weather/export
        group.MapGet("/{cityId}/weather/export",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   [FromQuery] int? days,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityWeatherExportQuery(cityId, days), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Weather.WeatherExport")
            .WithDescription("Exports forecast data for a specific subscribed city as CSV.")
            .Produces<CityExportResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities/{cityId}/recommendations
        group.MapGet("/{cityId}/recommendations",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityRecommendationsQuery(cityId), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Weather.Recommendations")
            .WithDescription("Gets weather recommendations for a subscribed city.")
            .Produces<CityRecommendationsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);
    }
}
