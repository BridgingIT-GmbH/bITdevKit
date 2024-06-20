// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Common;
using EnsureThat;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

public class ForecastImportJob : JobBase,
    IRetryJobScheduling,
    IChaosExceptionJobScheduling
{
    private readonly IMediator mediator;

    public ForecastImportJob(
        ILoggerFactory loggerFactory,
        IMediator mediator)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(mediator, nameof(mediator));

        this.mediator = mediator;
    }

    RetryJobSchedulingOptions IRetryJobScheduling.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    ChaosExceptionJobSchedulingOptions IChaosExceptionJobScheduling.Options => new() { InjectionRate = 0.10 };

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var response = await this.mediator.Send(new CityFindAllQuery(), cancellationToken).AnyContext();

        foreach (var result in response?.Result.SafeNull())
        {
            await this.mediator.Send(
                new ForecastUpdateCommand(result.City), cancellationToken).AnyContext();
        }

        await this.mediator.Publish( // TODO: umstellen auf Message (broker.Publish())
            new ForecastsImportedEvent(response?.Result?.Select(r => r.City.Name)), cancellationToken).AnyContext();
    }
}
