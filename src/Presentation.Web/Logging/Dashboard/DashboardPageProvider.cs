// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides log entry dashboard page descriptors.
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
        yield return new DashboardPage("Logs", "journal-text", DashboardEndpoints.BuildLogEntriesPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 40,
            Description = "Inspect log entries",
            Card = GetCardAsync
        };

        yield return new DashboardPage("Errors", "exclamation-octagon", DashboardEndpoints.BuildErrorsPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 41,
            Description = "Inspect error log entries",
            Card = GetErrorsCardAsync
        };

        yield return new DashboardPage("Logs Stream", "terminal", DashboardEndpoints.BuildLogEntriesStreamPath(options))
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 42,
            Description = "Live log stream",
            Card = GetStreamCard
        };
    }

    private static ValueTask<DashboardPageCard> GetStreamCard(HttpContext context)
    {
        var logEntryService = context.RequestServices.GetService<ILogEntryService>();
        return ValueTask.FromResult(new DashboardPageCard("Logs Stream", "Live log tail", logEntryService is null ? "Unavailable" : "Live")
        {
            Detail = logEntryService is null ? "ILogEntryService is not registered" : "Stream stored log entries from the database",
            Icon = "terminal",
            Url = DashboardEndpoints.BuildLogEntriesStreamPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>()),
            Group = "bdk",
            GroupOrder = 0,
            Order = 42
        });
    }

    private static async ValueTask<DashboardPageCard> GetErrorsCardAsync(HttpContext context)
    {
        var logEntryService = context.RequestServices.GetService<ILogEntryService>();
        if (logEntryService is null)
        {
            return new DashboardPageCard("Errors", "Stored error entries", "Unavailable")
            {
                Detail = "ILogEntryService is not registered",
                Icon = "exclamation-octagon",
                Url = DashboardEndpoints.BuildErrorsPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>()),
                Group = "bdk",
                GroupOrder = 0,
                Order = 41
            };
        }

        try
        {
            var start = DateTimeOffset.Now.Date;
            var stats = await logEntryService.GetStatisticsAsync(start, null, null, context.RequestAborted);
            var errors = stats.LevelCounts
                .Where(pair => pair.Key >= LogLevel.Error)
                .Sum(pair => pair.Value);

            return new DashboardPageCard("Errors", "Stored error entries", errors.ToString("N0", CultureInfo.InvariantCulture))
            {
                Detail = "Error and fatal entries today",
                Icon = "exclamation-octagon",
                Url = DashboardEndpoints.BuildErrorsPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>()),
                Group = "bdk",
                GroupOrder = 0,
                Order = 41
            };
        }
        catch (Exception ex)
        {
            return new DashboardPageCard("Errors", "Stored error entries", "Error")
            {
                Detail = ex.Message,
                Icon = "exclamation-octagon",
                Url = DashboardEndpoints.BuildErrorsPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>()),
                Group = "bdk",
                GroupOrder = 0,
                Order = 41
            };
        }
    }

    private static async ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var logEntryService = context.RequestServices.GetService<ILogEntryService>();
        if (logEntryService is null)
        {
            return CreateCard(context, "Unavailable", "ILogEntryService is not registered");
        }

        try
        {
            var start = DateTimeOffset.Now.Date;
            var stats = await logEntryService.GetStatisticsAsync(start, null, null, context.RequestAborted);
            var count = stats.LevelCounts.Values.Sum();
            var errors = stats.LevelCounts
                .Where(pair => pair.Key >= LogLevel.Error)
                .Sum(pair => pair.Value);

            return CreateCard(
                context,
                count.ToString("N0", CultureInfo.InvariantCulture),
                $"{errors.ToString("N0", CultureInfo.InvariantCulture)} error/fatal today");
        }
        catch (Exception ex)
        {
            return CreateCard(context, "Error", ex.Message);
        }
    }

    private static DashboardPageCard CreateCard(HttpContext context, string value, string detail)
    {
        return new DashboardPageCard("Logs", "Stored log entries", value)
        {
            Detail = detail,
            Icon = "journal-text",
            Url = DashboardEndpoints.BuildLogEntriesPath(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>()),
            Group = "bdk",
            GroupOrder = 0,
            Order = 40
        };
    }
}
