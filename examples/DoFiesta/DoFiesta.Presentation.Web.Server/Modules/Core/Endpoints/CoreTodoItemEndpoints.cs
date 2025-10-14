// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
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

        // GET single TodoItem
        group.MapGet("/{id:guid}", TodoItemFindOne)
            //.RequireEntityPermission<TodoItem>(Permission.Read)
            .WithName("Core.TodoItems.GetById")
            .Produces<TodoItemModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET all TodoItems
        group.MapGet("", TodoItemFindAll)
            //.RequireEntityPermission<TodoItem>(Permission.List)
            .WithName("Core.TodoItems.GetAll")
            .WithFilterSchema()
            .Produces<IEnumerable<TodoItemModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET sarch TodoItems
        group.MapPost("search", TodoItemSearch)
            //.RequireEntityPermission<TodoItem>(Permission.List)
            .WithName("Core.TodoItems.Search")
            .WithFilterSchema(true)
            .Produces<IEnumerable<TodoItemModel>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // POST new TodoItem
        group.MapPost("", TodoItemCreate)
            //.RequireEntityPermission<TodoItem>(Permission.Write)
            .WithName("Core.TodoItems.Create")
            .Produces<TodoItemModel>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/actions/completeall", TodoCompleteAll)
            //.RequireEntityPermission<TodoItem>(Permission.Write)
            .WithName("Core.TodoItems.CompleteAll")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // PUT update TodoItem
        group.MapPut("/{id:guid}", TodoItemUpdate)
            //.RequireEntityPermission<TodoItem>(Permission.Write)
            .WithName("Core.TodoItems.Update")
            .Produces<TodoItemModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // DELETE TodoItem
        group.MapDelete("/{id:guid}", TodoItemDelete)
            //.RequireEntityPermission<TodoItem>(Permission.Delete)
            .WithName("Core.TodoItems.Delete")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<TodoItemModel>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>> TodoItemFindOne(
        [FromServices] IRequester requester,
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        return (await requester.SendAsync(
            new TodoItemFindOneQuery(id), cancellationToken: cancellationToken))
            .MapHttpOk();
    }

    private static async Task<Results<Ok<IEnumerable<TodoItemModel>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>> TodoItemFindAll(
        HttpContext context,
        [FromServices] IRequester requester,
        CancellationToken cancellationToken = default)
    {
        return (await requester.SendAsync(
            new TodoItemFindAllQuery { Filter = await context.FromQueryFilterAsync() }, cancellationToken: cancellationToken))
            .MapHttpOkAll();
    }

    private static async Task<Results<Ok<IEnumerable<TodoItemModel>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>> TodoItemSearch(
        HttpContext context,
        [FromServices] IRequester requester,
        CancellationToken cancellationToken = default)
    {
        return (await requester.SendAsync(
            new TodoItemFindAllQuery { Filter = await context.FromBodyFilterAsync() }, cancellationToken: cancellationToken))
            .MapHttpOkAll();
    }

    private static async Task<Results<Created<TodoItemModel>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>> TodoItemCreate(
        [FromServices] IRequester requester,
        [FromBody] TodoItemModel model,
        CancellationToken cancellationToken = default)
    {
        return (await requester.SendAsync(
            new TodoItemCreateCommand { Model = model }, cancellationToken: cancellationToken))
            .MapHttpCreated(value => $"/api/core/assets/{value.Id}");
    }

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>> TodoCompleteAll(
        [FromServices] IRequester requester,
        CancellationToken cancellationToken = default)
    {
        return (await requester.SendAsync(
            new TodoItemCompleteAllCommand(), cancellationToken: cancellationToken))
            .MapHttpNoContent();
    }

    private static async Task<Results<Ok<TodoItemModel>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>> TodoItemUpdate(
        [FromServices] IRequester requester,
        [FromRoute] string id,
        [FromBody] TodoItemModel model,
        CancellationToken cancellationToken = default)
    {
        return (await requester.SendAsync(
            new TodoItemUpdateCommand { Model = model }, cancellationToken: cancellationToken))
            .MapHttpOk();
    }

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>> TodoItemDelete(
        [FromServices] IRequester requester,
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        return (await requester.SendAsync(
            new TodoItemDeleteCommand(id), cancellationToken: cancellationToken))
            .MapHttpNoContent();
    }
}