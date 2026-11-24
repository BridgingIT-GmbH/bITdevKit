// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Mvc;

/// <summary>Exposes authenticated task-title reference suggestions for task creation.</summary>
/// <example><code>GET /api/core/task-lookups</code></example>
public sealed class CoreTaskLookupEndpoints : EndpointsBase
{
    /// <summary>Maps the task lookup catalog and its controlled cache/source failure responses.</summary>
    /// <param name="app">The endpoint route builder for the application.</param>
    /// <example><code>new CoreTaskLookupEndpoints().Map(app);</code></example>
    public override void Map(IEndpointRouteBuilder app)
    {
        app.MapGroup("api/core/task-lookups")
            .RequireAuthorization()
            .WithTags("Core.TaskLookups")
            .MapGet("", FindAll)
            .WithName("Core.TaskLookups.GetAll")
            .Produces<IReadOnlyList<TaskLookupModel>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status429TooManyRequests)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError)
            .ProducesResultProblem(StatusCodes.Status502BadGateway)
            .ProducesResultProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesResultProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<Microsoft.AspNetCore.Http.IResult> FindAll(HttpContext context,
        [FromServices] IRequester requester, CancellationToken cancellationToken = default)
    {
        context.Response.Headers.CacheControl = "no-store";
        var result = await requester.SendAsync(new TaskLookupsQuery(), cancellationToken: cancellationToken);
        return result.MapHttpOkAll();
    }
}