// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

/// <summary>Shared request path extraction and immutable logging/auditing blacklist defaults.</summary>
/// <example><code>var excluded = RequestPathDefaults.GetPath(context).MatchAny(RequestPathDefaults.Blacklist);</code></example>
public static class RequestPathDefaults
{
    /// <summary>Default whole-path patterns; each configured feature copies this immutable collection.</summary>
    /// <example><code>var patterns = RequestPathDefaults.Blacklist.ToArray();</code></example>
    public static ImmutableArray<string> Blacklist { get; } =
    [
        "/*.js", "/*.css", "/*.map", "/*.html", "/swagger*", "/favicon.ico", "/_framework*", "/_vs*", "/health*",
        "/notificationhub*", "/_content*", "/signalrhub*", "/_bdk/*"
    ];

    /// <summary>Reads the original request feature path with the established HttpRequest.Path fallback.</summary>
    /// <example><code>var path = RequestPathDefaults.GetPath(context);</code></example>
    public static string GetPath(HttpContext context, bool includeQuery = false)
    {
        var feature = context.Features.Get<IHttpRequestFeature>();
        var path = includeQuery ? feature?.RawTarget : feature?.Path;
        return string.IsNullOrEmpty(path) ? context.Request.Path.ToString() : path;
    }
}
