// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;

public class DinnerCreatedDomainEventHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Menu> repository) // TODO: scoped repo cannot be injected somehow, see log
    : DomainEventHandlerBase<DinnerCreatedDomainEvent>(loggerFactory)
{
    public override bool CanHandle(DinnerCreatedDomainEvent notification)
    {
        return true;
    }

    public override async Task Process(DinnerCreatedDomainEvent @event, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        var dinnerId = DinnerId.Create(@event.DinnerId);
        var hostId = HostId.Create(@event.HostId);
        var menuId = MenuId.Create(@event.MenuId);

        this.Logger.LogInformation($"checking Dinner: {@event.Name} with Menu: {menuId} for Host: {hostId}");

        DomainRules.Apply(
        [
            new MenuForHostMustExistRule(repository, hostId, menuId),
        ]);

        var menu = await repository.FindOneAsync(menuId, cancellationToken: cancellationToken);
        menu.AddDinnerId(dinnerId);

        await repository.UpdateAsync(menu, cancellationToken);
    }
}