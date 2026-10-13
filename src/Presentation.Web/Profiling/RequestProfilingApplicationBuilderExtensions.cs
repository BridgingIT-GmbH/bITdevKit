// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Builder;

using BridgingIT.DevKit.Presentation;

/// <summary>Installs the HTTP adapter's outer observation and inner exception reporting boundaries.</summary>
/// <example><code>app.UseRequestProfiling().UseExceptionHandler().UseRequestProfilingExceptionObserver();</code></example>
public static class RequestProfilingApplicationBuilderExtensions
{
    /// <summary>Observes downstream request work; install before host exception handling, routing and response middleware.</summary>
    /// <example><code>app.UseRequestProfiling(); app.UseExceptionHandler(); app.UseRequestProfilingExceptionObserver();</code></example>
    public static IApplicationBuilder UseRequestProfiling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<RequestProfilingMiddleware>();
    }

    /// <summary>Reports failures immediately inside host error handling and before protected routing/endpoints.</summary>
    /// <example><code>app.UseExceptionHandler(); app.UseRequestProfilingExceptionObserver(); app.UseRouting();</code></example>
    public static IApplicationBuilder UseRequestProfilingExceptionObserver(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<RequestProfilingExceptionObserverMiddleware>();
    }
}
