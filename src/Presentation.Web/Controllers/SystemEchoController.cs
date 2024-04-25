// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

[ApiController]
[Route("/api/_system/echo")]
public class SystemEchoController : ControllerBase
{
    private readonly IMediator mediator;

    public SystemEchoController(IMediator mediator)
    {
        EnsureArg.IsNotNull(mediator, nameof(mediator));

        this.mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    [OpenApiTag("_system/echo")]
    public async Task<string> Get()
    {
        var response = await this.mediator.Send(new EchoQuery()).AnyContext();
        return response.Result;
    }
}