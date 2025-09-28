﻿//// MIT-License
//// Copyright BridgingIT GmbH - All Rights Reserved
//// Use of this source code is governed by an MIT-style license that can be
//// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

//namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core.Controllers;

//using System.Net;
//using Application.Modules.Core;
//using BridgingIT.DevKit.Application.Identity;
//using BridgingIT.DevKit.Application.Storage;
//using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
//using BridgingIT.DevKit.Presentation;
//using Common;
//using DevKit.Presentation.Web;
//using MediatR;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;

//[Authorize]
//[Route("api/core/todoitems")]
//[ApiController]
//public class TodoItemController( // TODO: move to minimal endpoints
//    IMediator mediator,
//    IAuthorizationService authorizationService,
//    IFileMonitoringService fileMonitoringService,
//    ILogger<TodoItemController> logger) : ControllerBase
//{
//    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.Read))]
//    [HttpGet]
//    [Route("{id}")]
//    [ProducesResponseType((int)HttpStatusCode.OK)]
//    [ProducesResponseType((int)HttpStatusCode.NotFound)]
//    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
//    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
//    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
//    public async Task<ActionResult<TodoItemModel>> GetById(string id)
//    {
//        var authResult = await authorizationService.AuthorizeAsync(
//            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.Read));
//        if (!authResult.Succeeded)
//        {
//            return this.Unauthorized("read permission needed");
//        }

//        var response = await mediator.Send(
//            new TodoItemFindOneQuery(id)).AnyContext();

//        return response.Result.ToOkActionResult();
//    }

//    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.List))]
//    [HttpGet]
//    [ProducesResponseType((int)HttpStatusCode.OK)]
//    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
//    public async Task<ActionResult<ICollection<TodoItemModel>>> GetAll([FromQueryFilter] FilterModel filter)
//    {
//        var authResult = await authorizationService.AuthorizeAsync(
//            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.List));
//        if (!authResult.Succeeded)
//        {
//            return this.Unauthorized("list permission needed");
//        }

//        // Example filter model:
//        // {
//        //     "page": 1,
//        //     "pageSize": 10,
//        //     "filters": [
//        //       { "field": "Name", "operator": "eq", "value": "Berlin" }
//        //     ]
//        // }

//        var response = await mediator.Send(
//            new TodoItemFindAllQuery { Filter = filter }).AnyContext();

//        return response.Result.ToOkActionResult();
//    }

//    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.List))]
//    [HttpPost("search")]
//    [ProducesResponseType((int)HttpStatusCode.OK)]
//    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
//    public async Task<ActionResult<ICollection<TodoItemModel>>> PostSearch([FromBodyFilter] FilterModel filter)
//    {
//        var scanContext = await fileMonitoringService.ScanLocationAsync("inbound");
//        foreach (var @event in scanContext.Events.SafeNull())
//        {
//            logger.LogInformation($"@@@@@@@@@ {@event.FilePath} [{@event.EventType}]");
//        }

//        var authResult = await authorizationService.AuthorizeAsync(
//            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.List));
//        if (!authResult.Succeeded)
//        {
//            return this.Unauthorized("list permission needed");
//        }

//        // Example filter model:
//        // {
//        //     "page": 1,
//        //     "pageSize": 10,
//        //     "filters": [
//        //       { "field": "Name", "operator": "eq", "value": "Berlin" }
//        //     ]
//        // }

//        var response = await mediator.Send(
//            new TodoItemFindAllQuery { Filter = filter }).AnyContext();

//        return response.Result.ToOkActionResult();
//    }

//    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.Write))]
//    [HttpPost]
//    [ProducesResponseType((int)HttpStatusCode.OK)]
//    [ProducesResponseType((int)HttpStatusCode.Created)]
//    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
//    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
//    public async Task<ActionResult<TodoItemModel>> Post(TodoItemModel model)
//    {
//        var authResult = await authorizationService.AuthorizeAsync(
//            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.Write));
//        if (!authResult.Succeeded)
//        {
//            return this.Unauthorized("write permission needed");
//        }

//        var response = await mediator.Send(
//            new TodoItemCreateCommand { Model = model }).AnyContext();
//        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.IsSuccess ? response.Result.Value.Id : null);

//        return response.Result.ToCreatedActionResult();
//    }

//    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.Write))]
//    [HttpPut]
//    [Route("{id}")]
//    [ProducesResponseType((int)HttpStatusCode.OK)]
//    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
//    [ProducesResponseType((int)HttpStatusCode.NotFound)]
//    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
//    public async Task<ActionResult<TodoItemModel>> Put(string id, TodoItemModel model)
//    {
//        var authResult = await authorizationService.AuthorizeAsync(
//            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.Write));
//        if (!authResult.Succeeded)
//        {
//            return this.Unauthorized();
//        }

//        var response = await mediator.Send(
//            new TodoItemUpdateCommand{ Model = model }).AnyContext();

//        return response.Result.ToUpdatedActionResult();
//    }

//    //[EntityPermissionRequirement(typeof(TodoItem), nameof(Permission.Delete))]
//    [HttpDelete]
//    [Route("{id}")]
//    [ProducesResponseType((int)HttpStatusCode.OK)]
//    [ProducesResponseType((int)HttpStatusCode.NotFound)]
//    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
//    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
//    public async Task<ActionResult> DeleteByName(string id)
//    {
//        var authResult = await authorizationService.AuthorizeAsync(
//            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.Delete));
//        if (!authResult.Succeeded)
//        {
//            return this.Unauthorized();
//        }

//        var response = await mediator.Send(
//            new TodoItemDeleteCommand(id)).AnyContext();

//        return response.Result.ToOkActionResult();
//    }

//    [HttpPost("actions/completeall")]
//    [ProducesResponseType((int)HttpStatusCode.OK)]
//    [ProducesResponseType((int)HttpStatusCode.Created)]
//    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
//    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
//    public async Task<ActionResult> CompleteAll()
//    {
//        var authResult = await authorizationService.AuthorizeAsync(
//            this.User, typeof(TodoItem), new EntityPermissionRequirement(Permission.Write));
//        if (!authResult.Succeeded)
//        {
//            return this.Unauthorized("write permission needed");
//        }

//        var response = await mediator.Send(
//            new TodoItemCompleteAllCommand()).AnyContext();

//        return response.Result.ToOkActionResult();
//    }

//    [HttpGet("scan")]
//    [AllowAnonymous]
//    [ProducesResponseType((int)HttpStatusCode.OK)]
//    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
//    public async Task<ActionResult> Scan([FromBodyFilter] FilterModel filter)
//    {
//        var scanContext = await fileMonitoringService.ScanLocationAsync("inbound");
//        foreach (var @event in scanContext.Events.SafeNull())
//        {
//            logger.LogInformation($"@@@@@@@@@ {@event.FilePath} [{@event.EventType}]");
//        }

//        return this.Ok(scanContext);
//    }
//}