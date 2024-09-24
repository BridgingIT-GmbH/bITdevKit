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

public class CityFindAllQueryHandler : QueryHandlerBase<CityFindAllQuery, IEnumerable<CityQueryResponse>>
{
    private readonly IGenericRepository<City> cityRepository;

    public CityFindAllQueryHandler(
        ILoggerFactory loggerFactory,
        IGenericRepository<City> cityRepository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(cityRepository, nameof(cityRepository));

        this.cityRepository = cityRepository;
    }

    public override async Task<QueryResponse<IEnumerable<CityQueryResponse>>> Process(
        CityFindAllQuery request,
        CancellationToken cancellationToken)
    {
        var cities = await this.cityRepository.FindAllAsync(new CityIsNotDeletedSpecification(),
                cancellationToken: cancellationToken)
            .AnyContext();

        return new QueryResponse<IEnumerable<CityQueryResponse>>
        {
            Result = cities.Select(c => CityQueryResponse.Create(c))
        };
    }
}