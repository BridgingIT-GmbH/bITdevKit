// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

public class HealthCheckJob(
    ILoggerFactory loggerFactory,
    HealthCheckService healthCheckService) : JobBase(loggerFactory)
{
    private readonly HealthCheckService healthCheckService = healthCheckService;

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
        }
    }
}