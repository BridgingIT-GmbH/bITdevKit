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

public class DinnerCreatedDomainEventHandler : DomainEventHandlerBase<DinnerCreatedDomainEvent>
{
    private readonly IGenericRepository<Menu> repository;

    public DinnerCreatedDomainEventHandler(ILoggerFactory loggerFactory, IGenericRepository<Menu> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public override bool CanHandle(DinnerCreatedDomainEvent notification)
    {
        return true;
    }

    public override async Task Process(DinnerCreatedDomainEvent @event, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(@event, nameof(@event));
        EnsureArg.IsNotNull(@event.Dinner, nameof(@event.Dinner));
        EnsureArg.IsNotNull(@event.Dinner.MenuId, nameof(@event.Dinner.MenuId));

        this.Logger.LogInformation($"checking Dinner: {@event.Dinner.Name} with Menu: {@event.Dinner.MenuId} for Host: {@event.Dinner.HostId}");

        Check.Throw(new IBusinessRule[]
        {
            new MenuForHostMustExistRule(this.repository, @event.Dinner.HostId, @event.Dinner.MenuId),
        });

        var menu = await this.repository.FindOneAsync(@event.Dinner.MenuId, cancellationToken: cancellationToken);
        menu.AddDinnerId((DinnerId)@event.Dinner.Id); // TODO: akward cast here
        //menu.AddDinnerId(@event.Dinner.Id);

        await this.repository.UpdateAsync(menu, cancellationToken);
    }
}