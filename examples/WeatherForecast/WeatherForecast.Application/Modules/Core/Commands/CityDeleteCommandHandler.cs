// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using Common;
using DevKit.Application.Commands;
using DevKit.Domain.Repositories;
using Domain;
using Domain.Model;
using MediatR;
using Microsoft.Extensions.Logging;

public class CityDeleteCommandHandler(
    ILoggerFactory loggerFactory,
    IMediator mediator,
    IWeatherDataAdapter dataAdapter,
    IGenericRepository<City> cityRepository,
    IGenericRepository<Forecast> forecastRepository)
    : CommandHandlerBase<CityDeleteCommand, AggregateDeletedCommandResult>(loggerFactory)
{
    private readonly IWeatherDataAdapter dataAdapter = dataAdapter;

    public override async Task<CommandResponse<AggregateDeletedCommandResult>> Process(
        CityDeleteCommand command,
        CancellationToken cancellationToken)
    {
        var entity = (await mediator.Send(new CityFindOneQuery(command.Name), cancellationToken).AnyContext())
            ?.Result?.City;

        this.Logger.LogInformation($"+++ delete city with name: {entity.Name} ({entity.Country})");

        // soft delete city
        entity.Delete("command"); // checks business rules
        await cityRepository.UpsertAsync(entity, cancellationToken).AnyContext();

        // hard delete all city forecasts
        foreach (var forecast in await forecastRepository.FindAllAsync(new ForecastForCitySpecification(entity.Id),
                         cancellationToken: cancellationToken)
                     .AnyContext())
        {
            await forecastRepository.DeleteAsync(forecast, cancellationToken).AnyContext();
        }

        return new CommandResponse<AggregateDeletedCommandResult>
        {
            Result = new AggregateDeletedCommandResult(entity.Id.ToString())
        };
    }
}