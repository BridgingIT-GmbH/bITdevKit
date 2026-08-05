// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Broadcasting.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Provides the Broadcasting dashboard navigation and index-card descriptor.</summary>
/// <param name="options">The shared dashboard endpoint options.</param>
/// <example><code>var pages = provider.GetPages(httpContext);</code></example>
public sealed class DashboardPageProvider(DashboardEndpointsOptions options)
    : IDashboardPageProvider
{
    /// <inheritdoc />
    public IEnumerable<DashboardPage> GetPages(HttpContext httpContext)
    {
        var broadcastingOptions =
            httpContext.RequestServices.GetService<BroadcastingOptions>();
        if (
            broadcastingOptions?.Enabled != true
            || httpContext.RequestServices.GetService<IBroadcastingDiagnostics>() is null
        )
        {
            yield break;
        }

        yield return new DashboardPage(
            "broadcasting",
            "Broadcasting",
            "broadcast-pin",
            DashboardEndpoints.BuildBroadcastingPath(options)
        )
        {
            Group = "bdk",
            GroupOrder = 0,
            Order = 30,
            Description = "Inspect live nodes and test delivery",
            Card = GetCardAsync,
        };
    }

    private static async ValueTask<DashboardPageCard> GetCardAsync(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<DashboardEndpointsOptions>();
        try
        {
            var snapshot = await GetSnapshotAsync(context);
            if (snapshot is null)
            {
                return CreateCard(options, "Unavailable", "Diagnostics are not registered.");
            }

            if (!snapshot.Enabled)
            {
                return CreateCard(options, "Disabled", "The host runtime is disabled.");
            }

            var active = CountNodes(snapshot, activeOnly: true);
            var total = CountNodes(snapshot, activeOnly: false);
            return CreateCard(
                options,
                active.ToString("N0", CultureInfo.InvariantCulture),
                $"{total.ToString("N0", CultureInfo.InvariantCulture)} registered nodes across {snapshot.Scopes.Count.ToString("N0", CultureInfo.InvariantCulture)} scopes"
            );
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return CreateCard(options, "Error", "Registry diagnostics could not be loaded.");
        }
    }

    private static async Task<BroadcastingDiagnosticSnapshot> GetSnapshotAsync(
        HttpContext context
    )
    {
        var diagnostics = context.RequestServices.GetService<IBroadcastingDiagnostics>();
        return diagnostics is null
            ? null
            : await diagnostics.GetAsync(context.RequestAborted).ConfigureAwait(false);
    }

    private static int CountNodes(
        BroadcastingDiagnosticSnapshot snapshot,
        bool activeOnly
    ) =>
        snapshot
            .Scopes.SelectMany(scope => scope.Nodes)
            .GroupBy(node => node.NodeIdentity, StringComparer.OrdinalIgnoreCase)
            .Count(group => !activeOnly || group.Any(node => node.IsActive));

    private static DashboardPageCard CreateCard(
        DashboardEndpointsOptions options,
        string value,
        string detail
    ) =>
        new("Broadcasting", "Live node delivery", value)
        {
            Detail = detail,
            Icon = "broadcast-pin",
            Url = DashboardEndpoints.BuildBroadcastingPath(options),
            Group = "bdk",
            GroupOrder = 0,
            Order = 30,
        };
}