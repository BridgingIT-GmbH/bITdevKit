// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Modules.Core.Controllers;

using System.Net;
using Application.Modules.Core;
using Common;
using DevKit.Presentation.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[Route("api/core/cities")]
[ApiController]
public class CityController : ControllerBase
{
    private readonly ILogger<CityController> logger;
    private readonly IMediator mediator;
    private readonly IMapper<CityQueryResponse, CityModel> mapper;

    public CityController(
        ILogger<CityController> logger,
        IMediator mediator,
        IMapper<CityQueryResponse, CityModel> mapper)
    {
        EnsureArg.IsNotNull(logger, nameof(logger));
        EnsureArg.IsNotNull(mediator, nameof(mediator));
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        this.logger = logger;
        this.mediator = mediator;
        this.mapper = mapper;
    }

    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<CityModel>>> GetAll()
    {
        var response = await this.mediator.Send(new CityFindAllQuery()).AnyContext();

        return this.Ok(this.mapper.Map(response.Result));
    }

    [HttpGet]
    [Route("{name}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<CityModel>> GetByName(string name)
    {
        var response = await this.mediator.Send(new CityFindOneQuery(name)).AnyContext();

        return response?.Result is null
            ? this.NotFound()
            : this.Ok(this.mapper.Map(response.Result));
    }

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
        var response = await this.mediator.Send(new CityFindOneQuery(longitude, latitude)).AnyContext();

        return response?.Result is null
            ? this.NotFound()
            : this.Ok(this.mapper.Map(response.Result));
    }

    [HttpPost]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Post(CityModel model)
    {
        if (model is null)
        {
            this.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }

        var response = await this.mediator.Send(new CityCreateCommand(model)).AnyContext();
        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.EntityId);
        return response.Cancelled
            ? this.BadRequest(response.CancelledReason)
            : this.Created($"/api/cities/{response.Result.EntityId}", null);
    }

    [HttpPut]
    [Route("{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Put(string id, CityModel model)
    {
        if (id != model?.Id.ToString())
        {
            this.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }

        var response = await this.mediator.Send(new CityUpdateCommand(model)).AnyContext();
        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.EntityId);
        return response.Cancelled ? this.BadRequest(response.CancelledReason) : this.Ok();
    }

    [HttpDelete]
    [Route("{name}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> DeleteByName(string name)
    {
        var response = await this.mediator.Send(new CityDeleteCommand(name)).AnyContext();

        this.Response.Headers.AddOrUpdate(HttpHeaderKeys.EntityId, response.Result.EntityId);
        return response.Cancelled ? this.BadRequest(response.CancelledReason) : this.Ok();
    }
}