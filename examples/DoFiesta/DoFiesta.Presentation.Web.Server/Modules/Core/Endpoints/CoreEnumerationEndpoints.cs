// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using System.Text;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Mvc;

/// <summary>Exposes the authenticated shared reference catalog through ordinary application requests.</summary>
/// <example><code>GET /api/core/enumerations</code></example>
public class CoreEnumerationEndpoints : EndpointsBase
{
    /// <summary>
    ///     Maps the authenticated shared enumeration catalog and its controlled failure responses.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used to define routes.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/core/enumerations").RequireAuthorization()
            .WithTags("Core.Enumerations");

        // GET all Enumerations
        group.MapGet("", EnumerationsFindAll)
            .WithName("Core.Enumerations.GetAll")
            .Produces<EnumerationModel>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status429TooManyRequests)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError)
            .ProducesResultProblem(StatusCodes.Status502BadGateway)
            .ProducesResultProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesResultProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<Microsoft.AspNetCore.Http.IResult> EnumerationsFindAll(
        HttpContext context,
        [FromServices] IRequester requester,
        CancellationToken cancellationToken = default)
    {
        context.Response.Headers.CacheControl = "no-store";
        var result = await requester.SendAsync(new EnumerationCatalogQuery(), cancellationToken: cancellationToken);
        if (result.IsFailure) { return result.MapHttpNoContent(); }

        // The cached value is complete JSON, not a string to be JSON-quoted a second time.
        return Results.Content(result.Value, "application/json", Encoding.UTF8);
    }
}