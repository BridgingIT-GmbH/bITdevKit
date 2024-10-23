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

public class ForecastFindAllQueryHandler : QueryHandlerBase<ForecastFindAllQuery, IEnumerable<ForecastQueryResponse>>
{
    private readonly IGenericRepository<Forecast> forecastRepository;

    public ForecastFindAllQueryHandler(
        ILoggerFactory loggerFactory,
        IGenericRepository<Forecast> forecastRepository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(forecastRepository, nameof(forecastRepository));

        this.forecastRepository = forecastRepository;
    }

    public override async Task<QueryResponse<IEnumerable<ForecastQueryResponse>>> Process(
        ForecastFindAllQuery query,
        CancellationToken cancellationToken)
    {
        var forecasts = await this.forecastRepository.FindAllAsync(
                query.Filter,
                cancellationToken: cancellationToken) // //new FindOptions<Forecast> { Order = new OrderOption<Forecast>(e => e.Timestamp) },
            .AnyContext();

        return new QueryResponse<IEnumerable<ForecastQueryResponse>>
        {
            Result = forecasts.Select(c => ForecastQueryResponse.Create(c))
        };
    }
}