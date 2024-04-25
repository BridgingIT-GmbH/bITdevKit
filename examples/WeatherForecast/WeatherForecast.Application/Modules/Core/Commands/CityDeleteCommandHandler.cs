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
using BridgingIT.DevKit.Examples.WeatherForecast.Domain;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using EnsureThat;
using MediatR;
using Microsoft.Extensions.Logging;

public class CityDeleteCommandHandler : CommandHandlerBase<CityDeleteCommand, AggregateDeletedCommandResult>
{
    private readonly IMediator mediator;
    private readonly IWeatherDataAdapter dataAdapter;
    private readonly IGenericRepository<City> cityRepository;
    private readonly IGenericRepository<Forecast> forecastRepository;

    public CityDeleteCommandHandler(
        ILoggerFactory loggerFactory,
        IMediator mediator,
        IWeatherDataAdapter dataAdapter,
        IGenericRepository<City> cityRepository,
        IGenericRepository<Forecast> forecastRepository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(mediator, nameof(mediator));
        EnsureArg.IsNotNull(dataAdapter, nameof(dataAdapter));
        EnsureArg.IsNotNull(cityRepository, nameof(cityRepository));
        EnsureArg.IsNotNull(forecastRepository, nameof(forecastRepository));

        this.mediator = mediator;
        this.dataAdapter = dataAdapter;
        this.cityRepository = cityRepository;
        this.forecastRepository = forecastRepository;
    }

    public override async Task<CommandResponse<AggregateDeletedCommandResult>> Process(
        CityDeleteCommand command,
        CancellationToken cancellationToken)
    {
        var entity = (await this.mediator.Send(
            new CityFindOneQuery(command.Name), cancellationToken).AnyContext())?.Result?.City;

        this.Logger.LogInformation($"+++ delete city with name: {entity.Name} ({entity.Country})");

        // soft delete city
        entity.Delete("command"); // checks business rules
        await this.cityRepository.UpsertAsync(entity, cancellationToken).AnyContext();

        // hard delete all city forecasts
        foreach (var forecast in await this.forecastRepository.FindAllAsync(
            new ForecastForCitySpecification(entity.Id),
            cancellationToken: cancellationToken).AnyContext())
        {
            await this.forecastRepository.DeleteAsync(forecast, cancellationToken).AnyContext();
        }

        return new CommandResponse<AggregateDeletedCommandResult>
        {
            Result = new AggregateDeletedCommandResult(entity.Id.ToString())
        };
    }
}
