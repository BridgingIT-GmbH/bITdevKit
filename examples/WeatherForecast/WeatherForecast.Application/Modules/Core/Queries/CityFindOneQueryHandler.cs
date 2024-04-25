// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using EnsureThat;
using Microsoft.Extensions.Logging;

public class CityFindOneQueryHandler : QueryHandlerBase<CityFindOneQuery, CityQueryResponse>
{
    private readonly IGenericRepository<City> cityRepository;
    private readonly IGenericRepository<Forecast> forecastRepository;

    public CityFindOneQueryHandler(
        ILoggerFactory loggerFactory,
        IGenericRepository<City> cityRepository,
        IGenericRepository<Forecast> forecastRepository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(cityRepository, nameof(cityRepository));
        EnsureArg.IsNotNull(forecastRepository, nameof(forecastRepository));

        this.cityRepository = cityRepository;
        this.forecastRepository = forecastRepository;
    }

    public override async Task<QueryResponse<CityQueryResponse>> Process(
        CityFindOneQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.Name.IsNullOrEmpty())
        {
            // find by name
            var city = await this.cityRepository.FindOneAsync(new CityHasNameSpecification(query.Name).And(new CityIsNotDeletedSpecification()), cancellationToken: cancellationToken).AnyContext()
                ?? throw new AggregateNotFoundException(nameof(City));
            var forecasts = city is not null
                ? await this.forecastRepository.FindAllAsync(
                    new Specification<Forecast>(c => c.CityId == city.Id).And(new ForecastIsInFutureSpecification()),
                    new FindOptions<Forecast>(
                        order: new OrderOption<Forecast>(f => f.Timestamp)), cancellationToken).AnyContext()
                : null;

            return new QueryResponse<CityQueryResponse>()
            {
                Result = CityQueryResponse.Create(city, forecasts)
            };
        }
        else
        {
            Check.Throw(new IBusinessRule[]
            {
                new LongitudeShouldBeInRange(query.Longitude),
                new LatitudeShouldBeInRange(query.Latitude)
            });

            var city = await this.cityRepository.FindOneAsync(new CityHasLocationSpecification(query.Longitude, query.Latitude), cancellationToken: cancellationToken).AnyContext()
                    ?? throw new AggregateNotFoundException(nameof(City));
            var forecasts = city is not null
                ? await this.forecastRepository.FindAllAsync(new Specification<Forecast>(c => c.CityId == city.Id).And(new ForecastIsInFutureSpecification()), new FindOptions<Forecast>(
                        order: new OrderOption<Forecast>(f => f.Timestamp)), cancellationToken).AnyContext()
                : null;

            return new QueryResponse<CityQueryResponse>()
            {
                Result = CityQueryResponse.Create(city, forecasts)
            };
        }
    }
}
