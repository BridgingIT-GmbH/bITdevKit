// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Profiling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Provides the Profiling dashboard navigation and index card.</summary>
/// <param name="dashboardOptions">The shared dashboard endpoint options.</param>
/// <example><code>var pages = provider.GetPages(httpContext);</code></example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions dashboardOptions)
    : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        if (
            httpContext.RequestServices.GetService<ProfilingOptions>()?.Enabled != true
            || httpContext.RequestServices.GetService<IProfilingControlService>() is null
            || httpContext.RequestServices.GetService<IProfilingQueryService>() is null
        )
        {
            yield break;
        }

        yield return new DashboardPage(
            "profiling",
            "Profiling",
            "speedometer2",
            DashboardEndpoints.BuildProfilingPath(dashboardOptions)
        )
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 25,
            Description = "Collect and analyze focused runtime snapshots",
            Card = GetCardAsync,
        };
    }

    private static async ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<DashboardEndpointsOptions>();
        var control = context.RequestServices.GetService<IProfilingControlService>();
        if (control is null)
        {
            return CreateCard(options, "Unavailable", "Profiling control is not registered.");
        }

        try
        {
            var result = await control.GetStatusAsync(context.RequestAborted).ConfigureAwait(false);
            if (result.IsFailure || !result.Value.Available)
            {
                return CreateCard(options, "Unavailable", "Profiling infrastructure is unavailable.");
            }

            if (!result.Value.Enabled)
            {
                return CreateCard(options, "Disabled", "Profiling collection is disabled.");
            }

            var session = result.Value.Session;
            return session is null
                ? CreateCard(options, "Idle", "No profiling session is active.")
                : CreateCard(
                    options,
                    session.State.ToString(),
                    $"Session {session.Identity.Key}; {result.Value.Participations.Count} participating nodes"
                );
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return CreateCard(options, "Error", "Profiling status could not be loaded.");
        }
    }

    private static DashboardPageCard CreateCard(
        DashboardEndpointsOptions options,
        string value,
        string detail
    ) =>
        new("Profiling", "Runtime snapshot analysis", value)
        {
            Detail = detail,
            Icon = "speedometer2",
            Url = DashboardEndpoints.BuildProfilingPath(options),
            Group = "bdk",
            GroupOrder = 0,
            Order = 25,
        };
}
