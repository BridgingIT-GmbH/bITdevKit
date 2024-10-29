// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System.Linq.Expressions;
using Common;
using DevKit.Application.Queries;
using DevKit.Domain.Repositories;
using Domain.Model;
using Microsoft.Extensions.Logging;

public class ForecastFindAllDescriptionsQueryHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Forecast> forecastRepository)
    : QueryHandlerBase<ForecastFindAllDescriptionsQuery, IEnumerable<string>>(loggerFactory)
{
    public override async Task<QueryResponse<IEnumerable<string>>> Process(
        ForecastFindAllDescriptionsQuery query,
        CancellationToken cancellationToken)
    {
        var descriptions = await forecastRepository.ProjectAllAsync(e => e.Description, // projection
                new FindOptions<Forecast>
                {
                    Order = new OrderOption<Forecast>(e => e.Description), Distinct = new DistinctOption<Forecast>()
                },
                cancellationToken)
            .AnyContext();

        return new QueryResponse<IEnumerable<string>> { Result = descriptions };
    }
}

public class ForecastProjection
{
    public static Expression<Func<Forecast, ForecastProjection>> Expression =>
        e => new ForecastProjection { Description = e.Description };

    public string Description { get; set; }
}