// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Represents dashboard endpoints.
/// </summary>
/// <param name="options">The options controlling the operation.</param>
public class DashboardEndpoints(
    //ILogger<DashboardEndpoints> logger,
    DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    /// <inheritdoc/>
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

        group.MapGet("/", (Delegate)this.HandleIndex)
            .WithName("_bdk.Dashboard.Index")
            .WithSummary("Dashboard Index")
            .WithDescription("Shows the dashboard index page.")
            .Produces<string>();

        group.MapGet(options.EndpointPaths.IndexContent, (Delegate)this.HandleIndexContent)
            .WithName("_bdk.Dashboard.IndexContent")
            .WithSummary("Dashboard Index Content")
            .WithDescription("Shows the dashboard index card content fragment.")
            .Produces<string>();

        group.MapGet(options.EndpointPaths.AccessDenied, this.HandleAccessDenied)
            .WithName("_bdk.Dashboard.AccessDenied")
            .WithSummary("Dashboard Access Denied")
            .WithDescription("Shows the dashboard access denied page.")
            .Produces<string>((int)HttpStatusCode.Forbidden)
            .ExcludeFromDescription()
            .AllowAnonymous();

        group.MapPost(options.EndpointPaths.SignOut, this.HandleSignOut)
            .WithName("_bdk.Dashboard.SignOut")
            .WithSummary("Dashboard Sign Out")
            .WithDescription("Signs the current principal out of the dashboard authentication scheme.")
            .Produces((int)HttpStatusCode.Redirect)
            .DisableAntiforgery()
            .ExcludeFromDescription()
            .AllowAnonymous();

        group.MapDashboardPage<Pages.SystemOverview>(
            options.EndpointPaths.System,
            "_bdk.Dashboard.System",
            "Dashboard System",
            "Shows the dashboard system performance overview.");

        group.MapDashboardPage<Pages.SystemOverviewContent>(
            options.EndpointPaths.SystemContent,
            "_bdk.Dashboard.SystemContent",
            "Dashboard System Content",
            "Shows the dashboard system performance content fragment.");

        group.MapDashboardPage<Pages.SystemDiagnostics>(
            options.EndpointPaths.SystemDiagnostics,
            "_bdk.Dashboard.SystemDiagnostics", "System Diagnostics", "Shows runtime and HTTP diagnostic charts.");
        var systemHistory = new DashboardSystemHistory(
            app.ServiceProvider.GetService<IDotNetMetricsSnapshotService>(),
            app.ServiceProvider.GetService<IAspNetMetricsSnapshotService>(),
            logger: app.ServiceProvider.GetService<ILogger<DashboardSystemHistory>>());
        app.ServiceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.Register(systemHistory.Dispose);
        group.MapGet(options.EndpointPaths.SystemDiagnosticsData, () => Results.Json(systemHistory.Snapshot()))
            .WithName("_bdk.Dashboard.SystemDiagnosticsData").ExcludeFromDescription();

        group.MapDashboardPage<Pages.SystemProcess>(
            options.EndpointPaths.SystemProcess,
            "_bdk.Dashboard.SystemProcess",
            "Dashboard System Process",
            "Shows the dashboard system process, runtime, server and application details.");

        group.MapDashboardPage<Pages.SystemEnvironment>(
            options.EndpointPaths.SystemEnvironment,
            "_bdk.Dashboard.SystemEnvironment",
            "Dashboard System Environment",
            "Shows the dashboard system environment, worktree and environment variables.");
    }

    private async Task<IResult> HandleIndex(HttpContext httpContext)
    {
        var model = new DashboardIndexModel(await httpContext.GetDashboardPageCardsAsync(), httpContext.Request.Query["group"]);
        return Results.RazorSlice<Pages.DashboardIndex, DashboardIndexModel>(model);
    }

    private async Task<IResult> HandleIndexContent(HttpContext httpContext)
    {
        var model = new DashboardIndexModel(await httpContext.GetDashboardPageCardsAsync(), httpContext.Request.Query["group"]);
        return Results.RazorSlice<Pages.DashboardIndexContent, DashboardIndexModel>(model);
    }

    private IResult HandleAccessDenied()
    {
        return Results.Extensions.DashboardRazorSlice(
            "/Dashboard/Pages/AccessDenied.cshtml",
            typeof(DashboardEndpoints).Assembly,
            StatusCodes.Status403Forbidden);
    }

    private IResult HandleSignOut(HttpContext httpContext, [FromForm] string returnUrl)
    {
        var redirectUrl = this.GetLocalDashboardReturnUrl(httpContext, returnUrl);
        if (!this.IsSignOutEnabled())
        {
            return Results.Redirect(redirectUrl);
        }

        var schemes = this.GetSignOutAuthenticationSchemes();
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return Results.SignOut(properties, schemes);
    }

    private bool IsSignOutEnabled()
    {
        return options.Authentication.Kind is not DashboardAuthenticationRegistrationKind.ExistingScheme ||
            options.Authentication.SignOutEnabled ||
            GetSchemes(options.SignOutAuthenticationSchemes).Length > 0;
    }

    private string[] GetSignOutAuthenticationSchemes()
    {
        var signOutSchemes = GetSchemes(options.SignOutAuthenticationSchemes);
        return signOutSchemes.Length > 0
            ? signOutSchemes
            : GetSchemes(options.RequireAuthenticationSchemes);
    }

    private string GetLocalDashboardReturnUrl(HttpContext httpContext, string returnUrl)
    {
        var fallback = DashboardPath.Combine(httpContext.Request.PathBase, options.GroupPath);
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !returnUrl.StartsWith("/", StringComparison.Ordinal) ||
            returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return fallback;
        }

        var dashboardBasePath = DashboardPath.Combine(httpContext.Request.PathBase, options.GroupPath);
        return returnUrl.StartsWith(dashboardBasePath, StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : fallback;
    }

    private static string[] GetSchemes(IEnumerable<string> schemes)
    {
        return (schemes ?? [])
            .Where(scheme => !string.IsNullOrWhiteSpace(scheme))
            .Select(scheme => scheme.Trim())
            .ToArray();
    }
}
