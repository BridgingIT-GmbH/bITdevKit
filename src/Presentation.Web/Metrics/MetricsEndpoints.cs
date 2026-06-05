// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Maps the system metrics discovery and snapshot endpoints.
/// </summary>
/// <example>
/// <code>
/// services.AddMetrics(options => options
///     .Enabled(true)
///     .AddEndpoints(true));
/// </code>
/// </example>
public class MetricsEndpoints(
    MetricsEndpointsOptions options = null,
    IMetricsSnapshotService bdkSnapshotService = null,
    IDotNetMetricsSnapshotService dotNetSnapshotService = null,
    IAspNetMetricsSnapshotService aspNetSnapshotService = null,
    ILogger<MetricsEndpoints> logger = null) : EndpointsBase
{
    private readonly MetricsEndpointsOptions options = options ?? new MetricsEndpointsOptions();
    private readonly IMetricsSnapshotService bdkSnapshotService = bdkSnapshotService;
    private readonly IDotNetMetricsSnapshotService dotNetSnapshotService = dotNetSnapshotService;
    private readonly IAspNetMetricsSnapshotService aspNetSnapshotService = aspNetSnapshotService;
    private readonly ILogger<MetricsEndpoints> logger = logger;

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options)
            .WithTags("_bdk.Metrics");

        group.MapGet(string.Empty, this.GetMetrics)
            .WithName("_bdk.Metrics.Get")
            .Produces<Dictionary<string, string>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        if (this.options.BdkEnabled)
        {
            group.MapGet("bdk", this.GetBdkMetrics)
                .WithName("_bdk.Metrics.GetBdk")
                .Produces<MetricsSnapshotModel>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .Produces<ProblemDetails>((int)HttpStatusCode.ServiceUnavailable);
        }

        if (this.options.DotNetEnabled)
        {
            group.MapGet("dotnet", this.GetDotNetMetrics)
                .WithName("_bdk.Metrics.GetDotNet")
                .Produces<DotNetMetricsSnapshotModel>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .Produces<ProblemDetails>((int)HttpStatusCode.ServiceUnavailable);
        }

        if (this.options.AspNetEnabled)
        {
            group.MapGet("aspnet", this.GetAspNetMetrics)
                .WithName("_bdk.Metrics.GetAspNet")
                .Produces<AspNetMetricsSnapshotModel>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .Produces<ProblemDetails>((int)HttpStatusCode.ServiceUnavailable);

            group.MapGet("aspnet/routes", this.GetAspNetRouteMetrics)
                .WithName("_bdk.Metrics.GetAspNetRoutes")
                .Produces<AspNetRouteMetricsSnapshotModel>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .Produces<ProblemDetails>((int)HttpStatusCode.ServiceUnavailable);
        }

        if (this.options.OverviewEnabled)
        {
            group.MapGet("overview", this.GetOverview)
                .WithName("_bdk.Metrics.GetOverview")
                .Produces<MetricsOverviewSnapshotModel>()
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .Produces<ProblemDetails>((int)HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <summary>
    /// Returns the metrics endpoint discovery document.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The discovery response.</returns>
    public IResult GetMetrics(HttpContext httpContext)
    {
        var host = $"{httpContext.Request.Scheme}://{httpContext.Request.Host.Value.Trim('/')}";
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (this.options.BdkEnabled)
        {
            result["bdk"] = $"{host}/{this.options.GroupPath.Trim('/')}/bdk";
        }

        if (this.options.OverviewEnabled)
        {
            result["overview"] = $"{host}/{this.options.GroupPath.Trim('/')}/overview";
        }

        if (this.options.DotNetEnabled)
        {
            result["dotnet"] = $"{host}/{this.options.GroupPath.Trim('/')}/dotnet";
        }

        if (this.options.AspNetEnabled)
        {
            result["aspnet"] = $"{host}/{this.options.GroupPath.Trim('/')}/aspnet";
            result["aspnetRoutes"] = $"{host}/{this.options.GroupPath.Trim('/')}/aspnet/routes";
        }

        return Results.Ok(result);
    }

    /// <summary>
    /// Returns the current devkit metrics snapshot.
    /// </summary>
    /// <returns>The snapshot response.</returns>
    public IResult GetBdkMetrics()
    {
        if (this.bdkSnapshotService is null)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.ServiceUnavailable,
                Title = "Metrics Service Unavailable",
                Detail = "The devkit metrics snapshot service is not registered."
            });
        }

        try
        {
            return Results.Ok(this.bdkSnapshotService.GetSnapshot());
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to retrieve devkit metrics snapshot.");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Metrics Retrieval Failed",
                Detail = "An error occurred while retrieving the devkit metrics snapshot."
            });
        }
    }

    /// <summary>
    /// Returns the current .NET runtime metrics snapshot.
    /// </summary>
    /// <returns>The snapshot response.</returns>
    public IResult GetDotNetMetrics()
    {
        if (this.dotNetSnapshotService is null)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.ServiceUnavailable,
                Title = "Metrics Service Unavailable",
                Detail = "The .NET metrics snapshot service is not registered."
            });
        }

        try
        {
            return Results.Ok(this.dotNetSnapshotService.GetSnapshot());
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to retrieve .NET metrics snapshot.");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = ".NET Metrics Retrieval Failed",
                Detail = "An error occurred while retrieving the .NET metrics snapshot."
            });
        }
    }

    /// <summary>
    /// Returns the current ASP.NET metrics snapshot.
    /// </summary>
    /// <returns>The snapshot response.</returns>
    public IResult GetAspNetMetrics()
    {
        if (this.aspNetSnapshotService is null)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.ServiceUnavailable,
                Title = "Metrics Service Unavailable",
                Detail = "The ASP.NET metrics snapshot service is not registered."
            });
        }

        try
        {
            return Results.Ok(this.aspNetSnapshotService.GetSnapshot());
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to retrieve ASP.NET metrics snapshot.");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "ASP.NET Metrics Retrieval Failed",
                Detail = "An error occurred while retrieving the ASP.NET metrics snapshot."
            });
        }
    }

    /// <summary>
    /// Returns the current ASP.NET route metrics snapshot.
    /// </summary>
    /// <returns>The snapshot response.</returns>
    public IResult GetAspNetRouteMetrics()
    {
        if (this.aspNetSnapshotService is null)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.ServiceUnavailable,
                Title = "Metrics Service Unavailable",
                Detail = "The ASP.NET metrics snapshot service is not registered."
            });
        }

        try
        {
            return Results.Ok(this.aspNetSnapshotService.GetRouteSnapshot());
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to retrieve ASP.NET route metrics snapshot.");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "ASP.NET Route Metrics Retrieval Failed",
                Detail = "An error occurred while retrieving the ASP.NET route metrics snapshot."
            });
        }
    }

    /// <summary>
    /// Returns the dashboard-oriented overview projection of the devkit metrics snapshot.
    /// </summary>
    /// <returns>The overview response.</returns>
    public IResult GetOverview()
    {
        if (this.bdkSnapshotService is null)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.ServiceUnavailable,
                Title = "Metrics Service Unavailable",
                Detail = "The devkit metrics snapshot service is not registered."
            });
        }

        try
        {
            var snapshot = this.bdkSnapshotService.GetSnapshot();
            var overview = new MetricsOverviewSnapshotModel
            {
                CapturedAtUtc = snapshot.CapturedAtUtc,
                ProcessStartedAtUtc = snapshot.ProcessStartedAtUtc,
                UptimeSeconds = snapshot.UptimeSeconds,
                TopFailures = snapshot.TopFailures,
                TopThroughput = snapshot.TopThroughput,
                TopCurrent = snapshot.TopCurrent,
                LatencyHighlights = snapshot.LatencyHighlights,
                SummaryCards = new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["totalSuccess"] = snapshot.Features.Values.Sum(feature => feature.SuccessTotal),
                    ["totalFailure"] = snapshot.Features.Values.Sum(feature => feature.FailureTotal),
                    ["totalCurrent"] = snapshot.Features.Values.Sum(feature => feature.CurrentTotal),
                    ["featureCount"] = snapshot.Features.Count,
                    ["latencySeriesCount"] = snapshot.Features.Values.Sum(feature => feature.Durations.Count)
                }
            };

            foreach (var feature in snapshot.Features.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                overview.Features[feature.Key] = new MetricsOverviewFeatureModel
                {
                    Name = feature.Value.Name,
                    SuccessTotal = feature.Value.SuccessTotal,
                    FailureTotal = feature.Value.FailureTotal,
                    CurrentTotal = feature.Value.CurrentTotal,
                    TopThroughput = feature.Value.TopThroughput.FirstOrDefault(),
                    TopCurrent = feature.Value.TopCurrent.FirstOrDefault(),
                    SlowestSeries = feature.Value.LatencyHighlights.FirstOrDefault()
                };
            }

            return Results.Ok(overview);
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to retrieve metrics overview snapshot.");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Metrics Overview Failed",
                Detail = "An error occurred while retrieving the metrics overview snapshot."
            });
        }
    }
}
