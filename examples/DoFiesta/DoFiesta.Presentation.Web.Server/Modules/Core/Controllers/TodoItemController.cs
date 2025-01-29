// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core.Controllers;

using System.Net;
using Application.Modules.Core;
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using BridgingIT.DevKit.Presentation;
using Common;
using DevKit.Presentation.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[Route("api/core/todoitems")]
[ApiController]
public class TodoItemController( // TODO: move to minimal endpoints
    //ILogger<CityController> logger,
    IMediator mediator,
    IAuthorizationService authorizationService) : ControllerBase
{
    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.List))]
    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetAll([FromQueryFilter] FilterModel filter)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.List));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        // Example filter model:
        // {
        //     "page": 1,
        //     "pageSize": 10,
        //     "filters": [
        //       { "field": "Name", "operator": "eq", "value": "Berlin" }
        //     ]
        // }

        var response = await mediator.Send(
            new TodoItemFindAllQuery(filter)).AnyContext();

        // auth check for specific todoitem
        authResult = await authorizationService.AuthorizeAsync(
            this.User, response.Result.Value.First(), new EntityPermissionRequirement(Permission.List));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        return this.Ok(response.Result.Value);
    }

    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.List))]
    [HttpPost("search")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<TodoItem>>> PostSearch([FromBodyFilter] FilterModel filter)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.List));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        // Example filter model:
        // {
        //     "page": 1,
        //     "pageSize": 10,
        //     "filters": [
        //       { "field": "Name", "operator": "eq", "value": "Berlin" }
        //     ]
        // }

        var response = await mediator.Send(
            new TodoItemFindAllQuery(filter)).AnyContext();

        // auth check for specific todoitem
        authResult = await authorizationService.AuthorizeAsync(
            this.User, response.Result.Value.First(), new EntityPermissionRequirement(Permission.List));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        return this.Ok(response.Result.Value);
    }

    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.Read))]
    [HttpGet]
    [Route("{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<TodoItem>> GetById(string id)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.Read));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        var response = await mediator.Send(new TodoItemFindOneQuery(id)).AnyContext();

        return response?.Result is null
            ? this.NotFound()
            : this.Ok(response.Result.Value);
    }

    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.Write))]
    [HttpPost]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Post(TodoItem entity)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.Write));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized("write permission needed");
        }

        if (entity is null)
        {
            this.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }

        var response = await mediator.Send(new TodoItemCreateCommand(entity)).AnyContext();
        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.EntityId);

        return response.Cancelled
            ? this.BadRequest(response.CancelledReason)
            : this.Created($"/api/todoitems/{response.Result.EntityId}", null);
    }

    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.Write))]
    [HttpPut]
    [Route("{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Put(string id, TodoItem entity)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.Write));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        if (id != entity?.Id.ToString())
        {
            this.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }

        var response = await mediator.Send(new TodoItemUpdateCommand(entity)).AnyContext();
        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.EntityId);

        return response.Cancelled ? this.BadRequest(response.CancelledReason) : this.Ok();
    }

    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.Delete))]
    [HttpDelete]
    [Route("{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> DeleteByName(string id)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.Delete));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        var response = await mediator.Send(new TodoItemDeleteCommand(id)).AnyContext();

        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.EntityId);

        return response.Cancelled ? this.BadRequest(response.CancelledReason) : this.Ok();
    }
}