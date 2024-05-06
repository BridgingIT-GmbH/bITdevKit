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
public class CoreController(IMapper mapper, IMediator mediator) : CoreControllerBase
{
    private readonly IMapper mapper = mapper;
    private readonly IMediator mediator = mediator;

    public override async Task<ActionResult<ResultResponseModel>> EchoGet(CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken); // TODO: remove or Task.Run
        return new OkObjectResult(
            Result.Success().WithMessage("echo"));
    }

    // Bill ========================================================================================

    // Guest =======================================================================================

    // Host ========================================================================================
    public override async Task<ActionResult<HostResponseModel>> HostFindOne(string hostId, CancellationToken cancellationToken) =>
        (await this.mediator.Send(new HostFindOneQuery(hostId), cancellationToken)).Result
                .ToOkActionResult<Host, HostResponseModel>(this.mapper);

    public override async Task<ActionResult<ICollection<HostResponseModel>>> HostFindAll(CancellationToken cancellationToken) =>
        (await this.mediator.Send(new HostFindAllQuery(), cancellationToken)).Result
            .ToOkActionResult<Host, HostResponseModel>(this.mapper);

    public override async Task<ActionResult<HostResponseModel>> HostCreate([FromBody] HostCreateRequestModel body, CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(this.mapper.Map<HostCreateRequestModel, HostCreateCommand>(body), cancellationToken).AnyContext()).Result;
        return result.ToCreatedActionResult<Host, HostResponseModel>(this.mapper, $"Core_{nameof(this.HostFindOne)}", new { hostId = result.Value.Id });
    }

    // Dinner ======================================================================================
    public override async Task<ActionResult<DinnerResponseModel>> DinnerFindOneForHost(string hostId, string dinnerId, CancellationToken cancellationToken) =>
        (await this.mediator.Send(new DinnerFindOneForHostQuery(hostId, dinnerId), cancellationToken)).Result
            .ToOkActionResult<Dinner, DinnerResponseModel>(this.mapper);

    public override async Task<ActionResult<ICollection<DinnerResponseModel>>> DinnerFindAllForHost(string hostId, CancellationToken cancellationToken) =>
        (await this.mediator.Send(new DinnerFindAllForHostQuery(hostId), cancellationToken)).Result
            .ToOkActionResult<Dinner, DinnerResponseModel>(this.mapper);

    public override async Task<ActionResult<DinnerResponseModel>> DinnerCreate(string hostId, DinnerCreateRequestModel body, CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(this.mapper.Map<DinnerCreateRequestModel, DinnerCreateCommand>(body), cancellationToken)).Result;
        return result.ToCreatedActionResult<Dinner, DinnerResponseModel>(this.mapper, $"Core_{nameof(this.DinnerFindOneForHost)}", new { hostId, dinnerId = result.Value.Id });
    }

    // Menu ========================================================================================
    public override async Task<ActionResult<MenuResponseModel>> MenuFindOneForHost(string hostId, string menuId, CancellationToken cancellationToken) =>
        (await this.mediator.Send(new MenuFindOneForHostQuery(hostId, menuId), cancellationToken)).Result
            .ToOkActionResult<Menu, MenuResponseModel>(this.mapper);

    public override async Task<ActionResult<ICollection<MenuResponseModel>>> MenuFindAllForHost(string hostId, CancellationToken cancellationToken) =>
        (await this.mediator.Send(new MenuFindAllForHostQuery(hostId), cancellationToken)).Result
            .ToOkActionResult<Menu, MenuResponseModel>(this.mapper);

    public override async Task<ActionResult<MenuResponseModel>> MenuCreate(string hostId, [FromBody] MenuCreateRequestModel body, CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(this.mapper.Map<MenuCreateRequestModel, MenuCreateCommand>(body), cancellationToken)).Result;
        return result.ToCreatedActionResult<Menu, MenuResponseModel>(this.mapper, $"Core_{nameof(this.MenuFindOneForHost)}", new { hostId, menuId = result.Value.Id });
    }

    // MenuReview ==================================================================================

    // User ========================================================================================
    public override Task<ActionResult<UserResponseModel>> UserFindOne(string userId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public override Task<ActionResult<UsersResponseModel>> UserFindAll(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public override async Task<ActionResult<UserResponseModel>> UserCreate([FromBody] UserCreateRequestModel body, CancellationToken cancellationToken) =>
        (await this.mediator.Send(this.mapper.Map<UserCreateRequestModel, UserCreateCommand>(body), cancellationToken)).Result
            .ToCreatedActionResult<User, UserResponseModel>(this.mapper, $"Core_{nameof(this.UserFindOne)}", new { userId = (await this.mediator.Send((UserCreateCommand)this.mapper.Map<UserCreateRequestModel, UserCreateCommand>(body), cancellationToken)).Result.Value.Id });
}