// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Jobs.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the Jobs dashboard page descriptor and index card for the dashboard shell.
/// </summary>
/// <example>
/// <code>
/// var pages = provider.GetPages(httpContext);
/// </code>
/// </example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions options) : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        yield return new DashboardPage("jobs", "Jobs", "calendar2-check", DashboardEndpoints.BuildJobsPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 40,
            Description = "Registered jobs, occurrences, and scheduling controls",
            Card = GetCardAsync
        };
    }

    private static async ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var query = context.RequestServices.GetService<IJobSchedulerQueryService>();
        var url = DashboardEndpoints.BuildJobsPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>());
        var databaseReadyService = context.RequestServices.GetService<IDatabaseReadyService>();

        if (databaseReadyService?.IsReady() == false)
        {
            return new DashboardPageCard("Jobs", "Registered jobs", "-")
            {
                Detail = "Database starting",
                Icon = "calendar2-check",
                Url = url,
                Group = "bdk",
                GroupOrder = 0,
                Order = 40
            };
        }

        if (query is null)
        {
            return new DashboardPageCard("Jobs", "Registered jobs", "Unavailable")
            {
                Detail = "AddJobScheduler() is not registered",
                Icon = "calendar2-check",
                Url = url,
                Group = "bdk",
                GroupOrder = 0,
                Order = 40
            };
        }

        try
        {
            var summaryTask = query.GetDashboardSummaryAsync(cancellationToken: context.RequestAborted);
            var metricsTask = query.GetMetricsAsync(cancellationToken: context.RequestAborted);
            await Task.WhenAll(summaryTask, metricsTask);

            var summary = await summaryTask;
            if (summary.IsFailure)
            {
                return new DashboardPageCard("Jobs", "Registered jobs", "Error")
                {
                    Detail = summary.Errors.FirstOrDefault()?.Message,
                    Icon = "calendar2-check",
                    Url = url,
                    Group = "bdk",
                    GroupOrder = 0,
                    Order = 40
                };
            }

            var s = summary.Value;
            var jobCount = s.JobFacets?.EnabledCount ?? 0;

            var metrics = await metricsTask;
            var completed = metrics.IsSuccess
                ? metrics.Value.OccurrenceCountsByStatus?.GetValueOrDefault(JobOccurrenceStatus.Completed) ?? 0
                : 0;

            var detail = $"{s.RunningOccurrenceCount} running, {completed.ToString("N0", CultureInfo.InvariantCulture)} completed, {s.FailedOccurrenceCount} failed";
            if (s.RetryScheduledCount > 0)
            {
                detail += $", {s.RetryScheduledCount} retrying";
            }

            return new DashboardPageCard("Jobs", "Registered jobs", jobCount.ToString("N0", CultureInfo.InvariantCulture))
            {
                Detail = detail,
                Icon = "calendar2-check",
                Url = url,
                Group = "bdk",
                GroupOrder = 0,
                Order = 40
            };
        }
        catch (Exception ex)
        {
            return new DashboardPageCard("Jobs", "Registered jobs", "Error")
            {
                Detail = ex.Message,
                Icon = "calendar2-check",
                Url = url,
                Group = "bdk",
                GroupOrder = 0,
                Order = 40
            };
        }
    }
}
