// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>
/// Endpoints for the weather dashboard aggregating cities, alerts, and recommendations.
/// </summary>
public class DashboardEndpoints : EndpointsBase
{
    /// <summary>
    /// Maps the dashboard endpoints to the specified route builder.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used to define routes.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/core/dashboard")
            .RequireAuthorization()
            .WithTags("Core.Dashboard");

        // GET /api/core/dashboard
        group.MapGet("/",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                => (await requester
                    .SendAsync(new DashboardQuery(), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.Dashboard.Get")
            .WithDescription("Gets the full weather dashboard with cities, highlights, alerts, and recommendations.")
            .Produces<DashboardModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);
    }
}
