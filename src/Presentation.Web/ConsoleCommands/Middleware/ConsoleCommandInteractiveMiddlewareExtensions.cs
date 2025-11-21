// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

/// <summary>
/// Extensions for wiring a middleware that collects request latency for status metrics.
/// </summary>
public static class ConsoleCommandInteractiveMiddlewareExtensions
{
    /// <summary>
    /// Adds a middleware to collect latency and error statistics used by the status interactive command.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication UseConsoleCommandsInteractiveStats(this WebApplication app)
    {
        var stats = app.Services.GetService<ConsoleCommandInteractiveRuntimeStats>();
        if (stats == null)
        {
            return app;
        }

        app.Use(async (ctx, next) =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                System.Threading.Interlocked.Increment(ref stats.TotalRequests);
                await next();
                if (ctx.Response.StatusCode >= 500)
                {
                    System.Threading.Interlocked.Increment(ref stats.TotalFailures);
                }
            }
            finally
            {
                sw.Stop();
                System.Threading.Interlocked.Add(ref stats.TotalLatencyMs, sw.ElapsedMilliseconds);
            }
        });

        return app;
    }
}
