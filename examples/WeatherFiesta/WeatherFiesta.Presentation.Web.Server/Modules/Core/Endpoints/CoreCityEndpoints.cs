// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>
/// Endpoints for city management operations including subscriptions, weather, and ingestion.
/// </summary>
public class CoreCityEndpoints : EndpointsBase
{
    /// <summary>
    /// Maps the city-related endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used to define routes.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/core/cities")
            .RequireAuthorization()
            .WithTags("Core.Cities");

        // GET /api/core/cities/suggestions
        group.MapGet("/suggestions",
            async ([FromServices] IRequester requester,
                   [FromQuery] string search,
                   [FromQuery] string countryCode,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CitySuggestionQuery(search, countryCode), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Cities.Suggestions")
            .WithDescription("Searches for city suggestions using the Open-Meteo geocoding API.")
            .Produces<List<CitySuggestionModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // POST /api/core/cities
        group.MapPost("/",
            async ([FromServices] IRequester requester,
                   [FromBody] CityCreateModel model,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityCreateCommand { Model = model }, cancellationToken: ct))
                    .MapHttpCreated(value => $"/api/core/cities/{value.Id}"))
            .WithName("Core.Cities.Create")
            .WithDescription("Creates a city and subscribes the current user to it.")
            .Produces<CityModel>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities
        group.MapGet("/",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new UserCitiesQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Cities.List")
            .WithDescription("Lists all city subscriptions for the current user with weather data.")
            .Produces<List<UserCityModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // DELETE /api/core/cities/{cityId}
        group.MapDelete("/{cityId}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityUnsubscribeCommand(cityId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Cities.Unsubscribe")
            .WithDescription("Unsubscribes the current user from a city.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // PUT /api/core/cities/{cityId}/primary
        group.MapPut("/{cityId}/primary",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new SetPrimaryCityCommand(cityId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Cities.SetPrimary")
            .WithDescription("Sets a city as the user's primary city.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // PUT /api/core/cities/reorder
        group.MapPut("/reorder",
            async ([FromServices] IRequester requester,
                   [FromBody] List<string> cityIds,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new ReorderCitiesCommand { CityIds = cityIds }, cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Cities.Reorder")
            .WithDescription("Reorders the user's city subscriptions.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/cities/{cityId}/weather
        group.MapGet("/{cityId}/weather",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   [FromQuery] int? forecastDays,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityWeatherQuery(cityId, forecastDays), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Cities.Weather")
            .WithDescription("Gets current weather and forecasts for a subscribed city.")
            .Produces<CityWeatherResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // POST /api/core/cities/{cityId}/ingest
        group.MapPost("/{cityId}/ingest",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new CityIngestCommand(cityId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Cities.Ingest")
            .WithDescription("Triggers weather data ingestion for a subscribed city.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);
    }
}
