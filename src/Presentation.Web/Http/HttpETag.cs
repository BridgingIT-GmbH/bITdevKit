// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web;

using System.Text;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Transports opaque version tokens through HTTP entity-tag headers without changing their quoting.</summary>
/// <example><code>var condition = HttpETag.ReadCondition(request.Headers.IfMatch);</code></example>
public static class HttpETag
{
    private const string Prefix = "\"bdk.";

    /// <summary>Reads one optional version condition and rejects conflicting header/body conditions and wildcards.</summary>
    /// <example><code>var condition = HttpETag.ReadCondition(header, model.IfMatchETag);</code></example>
    public static Result<string> ReadCondition(string header, string bodyETag = null)
    {
        try
        {
            var supplied = Decode(header);
            var body = Decode(bodyETag);
            if (supplied is not null && body is not null && !string.Equals(supplied, body, StringComparison.Ordinal))
            {
                return Result<string>.Failure(new ValidationError("If-Match and the body ETag must identify the same version."));
            }

            return Result<string>.Success(supplied ?? body);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return Result<string>.Failure(new ValidationError("Supply one concrete ETag returned by this API; wildcards and ETag lists are unsupported."));
        }
    }

    /// <summary>Writes a strong HTTP transport token representing the exact version token.</summary>
    /// <example><code>HttpETag.Write(context.Response, info.ETag);</code></example>
    public static void Write(HttpResponse response, string etag)
    {
        if (!string.IsNullOrWhiteSpace(etag))
        {
            response.Headers.ETag = Prefix + Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(etag)) + "\"";
        }
    }

    private static string Decode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Contains(',') || value.Any(char.IsControl)) throw new FormatException("A single ETag is required.");
        if (value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            if (!value.EndsWith('"')) throw new FormatException("An encoded ETag must be quoted.");
            value = new UTF8Encoding(false, true).GetString(Base64UrlHelper.Decode(value[Prefix.Length..^1]));
        }

        if (string.IsNullOrWhiteSpace(value) || value.Trim('"') == "*" || value.Any(char.IsControl))
        {
            throw new FormatException("A concrete version is required.");
        }

        return value;
    }
}
