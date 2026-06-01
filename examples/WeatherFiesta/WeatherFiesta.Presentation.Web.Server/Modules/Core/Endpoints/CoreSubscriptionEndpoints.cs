// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>
/// Endpoints for subscription management including user self-service and admin operations.
/// </summary>
public class CoreSubscriptionEndpoints : EndpointsBase
{
    /// <summary>
    /// Maps the subscription-related endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used to define routes.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        // User endpoints
        var userGroup = app
            .MapGroup("api/core/users")
            .RequireAuthorization()
            .WithTags("Core.Subscription");

        // GET /api/core/users/subscription
        userGroup.MapGet("/subscription",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new UserSubscriptionQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Subscription.Get")
            .WithDescription("Gets the current user's subscription plan details.")
            .Produces<UserSubscriptionModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // Admin endpoints
        var adminGroup = app
            .MapGroup("api/core/admin/subscriptions")
            .RequireAuthorization(policy => policy.RequireRole("CoreAdmin"))
            .WithTags("Core.Admin.Subscription");

        // GET /api/core/admin/subscriptions
        adminGroup.MapGet("/",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminUserSubscriptionsQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Admin.Subscriptions.List")
            .WithDescription("Lists all user subscriptions.")
            .Produces<List<UserSubscriptionModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET /api/core/admin/subscriptions/{userId}
        adminGroup.MapGet("/{userId}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string userId,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminUserSubscriptionQuery { UserId = userId }, cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Admin.Subscriptions.Get")
            .WithDescription("Gets a specific user's subscription.")
            .Produces<UserSubscriptionModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // PUT /api/core/admin/subscriptions/{userId}
        adminGroup.MapPut("/{userId}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string userId,
                   [FromBody] AdminSubscriptionUpdateModel model,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new AdminUserSubscriptionUpdateCommand { UserId = userId, Model = model }, cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Admin.Subscriptions.Update")
            .WithDescription("Updates a user's subscription plan, status, and billing cycle.")
            .Produces<UserSubscriptionModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);
    }
}
