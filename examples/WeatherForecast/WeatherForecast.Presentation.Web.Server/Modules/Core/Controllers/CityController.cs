// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Modules.Core.Controllers;

using System.Net;
using Application.Modules.Core;
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using BridgingIT.DevKit.Presentation;
using Common;
using DevKit.Presentation.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[Route("api/core/cities")]
[ApiController]
public class CityController(
    //ILogger<CityController> logger,
    IMediator mediator,
    IMapper<CityQueryResponse, CityModel> mapper,
    IAuthorizationService authorizationService) : ControllerBase
{
    //[EntityPermissionRequirement(typeof(City), nameof(Permission.List))]
    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<CityModel>>> GetAll([FromQueryFilter] FilterModel filter)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(City), new EntityPermissionRequirement(Permission.List));
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
            new CityFindAllQuery(filter)).AnyContext();

        // auth check for specific city
        authResult = await authorizationService.AuthorizeAsync(
            this.User, City.Create(response.Result.First().City.Name, response.Result.First().City.Country, response.Result.First().City.Location.Longitude, response.Result.First().City.Location.Latitude), new EntityPermissionRequirement(Permission.List));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        return this.Ok(mapper.Map(response.Result));
    }

    //[EntityPermissionRequirement(typeof(City), nameof(Permission.List))]
    [HttpPost("search")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<CityModel>>> PostSearch([FromBodyFilter] FilterModel filter)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(City), new EntityPermissionRequirement(Permission.List));
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
            new CityFindAllQuery(filter)).AnyContext();

        return this.Ok(mapper.Map(response.Result));
    }

    //[EntityPermissionRequirement(typeof(City), nameof(Permission.Read))]
    [HttpGet]
    [Route("{name}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<CityModel>> GetByName(string name)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(City), new EntityPermissionRequirement(Permission.Read));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        var response = await mediator.Send(new CityFindOneQuery(name)).AnyContext();

        return response?.Result is null
            ? this.NotFound()
            : this.Ok(mapper.Map(response.Result));
    }

    //[EntityPermissionRequirement(typeof(City), nameof(Permission.Read))]
    [HttpGet]
    [Route("location")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<CityModel>> GetByLocation(
        [FromQuery] double? longitude,
        [FromQuery] double? latitude)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(City), new EntityPermissionRequirement(Permission.Read));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        var response = await mediator.Send(new CityFindOneQuery(longitude, latitude)).AnyContext();

        return response?.Result is null
            ? this.NotFound()
            : this.Ok(mapper.Map(response.Result));
    }

    //[EntityPermissionRequirement(typeof(City), nameof(Permission.Write))]
    [HttpPost]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Post(CityModel model)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(City), new EntityPermissionRequirement(Permission.Write));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized("write permission needed");
        }

        if (model is null)
        {
            this.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }

        var response = await mediator.Send(new CityCreateCommand(model)).AnyContext();
        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.EntityId);

        return response.Cancelled
            ? this.BadRequest(response.CancelledReason)
            : this.Created($"/api/cities/{response.Result.EntityId}", null);
    }

    //[EntityPermissionRequirement(typeof(City), nameof(Permission.Write))]
    [HttpPut]
    [Route("{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Put(string id, CityModel model)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(City), new EntityPermissionRequirement(Permission.Write));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        if (id != model?.Id.ToString())
        {
            this.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }

        var response = await mediator.Send(new CityUpdateCommand(model)).AnyContext();
        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.EntityId);

        return response.Cancelled ? this.BadRequest(response.CancelledReason) : this.Ok();
    }

    //[EntityPermissionRequirement(typeof(City), nameof(Permission.Delete))]
    [HttpDelete]
    [Route("{name}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> DeleteByName(string name)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(City), new EntityPermissionRequirement(Permission.Delete));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        var response = await mediator.Send(new CityDeleteCommand(name)).AnyContext();

        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.EntityId);

        return response.Cancelled ? this.BadRequest(response.CancelledReason) : this.Ok();
    }
}