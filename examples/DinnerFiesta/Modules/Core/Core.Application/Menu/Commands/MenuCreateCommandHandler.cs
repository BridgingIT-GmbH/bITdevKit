// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;

public class MenuCreateCommandHandler : CommandHandlerBase<MenuCreateCommand, Result<Menu>>
{
    private readonly IGenericRepository<Menu> repository;

    public MenuCreateCommandHandler(ILoggerFactory loggerFactory, IGenericRepository<Menu> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public override async Task<CommandResponse<Result<Menu>>> Process(MenuCreateCommand command, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        var menu = Menu.Create(
            HostId.Create(command.HostId),
            command.Name,
            command.Description,
            command.Sections?.Select(section => MenuSection.Create(
                section.Name,
                section.Description,
                section.Items?.Select(item => MenuSectionItem.Create(
                    item.Name,
                    item.Description)))));

        await this.repository.InsertAsync(menu, cancellationToken);

        return CommandResponse.Success(menu);
    }
}