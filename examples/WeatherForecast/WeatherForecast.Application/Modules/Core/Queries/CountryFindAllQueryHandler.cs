// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using EnsureThat;
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
        var countries = await this.cityRepository.ProjectAllAsync(
            e => e.Country,
            options: new FindOptions<City>
            {
                Order = new OrderOption<City>(e => e.Country),
                Distinct = new DistinctOption<City>()
            },
            cancellationToken: cancellationToken).AnyContext();

        return new QueryResponse<IEnumerable<string>>()
        {
            Result = countries
        };
    }
}
