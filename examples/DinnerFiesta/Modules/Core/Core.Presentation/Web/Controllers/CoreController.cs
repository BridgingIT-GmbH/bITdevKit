// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation.Web.Controllers;

using Application;
using Common;
using DevKit.Presentation.Web;
using Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class CoreController(IMapper mapper, IMediator mediator) : CoreControllerBase
{
    private readonly IMapper mapper = mapper;
    private readonly IMediator mediator = mediator;

    public override async Task<ActionResult<ResultModel>> EchoGet(CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken); // TODO: remove or Task.Run

        return new OkObjectResult(Result.Success().WithMessage("echo"));
    }

    // Bill ========================================================================================

    // Guest =======================================================================================

    // Host ========================================================================================
    public override async Task<ActionResult<HostModel>> HostFindOne(string hostId, CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(new HostFindOneQuery(hostId), cancellationToken)).Result;

        return result.ToOkActionResult<Host, HostModel>(this.mapper);
    }

    public override async Task<ActionResult<ICollection<HostModel>>> HostFindAll(CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(new HostFindAllQuery(), cancellationToken)).Result;

        return result.ToOkActionResult<Host, HostModel>(this.mapper);
    }

    public override async Task<ActionResult<HostModel>> HostCreate(
        [FromBody] HostModel body,
        CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(this.mapper.Map<HostModel, HostCreateCommand>(body), cancellationToken))
            .Result;

        return result.ToCreatedActionResult<Host, HostModel>(this.mapper,
            $"Core_{nameof(this.HostFindOne)}",
            new { hostId = result.Value?.Id });
    }

    public override async Task<ActionResult<HostModel>> HostUpdate(
        [FromBody] HostModel body,
        CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(this.mapper.Map<HostModel, HostUpdateCommand>(body), cancellationToken))
            .Result;

        return result.ToUpdatedActionResult<Host, HostModel>(this.mapper,
            $"Core_{nameof(this.HostFindOne)}",
            new { hostId = result.Value?.Id });
    }

    // Dinner ======================================================================================
    public override async Task<ActionResult<DinnerModel>> DinnerFindOneForHost(
        string hostId,
        string dinnerId,
        CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(new DinnerFindOneForHostQuery(hostId, dinnerId), cancellationToken))
            .Result;

        return result.ToOkActionResult<Dinner, DinnerModel>(this.mapper);
    }

    public override async Task<ActionResult<ICollection<DinnerModel>>> DinnerFindAllForHost(
        string hostId,
        CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(new DinnerFindAllForHostQuery(hostId), cancellationToken)).Result;

        return result.ToOkActionResult<Dinner, DinnerModel>(this.mapper);
    }

    public override async Task<ActionResult<DinnerModel>> DinnerCreate(
        string hostId,
        DinnerModel body,
        CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(
            this.mapper.Map<DinnerModel, DinnerCreateCommand>(body),
            cancellationToken)).Result;

        return result.ToCreatedActionResult<Dinner, DinnerModel>(this.mapper,
            $"Core_{nameof(this.DinnerFindOneForHost)}",
            new { hostId, dinnerId = result.Value?.Id });
    }

    // Menu ========================================================================================
    public override async Task<ActionResult<MenuModel>> MenuFindOneForHost(
        string hostId,
        string menuId,
        CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(new MenuFindOneForHostQuery(hostId, menuId), cancellationToken)).Result;

        return result.ToOkActionResult<Menu, MenuModel>(this.mapper);
    }

    public override async Task<ActionResult<ICollection<MenuModel>>> MenuFindAllForHost(
        string hostId,
        CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(new MenuFindAllForHostQuery(hostId), cancellationToken)).Result;

        return result.ToOkActionResult<Menu, MenuModel>(this.mapper);
    }

    public override async Task<ActionResult<MenuModel>> MenuCreate(
        string hostId,
        [FromBody] MenuModel body,
        CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(this.mapper.Map<MenuModel, MenuCreateCommand>(body), cancellationToken))
            .Result;

        return result.ToCreatedActionResult<Menu, MenuModel>(this.mapper,
            $"Core_{nameof(this.MenuFindOneForHost)}",
            new { hostId, menuId = result.Value?.Id });
    }

    // MenuReview ==================================================================================

    // User ========================================================================================
    public override Task<ActionResult<UserModel>> UserFindOne(string userId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task<ActionResult<ICollection<UserModel>>> UserFindAll(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override async Task<ActionResult<UserModel>> UserCreate(
        [FromBody] UserModel body,
        CancellationToken cancellationToken)
    {
        var result = (await this.mediator.Send(this.mapper.Map<UserModel, UserCreateCommand>(body), cancellationToken))
            .Result;

        return result.ToCreatedActionResult<User, UserModel>(this.mapper,
            $"Core_{nameof(this.UserFindOne)}",
            new
            {
                userId = (await this.mediator.Send(this.mapper.Map<UserModel, UserCreateCommand>(body),
                    cancellationToken)).Result.Value.Id
            });
    }
}