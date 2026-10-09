// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;
using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using IDatabaseReadyService = BridgingIT.DevKit.Common.IDatabaseReadyService;

/// <summary>
/// Defines the WeatherFiesta Core module dashboard pages.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;CoreDashboard&gt;());
/// </code>
/// </example>
public sealed class CoreDashboard(DashboardEndpointsOptions options) : DashboardPageSet(options)
{
    /// <inheritdoc />
    protected override void Configure(DashboardPageSetBuilder pages)
    {
        pages.WithTags("_bdk.Dashboard.WeatherFiesta.Core");

        pages.Group("Core", 100)
            .Page("core-overview", "/app/core")
                .Title("Overview")
                .Icon("cloud-sun")
                .Order(0)
                .Description("WeatherFiesta Core module overview")
                .Razor<Pages.Overview>()
                .Content<Pages.OverviewContent>()
                .Card(GetOverviewCardAsync)
            .Page("city-management", "/app/core/cities")
                .Title("City Management")
                .Icon("building-add")
                .Order(10)
                .Description("Search, add, and review WeatherFiesta cities")
                .Razor<Pages.CityManagement>()
                .Content<Pages.CityManagementContent>()
                .HideFromIndex()
                .Get("/suggestions", SearchCitySuggestionsAsync)
                    .Name("_bdk.Dashboard.WeatherFiesta.Core.CitySuggestions")
                .Post("/add", AddCityAsync)
                    .Name("_bdk.Dashboard.WeatherFiesta.Core.CityAdd")
            .Page("core-profiling", "/app/core/profiling")
                .Title("Profiling Lab")
                .Icon("speedometer2")
                .Order(20)
                .Description("Run stored-weather reviews and relate operations to Runtime observations")
                .Razor<Pages.ProfilingLab>()
                .Content<Pages.ProfilingLabContent>()
                .Post("/review", ReviewCitiesAsync)
                    .Name("_bdk.Dashboard.WeatherFiesta.Core.ProfilingReview")
                .Post("/jobs/{scenario}", DispatchProfilingJobAsync)
                    .Name("_bdk.Dashboard.WeatherFiesta.Core.ProfilingJob")
            .Page("core-api-requests", "/app/core/api-requests")
                .Title("API Requests")
                .Icon("send")
                .Order(30)
                .Description("Send bounded HTTP requests to the cities and weather API")
                .Razor<Pages.ApiRequests>()
                .Get("/http-access", GetHttpAccessAsync)
                    .Name("_bdk.Dashboard.WeatherFiesta.Core.ApiRequestAccess");
    }

    private static async Task<IResult> GetHttpAccessAsync(
        [FromServices] DashboardEndpointsOptions options,
        HttpContext context)
    {
        var scheme = options.Authentication.Kind == DashboardAuthenticationRegistrationKind.OpenIdConnect
            ? options.Authentication.CookieScheme
            : options.RequireAuthenticationSchemes.FirstOrDefault();
        var authentication = await context.AuthenticateAsync(scheme);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        return Results.Ok(new { accessToken = authentication.Succeeded ? authentication.Properties?.GetTokenValue("access_token") : null });
    }

    private static async Task<IResult> ReviewCitiesAsync(
        [FromServices] WeatherCityReviewService reviews,
        [FromBody] WeatherProfilingReviewRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // This explicit workload is independent of the excluded dashboard HTTP request.
        using var boundary = context.RequestServices.GetService<IOperationProfiler>().BeginSafeExecutionBoundary();
        return (await reviews.ReviewAsync(request.CityLimit, request.Repetitions, "dashboard", cancellationToken)).MapHttpOk();
    }

    private static async Task<IResult> DispatchProfilingJobAsync(
        [FromServices] IJobSchedulerService scheduler,
        [FromRoute] string scenario,
        CancellationToken cancellationToken
    )
    {
        var jobName = scenario switch
        {
            "stress" => "core_profiling_stress",
            "orchestration" => "core_hello_world_orchestration",
            _ => null,
        };
        return jobName is null
            ? Results.BadRequest("Unknown profiling scenario.")
            : (
                await scheduler.DispatchAsync(jobName, cancellationToken: cancellationToken)
            ).MapHttpOk();
    }

    private static async ValueTask<DashboardPageCard> GetOverviewCardAsync(DashboardPageCardContext card)
    {
        var databaseReadyService = card.HttpContext.RequestServices.GetService<IDatabaseReadyService>();
        if (databaseReadyService?.IsReady(nameof(CoreDbContext)) == false)
        {
            return card.Unavailable("Database starting");
        }

        var requester = card.HttpContext.RequestServices.GetService<IRequester>();
        if (requester is null)
        {
            return card.Unavailable("Requester unavailable");
        }

        var cities = await requester.SendAsync(new AdminCitiesQuery(), cancellationToken: card.HttpContext.RequestAborted);
        return cities.IsSuccess
            ? card.Value(
                cities.Value.Count.ToString("N0", CultureInfo.InvariantCulture),
                $"{cities.Value.Sum(city => city.SubscriptionCount).ToString("N0", CultureInfo.InvariantCulture)} subscriptions",
                "WeatherFiesta")
            : card.Error("Could not load cities");
    }

    private static async Task<IResult> SearchCitySuggestionsAsync(
        [FromServices] IRequester requester,
        [FromQuery] string search,
        [FromQuery] string countryCode,
        CancellationToken cancellationToken)
    {
        return (await requester.SendAsync(
                new CitySuggestionQuery(search, countryCode),
                cancellationToken: cancellationToken))
            .MapHttpOk();
    }

    private static async Task<IResult> AddCityAsync(
        [FromServices] IRequester requester,
        [FromBody] AdminCityCreateModel model,
        CancellationToken cancellationToken)
    {
        if (model.ExternalId.HasValue)
        {
            var citiesResult = await requester.SendAsync(new AdminCitiesQuery(), cancellationToken: cancellationToken);
            if (citiesResult.IsSuccess)
            {
                var existingCity = citiesResult.Value.FirstOrDefault(city => city.ExternalId == model.ExternalId);
                if (existingCity is not null)
                {
                    return Results.Ok(existingCity);
                }
            }
        }

        return (await requester.SendAsync(
                new AdminCityCreateCommand { Model = model },
                cancellationToken: cancellationToken))
            .MapHttpCreated(value => $"/api/core/cities/{value.Id}");
    }
}
