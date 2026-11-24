// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.AspNetCore.WebUtilities;

/// <summary>Creates opaque strong validators for one immutable version and fixed public encoded representation.</summary>
/// <example><code>var tag = CacheEntityTag.Create(version, representationId);</code></example>
public static class CacheEntityTag
{
    /// <summary>Creates a quoted HTTP validator without disclosing the representation descriptor.</summary>
    public static string Create(CacheEntryVersion version, string representationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(representationId);
        var representation = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(representationId))).Substring(0, 24);
        return "\"" + WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(version.Value)) + "." + representation + "\"";
    }

    /// <summary>Decodes only a strong validator for the exact fixed public representation.</summary>
    public static bool TryGetVersion(string tag, string representationId, out CacheEntryVersion version)
    {
        version = default;
        if (tag is null || tag.Length is < 4 or > 2048 || !tag.StartsWith('"') || !tag.EndsWith('"')) { return false; }

        var parts = tag[1..^1].Split('.');
        if (parts.Length != 2) { return false; }

        try
        {
            var value = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parts[0]));
            if (value.Length is < 1 or > 512 || value.Any(char.IsControl)) { return false; }

            version = new(value);
            return StringComparer.Ordinal.Equals(tag, Create(version, representationId));
        }
        catch (FormatException) { return false; }
    }

    /// <summary>Tests a comma-separated validator set using strong or GET-compatible weak comparison.</summary>
    /// <example><code>var matches = CacheEntityTag.Matches(request.Headers.IfNoneMatch, entityTag, weak: true);</code></example>
    public static bool Matches(string values, string current, bool weak) => HttpRepresentationConditions.MatchesEntityTag(values, current, weak);
}