// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using EnsureThat;
using Microsoft.Extensions.Logging;

public class ForecastFindAllDescriptionsQueryHandler : QueryHandlerBase<ForecastFindAllDescriptionsQuery, IEnumerable<string>>
{
    private readonly IGenericRepository<Forecast> forecastRepository;

    public ForecastFindAllDescriptionsQueryHandler(
        ILoggerFactory loggerFactory,
        IGenericRepository<Forecast> forecastRepository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(forecastRepository, nameof(forecastRepository));

        this.forecastRepository = forecastRepository;
    }

    public override async Task<QueryResponse<IEnumerable<string>>> Process(
        ForecastFindAllDescriptionsQuery query,
        CancellationToken cancellationToken)
    {
        var descriptions = await this.forecastRepository.ProjectAllAsync(
            e => e.Description,                // projection
            options: new FindOptions<Forecast>
            {
                Order = new OrderOption<Forecast>(e => e.Description),
                Distinct = new DistinctOption<Forecast>()
            },
            cancellationToken: cancellationToken).AnyContext();

        return new QueryResponse<IEnumerable<string>>()
        {
            Result = descriptions
        };
    }
}

public class ForecastProjection
{
    public static Expression<Func<Forecast, ForecastProjection>> Expression
    {
        get
        {
            return e => new ForecastProjection()
            {
                Description = e.Description
            };
        }
    }

    public string Description { get; set; }
}
