// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Provides request correlation accessors for ASP.NET Core HTTP contexts.
/// </summary>
/// <example><code>var correlationId = httpContext.TryGetCorrelationId();</code></example>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the correlation identifier established for the current HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The correlation identifier, or <see langword="null"/> when none is available.</returns>
    /// <example><code>var correlationId = httpContext.TryGetCorrelationId();</code></example>
    public static string TryGetCorrelationId(this HttpContext context)
    {
        return context.Items.TryGetValue(CorrelationId.HeaderName, out var id)
            ? id.ToString()
            : null;
    }
}
