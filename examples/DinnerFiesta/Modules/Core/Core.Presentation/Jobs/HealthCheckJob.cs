// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.JobScheduling;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

public class HealthCheckJob : JobBase
{
    private readonly HealthCheckService healthCheckService;
    private readonly IHubContext<SignalRHub> hub;

    public HealthCheckJob(
        ILoggerFactory loggerFactory,
        HealthCheckService healthCheckService,
        IHubContext<SignalRHub> hub)
        : base(loggerFactory)
    {
        this.healthCheckService = healthCheckService;
        this.hub = hub;
    }

    public override async Task Process(Quartz.IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var report = await this.healthCheckService.CheckHealthAsync(cancellationToken);
        if (report is not null)
        {
            if (report.Status == HealthStatus.Healthy)
            {
                this.Logger.LogInformation("{LogKey} health status is {HealthStatus}", "JOB", report.Status.ToString().ToLower());
            }
            else
            {
                this.Logger.LogWarning("{LogKey} health status is {HealthStatus}: {@HealthReport}", "JOB", report.Status.ToString().ToLower(), report);
            }

            await this.hub.Clients.All.SendAsync(
                    "CheckHealth",
                    $"{report.Status}",
                    cancellationToken);
        }
    }
}