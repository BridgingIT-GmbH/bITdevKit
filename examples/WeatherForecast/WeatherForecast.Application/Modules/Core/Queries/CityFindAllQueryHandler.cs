// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using Common;
using DevKit.Application.Queries;
using DevKit.Domain.Repositories;
using Domain;
using Domain.Model;
using Microsoft.Extensions.Logging;

public class CityFindAllQueryHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<City> cityRepository)
    : QueryHandlerBase<CityFindAllQuery, IEnumerable<CityQueryResponse>>(loggerFactory)
{
    public override async Task<QueryResponse<IEnumerable<CityQueryResponse>>> Process(
        CityFindAllQuery query,
        CancellationToken cancellationToken)
    {
        var cities = await cityRepository.FindAllAsync(  // repo takes care of the filter
                query.Filter, [new CityIsNotDeletedSpecification()], cancellationToken: cancellationToken);

        return QueryResponse.For(
            cities.Select(c => CityQueryResponse.Create(c)));
    }

    public async Task<QueryResponse<IEnumerable<CityQueryResponse>>> ProcessEntities(
        CityFindAllQuery query,
        CancellationToken cancellationToken)
    {
        var cities = (await cityRepository.FindAllResultAsync( // repo takes care of the filter
                query.Filter, [new CityIsNotDeletedSpecification()], cancellationToken: cancellationToken))
            .Ensure(e => e != null, new EntityNotFoundError())
            .Map(e => e.Select(c => CityQueryResponse.Create(c))).Value;

        return QueryResponse.For(cities);
    }

    public async Task<QueryResponse<Result<IEnumerable<CityQueryResponse>>>> ProcessResult(
        CityFindAllQuery query,
        CancellationToken cancellationToken)
    {
        return QueryResult.For(
            (await cityRepository.FindAllResultAsync( // repo takes care of the filter
                query.Filter, [new CityIsNotDeletedSpecification()], cancellationToken: cancellationToken))
            .Ensure(e => e != null, new EntityNotFoundError())
            .Map(e => e.Select(c => CityQueryResponse.Create(c))));
    }
}