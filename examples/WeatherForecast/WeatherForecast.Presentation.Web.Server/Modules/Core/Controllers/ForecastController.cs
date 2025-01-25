// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Modules.Core.Controllers;

using System.Net;
using Application.Modules.Core;
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Presentation;
using Common;
using Domain.Model;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/core/forecasts")]
[ApiController]
public class ForecastController(
    //ILogger<ForecastController> logger,
    IMediator mediator,
    IMapper<ForecastQueryResponse, ForecastModel> mapper,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<ForecastModel>>> GetAll([FromQueryFilter] FilterModel filter)
    {
        // Example filter model:
        // {
        //     "page": 0,
        //     "pageSize": 0,
        //     "filters": [
        //       { "field": "type.name", "operator": "isnotnull" },
        //       { "field": "type.name", "operator": "eq", "value": "AAA" },
        //       { "field": "temperatureMin", "operator": "gte", "value": 16.1 },
        //       { "field": "timestamp", "operator": "gte", "value": "2024-10-24T10:00:00+00:00" }
        //     ]
        // }

        var response = await mediator.Send(
            new ForecastFindAllQuery(filter)).AnyContext();

        return this.Ok(mapper.Map(response.Result));
    }

    [Authorize]
    //[EntityPermissionRequirement(typeof(Forecast), nameof(Permission.Read))]
    [HttpGet("auth")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<ForecastModel>>> GetAllAuth([FromQueryFilter] FilterModel filter)
    {
        var authResult = await authorizationService.AuthorizeAsync(
            this.User, typeof(Forecast), new EntityPermissionRequirement(Permission.Read));
        if (!authResult.Succeeded)
        {
            return this.Unauthorized();
        }

        // Example filter model:
        // {
        //     "page": 0,
        //     "pageSize": 0,
        //     "filters": [
        //       { "field": "type.name", "operator": "isnotnull" },
        //       { "field": "type.name", "operator": "eq", "value": "AAA" },
        //       { "field": "temperatureMin", "operator": "gte", "value": 16.1 },
        //       { "field": "timestamp", "operator": "gte", "value": "2024-10-24T10:00:00+00:00" }
        //     ]
        // }

        var response = await mediator.Send(
            new ForecastFindAllQuery(filter)).AnyContext();

        return this.Ok(mapper.Map(response.Result));
    }

    [HttpPost("search")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<ForecastModel>>> PostSearch([FromBodyFilter] FilterModel filter)
    {
        // Example filter model:
        // {
        //     "page": 1,
        //     "pageSize": 5,
        //     "filters": [
        //       { "field": "type.name", "operator": "isnotnull" },
        //       { "field": "type.name", "operator": "eq", "value": "AAA" },
        //       { "field": "temperatureMin", "operator": "gte", "value": 16.1 },
        //       { "field": "timestamp", "operator": "gte", "value": "2024-10-24T10:00:00+00:00" }
        //     ]
        // }

        var response = await mediator.Send(
            new ForecastFindAllQuery(filter)).AnyContext();

        return this.Ok(mapper.Map(response.Result));
    }

    [HttpPost("paged")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<ResultPaged<Forecast>>> PostPaged([FromBodyFilter] FilterModel filter)
    {
        // Example filter model:
        // {
        //     "page": 1,
        //     "pageSize": 5,
        //     "filters": [
        //     {
        //         "field": "temperatureMin",
        //         "operator": "gte",
        //         "value": 13.1
        //     }
        //     ],
        //     "orderings": [
        //     {
        //         "field": "temperatureMin",
        //         "direction": "asc"
        //     }
        //     ]
        // }

        var response = await mediator.Send(
            new ForecastFindAllPagedQuery(filter)).AnyContext();

        return this.Ok(response.Result);
    }

    [HttpGet]
    [Route("descriptions")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<string>>> GetAllDescriptions()
    {
        var response = await mediator.Send(new ForecastFindAllDescriptionsQuery()).AnyContext();

        return this.Ok(response.Result);
    }
}