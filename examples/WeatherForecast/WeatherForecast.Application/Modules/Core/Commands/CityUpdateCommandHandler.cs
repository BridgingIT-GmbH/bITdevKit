// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using Common;
using DevKit.Application.Commands;
using DevKit.Domain;
using DevKit.Domain.Repositories;
using Domain.Model;
using MediatR;
using Microsoft.Extensions.Logging;

public class CityUpdateCommandHandler(
    ILoggerFactory loggerFactory,
    IMediator mediator,
    IGenericRepository<City> repository)
    : CommandHandlerBase<CityUpdateCommand, AggregateUpdatedCommandResult>(loggerFactory)
{
    private readonly IMediator mediator = mediator;

    public override async Task<CommandResponse<AggregateUpdatedCommandResult>> Process(
        CityUpdateCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ update city with name: {command.Model.Name} ({command.Model.Country})");

        if (!await repository.ExistsAsync(command.Model.Id, cancellationToken).AnyContext())
        {
            throw new EntityNotFoundException();
        }

        var entity =
            await repository.FindOneAsync(command.Model.Id, cancellationToken: cancellationToken).AnyContext() ??
            throw new AggregateNotFoundException(nameof(City));
        entity.Update(command.Model.Name, command.Model.Country, command.Model.Longitude, command.Model.Latitude);
        await repository.UpsertAsync(entity, cancellationToken).AnyContext();

        // TODO: invalidate query cache

        return new CommandResponse<AggregateUpdatedCommandResult>
        {
            Result = new AggregateUpdatedCommandResult(command.Model.Id.ToString())
        };
    }
}