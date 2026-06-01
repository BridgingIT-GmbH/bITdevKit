// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>
/// Endpoints for user profile and preferences management.
/// </summary>
public class CoreUserEndpoints : EndpointsBase
{
    /// <summary>
    /// Maps the user-related endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used to define routes.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/core/users")
            .RequireAuthorization()
            .WithTags("Core.Users");

        // GET /api/core/users/me
        group.MapGet("/me",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new UserProfileQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Users.Profile")
            .WithDescription("Gets the current user's profile.")
            .Produces<UserProfileModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // PUT /api/core/users/me
        group.MapPut("/me",
            async ([FromServices] IRequester requester,
                   [FromBody] UserProfileUpdateModel model,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new UserProfileUpdateCommand { Model = model }, cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Users.UpdateProfile")
            .WithDescription("Updates the current user's profile.")
            .Produces<UserProfileModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/users/preferences
        group.MapGet("/preferences",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new UserPreferencesQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Users.Preferences")
            .WithDescription("Gets the current user's unit preferences.")
            .Produces<UnitPreferencesModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // PUT /api/core/users/preferences
        group.MapPut("/preferences",
            async ([FromServices] IRequester requester,
                   [FromBody] UserPreferencesUpdateModel model,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new UserPreferencesUpdateCommand { Model = model }, cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Users.UpdatePreferences")
            .WithDescription("Updates the current user's unit preferences.")
            .Produces<UnitPreferencesModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // DELETE /api/core/users/me
        group.MapDelete("/me",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new UserDeleteCommand(), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.Users.Delete")
            .WithDescription("Soft-deletes the current user and all associated data.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);
    }
}
