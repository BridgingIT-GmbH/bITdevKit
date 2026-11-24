// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Http;

/// <summary>Transports opaque provider version tokens in strong HTTP entity-tags.</summary>
/// <example>
/// <code>
/// OpaqueVersionETag.Write(response, "inspection-v1");
/// var condition = OpaqueVersionETag.ReadCondition(request.Headers.IfMatch);
/// </code>
/// </example>
public static class OpaqueVersionETag
{
    private const string Prefix = "\"bdk.";

    /// <summary>Reads a single version condition from an If-Match header or request body.</summary>
    /// <param name="header">An optional If-Match header value.</param>
    /// <param name="bodyETag">An optional raw version or transport tag supplied in the body.</param>
    /// <returns>
    /// The decoded version or the raw version with surrounding whitespace trimmed. When both inputs
    /// are omitted or blank, the successful value is null. Malformed transport tags,
    /// unsupported conditions, and conflicting values return a <see cref="ValidationError"/>.
    /// </returns>
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

    /// <summary>Sets a single strong ETag with the quoted <c>bdk.</c> Base64Url transport format.</summary>
    /// <param name="response">The response whose ETag header to set.</param>
    /// <param name="etag">The provider version to encode as UTF-8. A blank version leaves the header as it is.</param>
    /// <remarks>
    /// Exact round-trips through <see cref="ReadCondition"/> require a nonblank, well-formed UTF-16
    /// version free of control characters. A version that becomes <c>*</c> when its surrounding
    /// double quotes are trimmed is treated as a wildcard. UTF-8 encoding replaces unpaired surrogates.
    /// </remarks>
    public static void Write(HttpResponse response, string etag)
    {
        if (!string.IsNullOrWhiteSpace(etag))
        {
            response.Headers.ETag = Prefix + Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(etag)) + "\"";
        }
    }

    private static string Decode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (value.Contains(',') || value.Any(char.IsControl))
        {
            throw new FormatException("A single ETag is required.");
        }

        if (value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            if (!value.EndsWith('"'))
            {
                throw new FormatException("An encoded ETag must be quoted.");
            }

            value = new UTF8Encoding(false, true).GetString(Base64UrlHelper.Decode(value[Prefix.Length..^1]));
        }

        if (string.IsNullOrWhiteSpace(value) || value.Trim('"') == "*" || value.Any(char.IsControl))
        {
            throw new FormatException("A concrete version is required.");
        }

        return value;
    }
}
