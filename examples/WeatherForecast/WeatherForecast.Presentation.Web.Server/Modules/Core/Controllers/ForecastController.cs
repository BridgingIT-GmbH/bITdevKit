// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Modules.Core.Controllers;

using System.Net;
using Application.Modules.Core;
using Common;
using Domain.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[Route("api/core/forecasts")]
[ApiController]
public class ForecastController : ControllerBase
{
    private readonly ILogger<ForecastController> logger;
    private readonly IMediator mediator;
    private readonly IMapper<ForecastQueryResponse, ForecastModel> mapper;

    public ForecastController(
        ILogger<ForecastController> logger,
        IMediator mediator,
        IMapper<ForecastQueryResponse, ForecastModel> mapper)
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
    public async Task<ActionResult<IEnumerable<City>>> GetAll()
    {
        var response = await this.mediator.Send(new ForecastFindAllQuery()).AnyContext();

        return this.Ok(this.mapper.Map(response.Result));
    }

    [HttpGet]
    [Route("descriptions")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<string>>> GetAllDescriptions()
    {
        var response = await this.mediator.Send(new ForecastFindAllDescriptionsQuery()).AnyContext();

        return this.Ok(response.Result);
    }
}