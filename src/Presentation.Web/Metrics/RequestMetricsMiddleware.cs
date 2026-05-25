// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Captures lightweight request metrics for the ASP.NET metrics endpoints.
/// </summary>
public class RequestMetricsMiddleware(
    RequestDelegate next,
    AspNetMetricsTracker tracker,
    MetricsEndpointsOptions options)
{
    private readonly RequestDelegate next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly AspNetMetricsTracker tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    private readonly MetricsEndpointsOptions options = options ?? new MetricsEndpointsOptions();
    private readonly string[] pathBlackListPatterns =
    [
        "/*.js", "/*.css", "/*.map", "/*.html", "/swagger*", "/favicon.ico", "/_framework*", "/_vs*", "/health*",
        "/notificationhub*", "/_content*", "/signalrhub*", "/api/_system/logentries*"
    ];

    public async Task Invoke(HttpContext context)
    {
        if (this.ShouldSkip(context))
        {
            await this.next(context);
            return;
        }

        this.tracker.BeginRequest();
        var stopwatch = Stopwatch.StartNew();
        var statusCode = StatusCodes.Status200OK;

        try
        {
            await this.next(context);
            statusCode = context.Response.StatusCode;
        }
        catch
        {
            statusCode = context.Response.StatusCode >= 400
                ? context.Response.StatusCode
                : StatusCodes.Status500InternalServerError;

            throw;
        }
        finally
        {
            stopwatch.Stop();
            this.tracker.CompleteRequest(
                context.Request.Method,
                GetRoute(context),
                statusCode,
                stopwatch.ElapsedMilliseconds,
                DateTimeOffset.UtcNow);
        }
    }

    private bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        return context.Request.Path.StartsWithSegments(this.options.GroupPath)
            || path.MatchAny(this.pathBlackListPatterns);
    }

    private static string GetRoute(HttpContext context)
    {
        if (context.GetEndpoint() is RouteEndpoint routeEndpoint
            && !string.IsNullOrWhiteSpace(routeEndpoint.RoutePattern.RawText))
        {
            return routeEndpoint.RoutePattern.RawText;
        }

        return context.Request.Path.HasValue ? context.Request.Path.Value : "/";
    }
}
