// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core.Dashboard;

using System.Net;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Maps WeatherFiesta dashboard plugin endpoints.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    private const string CitiesPath = "/app/cities";
    private const string CitiesContentPath = "/app/cities/content";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard.WeatherFiesta");

        group.MapDashboardPage(
            CitiesPath,
            "/Modules/Core/Dashboard/Pages/Index.cshtml",
            typeof(DashboardEndpoints).Assembly,
            "_bdk.Dashboard.WeatherFiesta.Cities",
            "WeatherFiesta Dashboard Cities",
            "Shows WeatherFiesta city data inside the bITdevKit dashboard shell.");

        group.MapDashboardPage(
            CitiesContentPath,
            "/Modules/Core/Dashboard/Pages/Content.cshtml",
            typeof(DashboardEndpoints).Assembly,
            "_bdk.Dashboard.WeatherFiesta.CitiesContent",
            "WeatherFiesta Dashboard Cities Content",
            "Shows WeatherFiesta city dashboard content.");
    }

    internal static string BuildCitiesPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, CitiesPath);
    }

    internal static string BuildCitiesContentPath(DashboardEndpointsOptions options)
    {
        return DashboardPath.Combine(options?.GroupPath, CitiesContentPath);
    }
}
