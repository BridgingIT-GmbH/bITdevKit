// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>
/// Endpoints for administrative city management operations.
/// All endpoints require the CoreAdmin role.
/// </summary>
public class AdminEndpoints : EndpointsBase
{
    /// <summary>
    /// Maps the admin endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used to define routes.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/core/admin/cities")
            .RequireAuthorization(policy => policy.RequireRole("CoreAdmin"))
            .WithTags("Core.Admin");

        // POST /api/core/admin/cities
        group.MapPost("/",
            async ([FromServices] IRequester requester,
                   [FromBody] AdminCityCreateModel model,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminCityCreateCommand { Model = model }, cancellationToken: ct))
                    .MapHttpCreated(value => $"/api/core/admin/cities/{value.Id}"))
            .WithName("Core.Admin.Cities.Create")
            .WithDescription("Creates a city directly without geocoding.")
            .Produces<CityModel>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // PUT /api/core/admin/cities/{cityId}
        group.MapPut("/{cityId}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   [FromBody] AdminCityUpdateModel model,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminCityUpdateCommand(cityId, model), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Admin.Cities.Update")
            .WithDescription("Updates city details.")
            .Produces<CityModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // DELETE /api/core/admin/cities/{cityId}
        group.MapDelete("/{cityId}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminCityDeleteCommand(cityId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Admin.Cities.Delete")
            .WithDescription("Hard-deletes a city and all associated data.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/admin/cities
        group.MapGet("/",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminCitiesQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Admin.Cities.List")
            .WithDescription("Lists all cities with subscription counts.")
            .Produces<List<AdminCityModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/admin/cities/{cityId}/subscriptions
        group.MapGet("/{cityId}/subscriptions",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminCitySubscriptionsQuery(cityId), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Admin.Cities.Subscriptions")
            .WithDescription("Lists subscriptions for a city including soft-deleted.")
            .Produces<List<AdminCitySubscriptionModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // POST /api/core/admin/cities/{cityId}/ingest
        group.MapPost("/{cityId}/ingest",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminCityIngestCommand(cityId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Admin.Cities.Ingest")
            .WithDescription("Triggers weather data ingestion for a city without subscription check.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // DELETE /api/core/admin/cities/{cityId}/weather
        group.MapDelete("/{cityId}/weather",
            async ([FromServices] IRequester requester,
                   [FromRoute] string cityId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminCityWeatherResetCommand(cityId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Admin.Cities.WeatherReset")
            .WithDescription("Deletes all weather data for a city.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // DELETE /api/core/admin/users/{userId}
        var usersGroup = app
            .MapGroup("api/core/admin/users")
            .RequireAuthorization(policy => policy.RequireRole("CoreAdmin"))
            .WithTags("Core.Admin");

        usersGroup.MapDelete("/{userId}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string userId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminUserDeleteCommand(userId), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Admin.Users.Delete")
            .WithDescription("Hard-deletes a user and all associated data.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);
    }
}
