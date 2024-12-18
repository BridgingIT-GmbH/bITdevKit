﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using Common;
using DevKit.Application.Commands;
using DevKit.Domain.Repositories;
using Domain.Model;
using Microsoft.Extensions.Logging;

public class CityCreateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<City> repository)
    : CommandHandlerBase<CityCreateCommand, AggregateCreatedCommandResult>(loggerFactory)
{
    public override async Task<CommandResponse<AggregateCreatedCommandResult>> Process(
        CityCreateCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ create city with name: {command.Model.Name} ({command.Model.Country})");

        // TODO: check in db if city with name already exists > throw exception
        var entity = City.Create(command.Model.Name,
            command.Model.Country,
            command.Model.Longitude,
            command.Model.Latitude);
        await repository.InsertAsync(entity, cancellationToken).AnyContext();

        return new CommandResponse<AggregateCreatedCommandResult>
        {
            Result = new AggregateCreatedCommandResult(entity.Id.ToString())
        };
    }
}