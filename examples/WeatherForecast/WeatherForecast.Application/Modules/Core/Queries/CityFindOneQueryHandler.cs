// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using Common;
using DevKit.Application.Queries;
using DevKit.Domain;
using DevKit.Domain.Repositories;
using Domain;
using Domain.Model;
using Microsoft.Extensions.Logging;

public class CityFindOneQueryHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<City> cityRepository,
    IGenericRepository<Forecast> forecastRepository)
    : QueryHandlerBase<CityFindOneQuery, CityQueryResponse>(loggerFactory)
{
    public override async Task<QueryResponse<CityQueryResponse>> Process(
        CityFindOneQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.Name.IsNullOrEmpty())
        {
            // find by name
            var city = await cityRepository
                    .FindOneAsync(new CityHasNameSpecification(query.Name).And(new CityIsNotDeletedSpecification()),
                        cancellationToken: cancellationToken)
                    .AnyContext() ??
                throw new AggregateNotFoundException(nameof(City));
            var forecasts = city is not null
                ? await forecastRepository.FindAllAsync(
                        new Specification<Forecast>(c => c.CityId == city.Id).And(
                            new ForecastIsInFutureSpecification()),
                        new FindOptions<Forecast>(order: new OrderOption<Forecast>(f => f.Timestamp)),
                        cancellationToken)
                    .AnyContext()
                : null;

            return new QueryResponse<CityQueryResponse> { Result = CityQueryResponse.Create(city, forecasts) };
        }
        else
        {
            Rule.Add(
                new LongitudeShouldBeInRange(query.Longitude),
                new LatitudeShouldBeInRange(query.Latitude)
            ).Check();

            var city = await cityRepository
                    .FindOneAsync(new CityHasLocationSpecification(query.Longitude, query.Latitude),
                        cancellationToken: cancellationToken)
                    .AnyContext() ??
                throw new AggregateNotFoundException(nameof(City));
            var forecasts = city is not null
                ? await forecastRepository
                    .FindAllAsync(
                        new Specification<Forecast>(c => c.CityId == city.Id).And(
                            new ForecastIsInFutureSpecification()),
                        new FindOptions<Forecast>(order: new OrderOption<Forecast>(f => f.Timestamp)),
                        cancellationToken)
                    .AnyContext()
                : null;

            return new QueryResponse<CityQueryResponse> { Result = CityQueryResponse.Create(city, forecasts) };
        }
    }
}