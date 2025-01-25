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

public class CountryFindAllQueryHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<City> cityRepository)
    : QueryHandlerBase<CountryFindAllQuery, IEnumerable<string>>(loggerFactory)
{
    public override async Task<QueryResponse<IEnumerable<string>>> Process(
        CountryFindAllQuery query,
        CancellationToken cancellationToken)
    {
        var countries = await cityRepository.ProjectAllAsync(e => e.Country,
                new FindOptions<City>
                {
                    Order = new OrderOption<City>(e => e.Country),
                    Distinct = new DistinctOption<City>()
                },
                cancellationToken)
            .AnyContext();

        return new QueryResponse<IEnumerable<string>> { Result = countries };
    }
}