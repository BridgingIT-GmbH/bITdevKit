// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides extension methods for configuring application-specific middlewares.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the application to use the ResultLogger for logging results.
    /// </summary>
    /// <param name="app">The application builder to configure.</param>
    /// <returns>The configured application builder.</returns>
    public static IApplicationBuilder UseResultLogger(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetService<ILogger<ResultLogger>>();
        Result.Setup(s => s.Logger = new ResultLogger(logger));

        return app;
    }

    /// <summary>
    /// Configures the application to use the RuleLogger middleware, setting up logging for rules.
    /// </summary>
    /// <param name="app">The IApplicationBuilder instance to configure.</param>
    /// <returns>The IApplicationBuilder instance for further configuration.</returns>
    public static IApplicationBuilder UseRuleLogger(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetService<ILogger<RuleLogger>>();
        Rule.Setup(s => s.SetLogger(new RuleLogger(logger)));

        return app;
    }
}