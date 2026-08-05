// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Broadcasting.Dashboard;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Maps the Broadcasting operational dashboard and its test-publish action.</summary>
/// <param name="options">The shared dashboard endpoint options.</param>
/// <param name="logger">The logger used for safe dashboard failure reporting.</param>
/// <example>
/// <code>
/// services.AddDashboard(options =>
///     options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(
    DashboardEndpointsOptions options,
    ILogger<DashboardEndpoints> logger = null
) : EndpointsBase, IDashboardEndpoints
{
    private const string BroadcastingPath = "/broadcasting";
    private const string BroadcastingContentPath = "/broadcasting/content";
    private const string BroadcastingPublishPath = "/broadcasting/publish";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();
        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options).WithTags("_bdk.Dashboard");
        group.MapDashboardPage<Pages.Index>(
            BroadcastingPath,
            "_bdk.Dashboard.Broadcasting",
            "Dashboard Broadcasting",
            "Shows broadcast scopes, node registrations, and delivery diagnostics."
        );
        group.MapDashboardPage<Pages.Content>(
            BroadcastingContentPath,
            "_bdk.Dashboard.BroadcastingContent",
            "Dashboard Broadcasting Content",
            "Shows the refreshable Broadcasting operational content."
        );
        group
            .MapPost(
                BroadcastingPublishPath,
                (Func<HttpContext, Task<IResult>>)this.PublishProbeAsync
            )
            .WithName("_bdk.Dashboard.BroadcastingPublish")
            .WithSummary("Publish a Broadcasting delivery probe")
            .WithDescription(
                "Publishes the built-in no-op probe to every active node in the default scope."
            )
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .DisableAntiforgery();
    }

    /// <summary>Builds the absolute Broadcasting dashboard page path.</summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard page path.</returns>
    /// <example><code>var path = DashboardEndpoints.BuildBroadcastingPath(options);</code></example>
    public static string BuildBroadcastingPath(DashboardEndpointsOptions options) =>
        DashboardPath.Combine(options?.GroupPath, BroadcastingPath);

    /// <summary>Builds the absolute Broadcasting content-fragment path.</summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute dashboard content path.</returns>
    /// <example><code>var path = DashboardEndpoints.BuildBroadcastingContentPath(options);</code></example>
    public static string BuildBroadcastingContentPath(DashboardEndpointsOptions options) =>
        DashboardPath.Combine(options?.GroupPath, BroadcastingContentPath);

    /// <summary>Builds the absolute Broadcasting probe-publish path.</summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The absolute probe-publish path.</returns>
    /// <example><code>var path = DashboardEndpoints.BuildBroadcastingPublishPath(options);</code></example>
    public static string BuildBroadcastingPublishPath(DashboardEndpointsOptions options) =>
        DashboardPath.Combine(options?.GroupPath, BroadcastingPublishPath);

    private async Task<IResult> PublishProbeAsync(HttpContext httpContext)
    {
        var service = httpContext.RequestServices.GetService<IBroadcastService>();
        if (service is null)
        {
            return Results.Problem(
                "Broadcasting is not registered.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        try
        {
            var result = await service
                .PublishAsync(
                    new BroadcastProbe(Guid.NewGuid(), DateTimeOffset.UtcNow),
                    targetScopes: null,
                    new BroadcastPublishOptions { RequireAtLeastOneTarget = true },
                    httpContext.RequestAborted
                )
                .ConfigureAwait(false);
            if (result.IsFailure)
            {
                return Results.Problem(
                    GetFailureMessage(result),
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Results.Ok(
                new
                {
                    broadcastId = result.Value.BroadcastId,
                    scopes = result.Value.TargetScopes,
                    targetCount = result.Value.TargetCount,
                    acceptedCount = result.Value.AcceptedCount,
                    failureCount = result.Value.FailureCount,
                    outcomes = result.Value.Nodes.Take(20).Select(node => new
                    {
                        nodeIdentity = node.NodeIdentity,
                        outcome = node.Outcome.ToString(),
                    }),
                }
            );
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger?.LogError(
                exception,
                "[UTL] dashboard probe publication failed (scope={Scope})",
                BroadcastingOptions.DefaultScope
            );
            return Results.Problem(
                "The test broadcast could not be published.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private static string GetFailureMessage(Result<BroadcastResult> result)
    {
        var message = string.Join(
            " ",
            result.Errors.Select(error => error.Message).Where(value =>
                !string.IsNullOrWhiteSpace(value)
            )
        );
        return string.IsNullOrWhiteSpace(message)
            ? "The test broadcast failed."
            : message;
    }
}