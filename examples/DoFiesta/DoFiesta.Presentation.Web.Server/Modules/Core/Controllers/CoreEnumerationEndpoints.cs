// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core.Controllers;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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

        // GET single TodoItem
        group.MapGet("/{id:guid}", TodoItemFindOne)
            .WithName("Core.Enumerations.GetAll")
            .WithFilterSchema()
            .Produces<TodoItem>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static Results<Ok<EnumerationModel>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> TodoItemFindOne(
        HttpContext context,
        [FromServices] IMediator mediator,
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        return Result<EnumerationModel>.Success(
            new EnumerationModel()).MapHttpOk();
    }
}