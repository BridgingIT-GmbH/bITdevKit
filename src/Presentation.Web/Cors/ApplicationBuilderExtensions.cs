// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Extension methods for configuring CORS middleware.
/// </summary>
[ExcludeFromCodeCoverage]
public static partial class ApplicationBuilderExtensions
{
    /// <summary>
    /// Use CORS middleware if enabled in configuration.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The web application for chaining.</returns>
    /// <remarks>
    /// Conditionally applies CORS middleware based on Cors:Enabled configuration.
    /// If a default policy is specified, it is applied globally; otherwise, CORS is applied per-endpoint via [EnableCors] attributes.
    /// <para>
    /// IMPORTANT: Must be called after UseRouting() and before UseAuthorization() in the middleware pipeline.
    /// </para>
    /// <para>
    /// Example usage in Program.cs:
    /// <code>
    /// app.UseRouting();
    /// app.UseCors(builder.Configuration);
    /// app.UseAuthorization();
    /// </code>
    /// </para>
    /// <para>
    /// When CORS is disabled, this method does nothing and cross-origin requests will be blocked by browsers.
    /// </para>
    /// </remarks>
    public static WebApplication UseCors(this WebApplication app, IConfiguration configuration)
    {
        var corsEnabled = configuration.GetValue<bool>("Cors:Enabled");
        if (!corsEnabled)
        {
            return app; // CORS disabled, skip middleware
        }

        var corsConfig = configuration.GetSection("Cors").Get<CorsConfiguration>();
        var logger = app.Services.GetService<ILoggerFactory>()?.CreateLogger("CORS");

        var defaultPolicy = !string.IsNullOrWhiteSpace(corsConfig?.DefaultPolicy)
            ? corsConfig.DefaultPolicy
            : "endpoint-level";

        logger?.LogInformation("{LogKey} CORS enabled (policy={Policy}, policies=#{PolicyCount})",
            "REQ",
            defaultPolicy,
            corsConfig?.Policies?.Count ?? 0);

        app.UseCors();

        return app;
    }
}
