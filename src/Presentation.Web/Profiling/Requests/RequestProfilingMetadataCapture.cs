// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Text;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Captures bounded request metadata without reading bodies or retaining authentication headers.</summary>
/// <example><code>var metadata = RequestProfilingMetadataCapture.Capture(context.Request, options);</code></example>
public static class RequestProfilingMetadataCapture
{
    /// <summary>Captures the original request target with bounded, redacted query arguments.</summary>
    /// <example><code>var metadata = RequestProfilingMetadataCapture.Capture(request, new RequestProfilingOptions());</code></example>
    public static HttpRequestProfilingMetadata Capture(HttpRequest request, RequestProfilingOptions options)
    {
        var raw = options.CaptureQueryString ? request.QueryString.Value ?? "" : "";
        var truncated = raw.Length > options.MaximumQueryStringLength;
        raw = raw[..Math.Min(raw.Length, options.MaximumQueryStringLength)];
        var query = new StringBuilder(raw.Length);
        foreach (var argument in raw.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = argument.IndexOf('=');
            var encodedName = separator < 0 ? argument : argument[..separator];
            var name = Uri.UnescapeDataString(encodedName.Replace('+', ' '));
            var redacted = options.RedactedQueryParameters.Contains(name, StringComparer.OrdinalIgnoreCase);
            query.Append(query.Length == 0 ? '?' : '&').Append(redacted ? encodedName + "=%5Bredacted%5D" : argument);
        }

        truncated |= query.Length > options.MaximumQueryStringLength;
        return new()
        {
            Method = request.Method, Path = request.PathBase.Add(request.Path).Value,
            QueryString = options.CaptureQueryString ? query.ToString(0, Math.Min(query.Length, options.MaximumQueryStringLength)) : null,
            QueryStringTruncated = truncated, Scheme = request.Scheme, Host = request.Host.Value,
            Protocol = request.Protocol, RequestContentType = request.ContentType,
        };
    }
}
