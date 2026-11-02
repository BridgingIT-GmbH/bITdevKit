// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Maps the Entities dashboard page and its fragment, export and write endpoints.
/// </summary>
/// <example>
/// <code>
/// // registered automatically by AddDashboardPlugins(); routes live below {dashboardGroup}/entities
/// // GET  /entities?entity=Invoice&amp;f=Total~gt~100
/// // POST /entities/update?preview=true
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options = null) : EndpointsBase, IDashboardEndpoints
{
    internal const string EntitiesPath = "/entities";

    internal static string BuildEntitiesPath(DashboardEndpointsOptions options) =>
        DashboardPath.Combine(options?.GroupPath, "entities");

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (options is not { Enabled: true }
            || options.DisabledPageKeys?.Contains("entities") == true
            || app.ServiceProvider.GetService<EntitiesDashboardCatalog>() is not { HasSources: true })
        {
            return;
        }

        var path = BuildEntitiesPath(options);
        var group = this.MapGroup(app, options).MapGroup(EntitiesPath).WithTags("_bdk.Dashboard");
        group.AddEndpointFilter(async (invocationContext, next) =>
        {
            invocationContext.HttpContext.Response.Headers.CacheControl = "no-store";

            return await next(invocationContext);
        });

        group.MapGet(string.Empty, (HttpContext http, CancellationToken ct) => EntitiesDashboardHandlers.GetPageAsync(http, path, ct))
            .WithName("_bdk.Dashboard.EntitiesPage")
            .ExcludeFromDescription();

        group.MapGet("/rows", (HttpContext http, CancellationToken ct) => EntitiesDashboardHandlers.GetRowsAsync(http, path, ct))
            .WithName("_bdk.Dashboard.EntitiesRows")
            .ExcludeFromDescription();

        group.MapGet("/detail", (HttpContext http, CancellationToken ct) => EntitiesDashboardHandlers.GetDetailAsync(http, path, ct))
            .WithName("_bdk.Dashboard.EntitiesDetail")
            .ExcludeFromDescription();

        group.MapGet("/editor", (HttpContext http, CancellationToken ct) => EntitiesDashboardHandlers.GetEditorAsync(http, path, ct))
            .WithName("_bdk.Dashboard.EntitiesEditor")
            .ExcludeFromDescription();

        group.MapGet("/export", (HttpContext http, CancellationToken ct) => EntitiesDashboardHandlers.ExportAsync(http, ct))
            .WithName("_bdk.Dashboard.EntitiesExport")
            .ExcludeFromDescription();

        group.MapPost("/create", (HttpContext http, CancellationToken ct) => EntitiesDashboardHandlers.CreateAsync(http, ct))
            .WithName("_bdk.Dashboard.EntitiesCreate")
            .Accepts<JsonElement>("application/json")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost("/update", (HttpContext http, bool? preview, CancellationToken ct) =>
                EntitiesDashboardHandlers.UpdateAsync(http, preview == true, ct))
            .WithName("_bdk.Dashboard.EntitiesUpdate")
            .Accepts<JsonElement>("application/json")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost("/delete", (HttpContext http, CancellationToken ct) => EntitiesDashboardHandlers.DeleteAsync(http, ct))
            .WithName("_bdk.Dashboard.EntitiesDelete")
            .Accepts<JsonElement>("application/json")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost("/bulk-delete", (HttpContext http, CancellationToken ct) => EntitiesDashboardHandlers.BulkDeleteAsync(http, ct))
            .WithName("_bdk.Dashboard.EntitiesBulkDelete")
            .Accepts<JsonElement>("application/json")
            .DisableAntiforgery()
            .ExcludeFromDescription();
    }
}
