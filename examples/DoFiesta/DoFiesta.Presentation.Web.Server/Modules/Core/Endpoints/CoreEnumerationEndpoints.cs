// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http.HttpResults;

public class CoreEnumerationEndpoints : EndpointsBase
{
    /// <summary>
    ///     Maps the endpoints for the Enumerationsto the specified route builder.
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
            .Produces<EnumerationModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static Results<Ok<EnumerationModel>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> EnumerationsFindAll(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        return Result<EnumerationModel>.Success(
            new EnumerationModel()).MapHttpOk();
    }
}