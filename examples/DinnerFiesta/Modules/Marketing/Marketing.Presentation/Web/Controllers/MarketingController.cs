// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Presentation.Web.Controllers;

using Application;
using Common;
using DevKit.Presentation.Web;
using Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class MarketingController(IMapper mapper, IMediator mediator) : MarketingControllerBase
{
    private readonly IMapper mapper = mapper;
    private readonly IMediator mediator = mediator;

    public override async Task<ActionResult<ResultResponseModel>> EchoGet(CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        return new OkObjectResult(Result.Success().WithMessage("echo"));
    }

    // Customer =====================================================================================
    public override async Task<ActionResult<ResultOfCustomerResponseModel>> CustomerFindOne(
        string customerId,
        CancellationToken cancellationToken)
    {
        var query = new CustomerFindOneQuery(customerId);
        var result = (await this.mediator.Send(query, cancellationToken)).Result;

        return result.ToOkActionResult<Customer, ResultOfCustomerResponseModel>(this.mapper);
    }

    public override async Task<ActionResult<ResultOfCustomersResponseModel>> CustomerFindAll(
        CancellationToken cancellationToken)
    {
        var query = new CustomerFindAllQuery();
        var result = (await this.mediator.Send(query, cancellationToken).AnyContext()).Result;

        return result.ToOkActionResult<IEnumerable<Customer>, ResultOfCustomersResponseModel>(this.mapper);
    }

    public override async Task<ActionResult<ResultResponseModel>> CustomerEmailUnsubscribe(
        string customerId,
        CancellationToken cancellationToken)
    {
        var command = new CustomerUnsubscribeCommand { CustomerId = customerId };
        var result = (await this.mediator.Send(command, cancellationToken).AnyContext()).Result;

        return result.ToOkActionResult<ResultResponseModel>(this.mapper);
    }
}