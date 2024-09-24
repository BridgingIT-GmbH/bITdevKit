// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using Common;
using DevKit.Application.Commands;
using DevKit.Domain;
using DevKit.Domain.Repositories;
using Domain;
using Microsoft.Extensions.Logging;

public class DinnerCreateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Dinner> repository) : CommandHandlerBase<DinnerCreateCommand, Result<Dinner>>(loggerFactory)
{
    public override async Task<CommandResponse<Result<Dinner>>> Process(
        DinnerCreateCommand command,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        var dinner = Dinner.Create(command.Name,
            command.Description,
            DinnerSchedule.Create(command.Schedule.StartDateTime,
                command.Schedule.EndDateTime),
            DinnerLocation.Create(command.Location.Name,
                command.Location.AddressLine1,
                command.Location.AddressLine2,
                command.Location.PostalCode,
                command.Location.City,
                command.Location.Country,
                command.Location.WebsiteUrl,
                command.Location.Latitude,
                command.Location.Longitude),
            command.IsPublic,
            command.MaxGuests,
            MenuId.Create(command.MenuId),
            HostId.Create(command.HostId),
            Price.Create(command.Price.Amount, command.Price.Currency),
            command.ImageUrl is not null ? new Uri(command.ImageUrl) : null);

        DomainRules.Apply([
            new DinnerNameMustBeUniqueRule(repository, dinner.Name),
            new DinnerScheduleMustNotOverlapRule(repository, dinner.HostId, dinner.Schedule)
        ]);

        await repository.InsertAsync(dinner, cancellationToken);

        return CommandResponse.Success(dinner);
    }
}