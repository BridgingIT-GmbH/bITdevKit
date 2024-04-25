// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation.Web.Controllers;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Presentation.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class CoreController : CoreControllerBase
{
    private readonly IMapper mapper;
    private readonly IMediator mediator;

    public CoreController(IMapper mapper, IMediator mediator)
    {
        this.mapper = mapper;
        this.mediator = mediator;
    }

    public override async Task<ActionResult<ResultResponseModel>> EchoGet()
    {
        await Task.Delay(1);
        return new OkObjectResult(Result.Success().WithMessage("echo"));
    }

    // Bill ========================================================================================

    // Guest =======================================================================================

    // Host ========================================================================================
    public override Task<ActionResult<ResultOfHostResponseModel>> HostFindOne(string hostId)
    {
        throw new NotImplementedException();
    }

    public override Task<ActionResult<ResultOfHostsResponseModel>> HostFindAll()
    {
        throw new NotImplementedException();
    }

    public override Task<ActionResult<ResultOfHostResponseModel>> HostCreate([FromBody] HostCreateRequestModel body)
    {
        throw new NotImplementedException();
    }

    // Dinner ======================================================================================
    public override async Task<ActionResult<ResultOfDinnerResponseModel>> DinnerFindOneForHost(string hostId, string dinnerId)
    {
        var query = new DinnerFindOneForHostQuery(hostId, dinnerId);
        var result = (await this.mediator.Send(query)).Result;

        return result.ToOkActionResult<Dinner, ResultOfDinnerResponseModel>(this.mapper);
    }

    public override async Task<ActionResult<ResultOfDinnersResponseModel>> DinnerFindAllForHost(string hostId)
    {
        var query = new DinnerFindAllForHostQuery(hostId);
        var result = (await this.mediator.Send(query)).Result;

        return result.ToOkActionResult<IEnumerable<Dinner>, ResultOfDinnersResponseModel>(this.mapper);
    }

    public override async Task<ActionResult<ResultOfDinnerResponseModel>> DinnerCreate(string hostId, DinnerCreateRequestModel body)
    {
        var command = this.mapper.Map<DinnerCreateRequestModel, DinnerCreateCommand>(body);
        var result = (await this.mediator.Send(command)).Result;

        return result.ToCreatedActionResult<Dinner, ResultOfDinnerResponseModel>(
            this.mapper,
            $"Core_{nameof(this.DinnerFindOneForHost)}", new { hostId, dinnerId = result.Value.Id });
    }

    // Menu ========================================================================================
    public override async Task<ActionResult<ResultOfMenuResponseModel>> MenuFindOneForHost(string hostId, string menuId)
    {
        var query = new MenuFindOneForHostQuery(hostId, menuId);
        var result = (await this.mediator.Send(query)).Result;

        return result.ToOkActionResult<Menu, ResultOfMenuResponseModel>(this.mapper);
    }

    public override async Task<ActionResult<ResultOfMenusResponseModel>> MenuFindAllForHost(string hostId)
    {
        var query = new MenuFindAllForHostQuery(hostId);
        var result = (await this.mediator.Send(query)).Result;

        return result.ToOkActionResult<IEnumerable<Menu>, ResultOfMenusResponseModel>(this.mapper);
    }

    public override async Task<ActionResult<ResultOfMenuResponseModel>> MenuCreate(string hostId, [FromBody] MenuCreateRequestModel body)
    {
        var command = this.mapper.Map<MenuCreateRequestModel, MenuCreateCommand>(body);
        var result = (await this.mediator.Send(command)).Result;

        return result.ToCreatedActionResult<Menu, ResultOfMenuResponseModel>(
            this.mapper,
            $"Core_{nameof(this.MenuFindOneForHost)}", new { hostId, menuId = result.Value.Id });
    }

    // MenuReview ==================================================================================

    // User ========================================================================================
    public override Task<ActionResult<ResultOfUserResponseModel>> UserFindOne(string userId)
    {
        throw new NotImplementedException();
    }

    public override Task<ActionResult<ResultOfUsersResponseModel>> UserFindAll()
    {
        throw new NotImplementedException();
    }

    public override async Task<ActionResult<ResultOfUserResponseModel>> UserCreate([FromBody] UserCreateRequestModel body)
    {
        var command = this.mapper.Map<UserCreateRequestModel, UserCreateCommand>(body);
        var result = (await this.mediator.Send(command)).Result;

        return result.ToCreatedActionResult<User, ResultOfUserResponseModel>(
        this.mapper,
        $"Core_{nameof(this.UserFindOne)}", new { userId = result.Value.Id });
    }
}