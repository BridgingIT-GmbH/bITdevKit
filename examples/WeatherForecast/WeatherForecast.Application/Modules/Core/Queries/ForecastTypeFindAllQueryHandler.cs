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

public class ForecastTypeFindAllQueryHandler : QueryHandlerBase<ForecastTypeFindAllQuery, IEnumerable<ForecastType>>
{
    private readonly IGenericRepository<ForecastType> forecastTypeRepository;

    public ForecastTypeFindAllQueryHandler(
        ILoggerFactory loggerFactory,
        IGenericRepository<ForecastType> forecastTypeRepository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(forecastTypeRepository, nameof(forecastTypeRepository));

        this.forecastTypeRepository = forecastTypeRepository;
    }

    public override async Task<QueryResponse<IEnumerable<ForecastType>>> Process(
        ForecastTypeFindAllQuery query,
        CancellationToken cancellationToken)
    {
        var forecastTypes = await this.forecastTypeRepository.FindAllAsync(
            cancellationToken: cancellationToken).AnyContext();

        return new QueryResponse<IEnumerable<ForecastType>>()
        {
            Result = forecastTypes
        };
    }
}
