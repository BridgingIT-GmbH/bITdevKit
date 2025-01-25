// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using Common;
using DevKit.Application.Queries;
using DevKit.Domain.Repositories;
using Domain.Model;
using Microsoft.Extensions.Logging;

public class ForecastFindAllQueryHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Forecast> forecastRepository)
    : QueryHandlerBase<ForecastFindAllQuery, IEnumerable<ForecastQueryResponse>>(loggerFactory)
{
    public override async Task<QueryResponse<IEnumerable<ForecastQueryResponse>>> Process(
        ForecastFindAllQuery query,
        CancellationToken cancellationToken)
    {
        var forecasts = await forecastRepository.FindAllAsync( // repo takes care of the filter
                query.Filter, cancellationToken: cancellationToken);

        return new QueryResponse<IEnumerable<ForecastQueryResponse>>
        {
            Result = forecasts.Select(c => ForecastQueryResponse.Create(c))
        };
    }
}

public class ForecastFindAllPagedQueryHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Forecast> forecastRepository)
    : QueryHandlerBase<ForecastFindAllPagedQuery, ResultPaged<Forecast>>(loggerFactory)
{
    public override async Task<QueryResponse<ResultPaged<Forecast>>> Process(
        ForecastFindAllPagedQuery query, CancellationToken cancellationToken)
    {
        var result = await forecastRepository.FindAllResultPagedAsync( // repo takes care of the filter and paging
                query.Filter, cancellationToken: cancellationToken);

        return QueryResponse.For(result); // new result to response syntax (use For)
    }
}