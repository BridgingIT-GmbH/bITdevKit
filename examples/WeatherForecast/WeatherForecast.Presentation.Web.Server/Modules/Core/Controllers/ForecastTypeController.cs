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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[Route("api/core/forecasttypes")]
[ApiController]
public class ForecastTypeController : ControllerBase
{
    private readonly ILogger<ForecastTypeController> logger;
    private readonly IMediator mediator;

    public ForecastTypeController(
        ILogger<ForecastTypeController> logger,
        IMediator mediator)
    {
        EnsureArg.IsNotNull(logger, nameof(logger));
        EnsureArg.IsNotNull(mediator, nameof(mediator));

        this.logger = logger;
        this.mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<IEnumerable<ForecastType>>> GetAll()
    {
        var response = await this.mediator.Send(new ForecastTypeFindAllQuery()).AnyContext();

        return this.Ok(response.Result);
    }
}