// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Modules.Core.Controllers;

using System.Net;
using Application.Modules.Core;
using Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[Route("api/core/countries")]
[ApiController]
public class CountryController : ControllerBase
{
    private readonly ILogger<CountryController> logger;
    private readonly IMediator mediator;

    public CountryController(
        ILogger<CountryController> logger,
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
    public async Task<ActionResult<IEnumerable<string>>> GetAll()
    {
        var response = await this.mediator.Send(new CountryFindAllQuery()).AnyContext();

        return this.Ok(response.Result);
    }
}