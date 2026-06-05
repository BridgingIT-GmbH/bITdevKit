// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IResult = Microsoft.AspNetCore.Http.IResult;

public class DashboardEndpoints(
    //ILogger<DashboardEndpoints> logger,
    DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
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

        group.MapGet("/", this.HandleIndex)
            .WithName("_bdk.Dashboard.Index")
            .WithSummary("Dashboard Index")
            .WithDescription("Shows the dashboard index page.")
            .Produces<string>((int)HttpStatusCode.OK);

        group.MapGet(options.EndpointPaths.IndexContent, this.HandleIndexContent)
            .WithName("_bdk.Dashboard.IndexContent")
            .WithSummary("Dashboard Index Content")
            .WithDescription("Shows the dashboard index card content fragment.")
            .Produces<string>((int)HttpStatusCode.OK);
    }

    private Task<IResult> HandleIndex()
    {
        return Task.FromResult(
            Results.RazorSlice<Pages.DashboardIndex>());
    }

    private Task<IResult> HandleIndexContent()
    {
        return Task.FromResult(
            Results.RazorSlice<Pages.DashboardIndexContent>());
    }
}
