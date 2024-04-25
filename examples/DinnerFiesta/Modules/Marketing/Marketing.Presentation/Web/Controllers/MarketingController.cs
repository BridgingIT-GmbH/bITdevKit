// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Presentation.Web.Controllers;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;
using BridgingIT.DevKit.Presentation.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class MarketingController : MarketingControllerBase
{
    private readonly IMapper mapper;
    private readonly IMediator mediator;

    public MarketingController(IMapper mapper, IMediator mediator)
    {
        this.mapper = mapper;
        this.mediator = mediator;
    }

    public override async Task<ActionResult<ResultResponseModel>> EchoGet()
    {
        await Task.Delay(1);
        return new OkObjectResult(Result.Success().WithMessage("echo"));
    }

    // Customer =====================================================================================
    public override async Task<ActionResult<ResultOfCustomerResponseModel>> CustomerFindOne(string customerId)
    {
        var query = new CustomerFindOneQuery(customerId);
        var result = (await this.mediator.Send(query)).Result;

        return result.ToOkActionResult<Customer, ResultOfCustomerResponseModel>(this.mapper);
    }

    public override async Task<ActionResult<ResultOfCustomersResponseModel>> CustomerFindAll()
    {
        var query = new CustomerFindAllQuery();
        var result = (await this.mediator.Send(query).AnyContext()).Result;

        return result.ToOkActionResult<IEnumerable<Customer>, ResultOfCustomersResponseModel>(this.mapper);
    }

    public override async Task<ActionResult<ResultResponseModel>> CustomerEmailUnsubscribe(string customerId)
    {
        var command = new CustomerUnsubscribeCommand { CustomerId = customerId };
        var result = (await this.mediator.Send(command).AnyContext()).Result;

        return result.ToOkActionResult<ResultResponseModel>(this.mapper);
    }
}