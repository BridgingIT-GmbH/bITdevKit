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

public class CountryFindAllQueryHandler : QueryHandlerBase<CountryFindAllQuery, IEnumerable<string>>
{
    private readonly IGenericRepository<City> cityRepository;

    public CountryFindAllQueryHandler(
        ILoggerFactory loggerFactory,
        IGenericRepository<City> cityRepository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(cityRepository, nameof(cityRepository));

        this.cityRepository = cityRepository;
    }

    public override async Task<QueryResponse<IEnumerable<string>>> Process(
        CountryFindAllQuery query,
        CancellationToken cancellationToken)
    {
        var countries = await this.cityRepository.ProjectAllAsync(e => e.Country,
                new FindOptions<City>
                {
                    Order = new OrderOption<City>(e => e.Country), Distinct = new DistinctOption<City>()
                },
                cancellationToken)
            .AnyContext();

        return new QueryResponse<IEnumerable<string>> { Result = countries };
    }
}