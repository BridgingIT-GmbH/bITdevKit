// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

public class CoreTodoItemEndpoints : EndpointsBase
{
    /// <summary>
    /// Maps the endpoints for the TodoItem aggregate to the specified route builder.
    /// </summary>
    /// <param name="app">The IEndpointRouteBuilder instance used to define routes.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/core/todoitems").RequireAuthorization()
            .WithTags("Core.TodoItems");

        app.MapGet("/_schema/probe", () => Results.NoContent())
           .ExcludeFromDescription()
           .Produces<ResultProblemDetails>(StatusCodes.Status200OK, "application/problem+json");

        // GET single TodoItem
        group.MapGet("/{id}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string id, CancellationToken ct)
                   => (await requester
                    .SendAsync(new TodoItemFindOneQuery(id), cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.TodoItems.GetById")
            .WithDescription("Gets a single TodoItem by its unique identifier.")
            .RequireEntityPermission<TodoItem>(Permission.Read)
            .Produces<TodoItemModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // GET all TodoItems
        group.MapGet("",
            async (HttpContext context,
                   [FromServices] IRequester requester,
                   [FromQuery] FilterModel filter, CancellationToken ct)
                   => (await requester
                    .SendAsync(new TodoItemFindAllQuery { Filter = filter }, cancellationToken: ct))
                    .MapHttpOkAll())
            .WithName("CoreModule.TodoItems.GetAll")
            .WithDescription("Gets all items matching the specified filter criteria.")
            .RequireEntityPermission<TodoItem>(Permission.List)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // POST search TodoItems
        group.MapPost("search",
            async (HttpContext context,
                   [FromServices] IRequester requester,
                   [FromBody] FilterModel filter, CancellationToken ct)
                   => (await requester
                    .SendAsync(new TodoItemFindAllQuery { Filter = filter }, cancellationToken: ct))
                    .MapHttpOkAll())
            .WithName("CoreModule.TodoItems.Search")
            .WithDescription("Searches for items matching the specified filter criteria.")
            .RequireEntityPermission<TodoItem>(Permission.List)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // POST new TodoItem
        group.MapPost("",
            async ([FromServices] IRequester requester,
                   [FromBody] TodoItemModel model, CancellationToken ct)
                   => (await requester
                    .SendAsync(new TodoItemCreateCommand { Model = model }, cancellationToken: ct))
                    .MapHttpCreated(value => $"/api/core/assets/{value.Id}"))
            .WithName("Core.TodoItems.Create")
            .WithDescription("Creates a new TodoItem with the specified details.")
            .RequireEntityPermission<TodoItem>(Permission.Write)
            .Produces<TodoItemModel>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/actions/completeall",
            async ([FromServices] IRequester requester,
                   CancellationToken ct)
                   => (await requester
                    .SendAsync(new TodoItemCompleteAllCommand(), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.TodoItems.CompleteAll")
            .WithDescription("Marks all TodoItems as completed.")
            .RequireEntityPermission<TodoItem>(Permission.Write)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // PUT update TodoItem
        group.MapPut("/{id}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string id,
                   [FromBody] TodoItemModel model, CancellationToken ct)
                   => (await requester
                    .SendAsync(new TodoItemUpdateCommand { Model = model }, cancellationToken: ct))
                    .MapHttpOk())
            .WithName("Core.TodoItems.Update")
            .WithDescription("Updates an existing TodoItem with the specified details.")
            .RequireEntityPermission<TodoItem>(Permission.Write)
            .Produces<TodoItemModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status409Conflict)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // DELETE TodoItem
        group.MapDelete("/{id}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string id, CancellationToken ct)
                   => (await requester
                    .SendAsync(new TodoItemDeleteCommand(id), cancellationToken: ct))
                    .MapHttpNoContent())
            .WithName("Core.TodoItems.Delete")
            .WithDescription("Deletes the specified TodoItem.")
            .RequireEntityPermission<TodoItem>(Permission.Delete)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);
    }
}