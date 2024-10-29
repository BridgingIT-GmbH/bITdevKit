// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using Common;
using DevKit.Application.Commands;
using DevKit.Domain.Repositories;
using Domain.Model;
using Microsoft.Extensions.Logging;

//tag::DatabaseTransaction[]
public class ForecastUpdateCommandHandler(
    ILoggerFactory loggerFactory,
    IWeatherDataAdapter dataAdapter,
    IGenericRepository<Forecast> forecastRepository,
    IGenericRepository<ForecastType> forecastTypeRepository,
    IRepositoryTransaction<Forecast> transaction)
    : CommandHandlerBase<ForecastUpdateCommand>(loggerFactory)
{
    public override async Task<CommandResponse> Process(
        ForecastUpdateCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"import forecasts for city {command.City.Name}");

        await transaction.ExecuteScopedAsync(async () =>
            {
                var type = await forecastTypeRepository
                    .FindOneAsync("102954ff-aa73-495b-a730-98f2d5ca10f3", cancellationToken: cancellationToken)
                    .AnyContext(); // find a specific type (AAA)

                // retrieve forecasts from external system, anti corruption is implemented by using an adapter
                await foreach (var forecast in dataAdapter.ToForecastAsync(command.City)
                                   .WithCancellation(cancellationToken))
                {
                    forecast.TypeId = type.Id;
                    await forecastRepository.UpsertAsync(forecast, cancellationToken).AnyContext();
                }
            })
            .AnyContext();

        return new CommandResponse();
    }
}