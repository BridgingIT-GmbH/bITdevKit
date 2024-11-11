// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

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

        await Rule.Add(new MenuForHostMustExistRule(repository, hostId, menuId))
            .ThrowOnFailure().CheckAsync(cancellationToken: cancellationToken);

        var menu = await repository.FindOneAsync(menuId, cancellationToken: cancellationToken);
        menu.AddDinnerId(dinnerId);

        await repository.UpdateAsync(menu, cancellationToken);
    }
}