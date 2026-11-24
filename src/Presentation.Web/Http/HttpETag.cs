// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Transports opaque version tokens through HTTP entity-tag headers without changing their quoting.</summary>
/// <example><code>var condition = HttpETag.ReadCondition(request.Headers.IfMatch);</code></example>
public static class HttpETag
{
    /// <summary>Reads one optional version condition and rejects conflicting header/body conditions and wildcards.</summary>
    /// <example><code>var condition = HttpETag.ReadCondition(header, model.IfMatchETag);</code></example>
    public static Result<string> ReadCondition(string header, string bodyETag = null) =>
        OpaqueVersionETag.ReadCondition(header, bodyETag);

    /// <summary>Writes a strong HTTP transport token representing the exact version token.</summary>
    /// <example><code>HttpETag.Write(context.Response, info.ETag);</code></example>
    public static void Write(HttpResponse response, string etag) =>
        OpaqueVersionETag.Write(response, etag);
}
