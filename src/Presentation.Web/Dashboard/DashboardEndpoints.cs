// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Net;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

public class DashboardEndpoints(
    //ILogger<DashboardEndpoints> logger,
    DashboardEndpointsOptions options) : EndpointsBase
{
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");
            //.DisableAntiforgery();
            //.RequireCors(nameof(BridgingIT.DevKit.Presentation.Web.Dashboard));
        var paths = options.EndpointPaths;

        group.MapGet("/", this.HandleIndex)
            .WithName("_bdk.Dashboard.Index")
            .WithSummary("Dashboard Index")
            .WithDescription("Shows the dashboard index page.")
            .Produces<string>((int)HttpStatusCode.OK);

        group.MapGet(paths.Metrics, this.HandleMetrics)
            .WithName("_bdk.Dashboard.Metrics")
            .WithSummary("Dashboard Metrics")
            .WithDescription("Shows the dashboard metrics page.")
            .Produces<string>((int)HttpStatusCode.OK);
    }

    private Task<IResult> HandleIndex()
    {
        return Task.FromResult(
            Results.RazorSlice<Pages.DashboardIndex>());
    }

    private Task<IResult> HandleMetrics()
    {
        return Task.FromResult(
            Results.RazorSlice<Pages.DashboardMetrics, DashboardMetricsViewModel>(new DashboardMetricsViewModel()));
    }

    //return Results.RazorSlice<Signin, SigninViewModel>(new SigninViewModel { Request = request, Options = options });
}