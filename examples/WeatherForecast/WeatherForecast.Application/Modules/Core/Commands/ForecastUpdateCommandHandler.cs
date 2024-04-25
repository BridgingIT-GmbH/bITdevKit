// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using EnsureThat;
using Microsoft.Extensions.Logging;

//tag::DatabaseTransaction[]
public class ForecastUpdateCommandHandler : CommandHandlerBase<ForecastUpdateCommand>
{
    private readonly IWeatherDataAdapter dataAdapter;
    private readonly IGenericRepository<Forecast> forecastRepository;
    private readonly IGenericRepository<ForecastType> forecastTypeRepository;
    private readonly IRepositoryTransaction<Forecast> transaction;

    public ForecastUpdateCommandHandler(
        ILoggerFactory loggerFactory,
        IWeatherDataAdapter dataAdapter,
        IGenericRepository<Forecast> forecastRepository,
        IGenericRepository<ForecastType> forecastTypeRepository,
        IRepositoryTransaction<Forecast> transaction)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(dataAdapter, nameof(dataAdapter));
        EnsureArg.IsNotNull(forecastRepository, nameof(forecastRepository));
        EnsureArg.IsNotNull(forecastTypeRepository, nameof(forecastTypeRepository));

        this.dataAdapter = dataAdapter;
        this.forecastRepository = forecastRepository;
        this.forecastTypeRepository = forecastTypeRepository;
        this.transaction = transaction;
    }

    public override async Task<CommandResponse> Process(ForecastUpdateCommand command, CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"import forecasts for city {command.City.Name}");

        await this.transaction.ExecuteScopedAsync(async () =>
        {
            var type = await this.forecastTypeRepository.FindOneAsync("102954ff-aa73-495b-a730-98f2d5ca10f3", cancellationToken: cancellationToken).AnyContext(); // find a specific type (AAA)

            // retrieve forecasts from external system, anti corruption is implemented by using an adapter
            await foreach (var forecast in this.dataAdapter.ToForecastAsync(command.City).WithCancellation(cancellationToken))
            {
                forecast.TypeId = type.Id;
                await this.forecastRepository.UpsertAsync(forecast, cancellationToken).AnyContext();
            }
        }).AnyContext();

        return new CommandResponse();
    }
}