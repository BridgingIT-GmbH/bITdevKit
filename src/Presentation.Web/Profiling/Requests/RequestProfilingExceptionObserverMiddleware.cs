// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.AspNetCore.Http;

/// <summary>Reports exceptions before the host handler consumes or re-executes them, preserving original routing.</summary>
/// <example><code>app.UseExceptionHandler(); app.UseRequestProfilingExceptionObserver(); app.UseRouting();</code></example>
public sealed class RequestProfilingExceptionObserverMiddleware(RequestDelegate next)
{
    /// <summary>Invokes downstream exactly once and rethrows the original exception with its stack unchanged.</summary>
    /// <example>Registered immediately inside the host's exception handler.</example>
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context).ConfigureAwait(false); }
        catch (Exception exception)
        {
            context.Features.Get<IRequestProfilingFeature>()?.ReportException(exception);
            throw;
        }
    }
}
