// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Builder;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds runtime metrics middleware to the application pipeline.
/// </summary>
public static class MetricsApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the devkit metrics middleware to the HTTP pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">Optional middleware configuration.</param>
    /// <returns>The application builder.</returns>
    /// <example>
    /// <code>
    /// app.UseRequestMetrics(options => options.RouteMetricsEnabled = true);
    /// </code>
    /// </example>
    public static IApplicationBuilder UseRequestMetrics(
        this IApplicationBuilder app,
        Action<MetricsMiddlewareOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var metricsOptions = app.ApplicationServices.GetService<MetricsOptions>();
        if (metricsOptions is { Enabled: false })
        {
            return app;
        }

        var options = new MetricsMiddlewareOptions();
        configure?.Invoke(options);

        if (!options.RouteMetricsEnabled)
        {
            return app;
        }

        if (app.ApplicationServices.GetService<AspNetMetricsTracker>() is null)
        {
            throw new InvalidOperationException(
                "ASP.NET metrics services are not registered. Call services.AddMetrics() or services.AddMetricsEndpoints(...) before app.UseRequestMetrics().");
        }

        var endpointOptions = app.ApplicationServices.GetService<MetricsEndpointsOptions>() ?? new MetricsEndpointsOptions();

        return app.UseMiddleware<RequestMetricsMiddleware>(endpointOptions);
    }

    /// <summary>
    /// Adds runtime metrics middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">Optional middleware configuration.</param>
    /// <returns>The application builder.</returns>
    [Obsolete("Use UseRequestMetrics() instead.")]
    public static IApplicationBuilder UseMetrics(
        this IApplicationBuilder app,
        Action<MetricsMiddlewareOptions> configure = null)
    {
        return app.UseRequestMetrics(configure);
    }
}
