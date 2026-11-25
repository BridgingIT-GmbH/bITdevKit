// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Common;

/// <summary>Matches request path exclusions and produces canonical keys for Request Profiling.</summary>
/// <seealso cref="PathGlobMatcher"/>
/// <example><code>var matcher = new RequestProfilingPathMatcher(["/health/**"]);</code></example>
public sealed class RequestProfilingPathMatcher
{
    private readonly PathGlobMatcher matcher;

    /// <summary>Validates and prepares at most 128 patterns, each at most 256 UTF-16 code units.</summary>
    /// <param name="patterns">The anchored request path patterns.</param>
    /// <exception cref="ArgumentNullException"><paramref name="patterns"/> is null.</exception>
    /// <exception cref="ArgumentException">The list exceeds its count limit or contains an invalid pattern.</exception>
    /// <example><code>var excluded = new RequestProfilingPathMatcher(["/swagger/**"]).IsMatch(path);</code></example>
    public RequestProfilingPathMatcher(IReadOnlyList<string> patterns)
    {
        this.matcher = new(patterns);
    }

    /// <summary>Matches the original incoming path before prefix stripping or key shortening.</summary>
    /// <param name="path">The incoming path. Null and empty inputs represent the root path.</param>
    /// <returns>True if any configured pattern matches.</returns>
    /// <example><code>if (matcher.IsMatch("/health/live")) ExcludeCapture();</code></example>
    public bool IsMatch(string path)
    {
        // The original request matcher skipped the first code unit even on unrooted input.
        return this.matcher.IsMatch(string.IsNullOrEmpty(path) || path[0] == '/'
            ? path
            : string.Concat("/", path.AsSpan(1)));
    }

    /// <summary>Strips one prefix on a path boundary and hashes overlong canonical keys.</summary>
    /// <param name="path">The incoming path, with null or empty representing the root.</param>
    /// <param name="prefix">The optional prefix to remove on a complete path boundary.</param>
    /// <param name="maximumLength">The positive maximum key length in UTF-16 code units.</param>
    /// <returns>The canonical key and whether it was shortened.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLength"/> is zero or negative.</exception>
    /// <example><code>var (key, shortened) = RequestProfilingPathMatcher.CreateKey(path, "/api", 128);</code></example>
    public static (string Key, bool Shortened) CreateKey(string path, string prefix, int maximumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);
        path = string.IsNullOrEmpty(path) ? "/" : path;
        prefix = prefix?.TrimEnd('/') ?? string.Empty;
        if (prefix.Length > 0 && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && (path.Length == prefix.Length || path[prefix.Length] == '/'))
        {
            path = path[prefix.Length..];
            if (path.Length == 0) { path = "/"; }
        }

        if (path.Length <= maximumLength)
        {
            return (path, false);
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())));
        if (maximumLength <= 17)
        {
            return (hash[..maximumLength], true);
        }

        var prefixLength = maximumLength - 17;
        if (char.IsHighSurrogate(path[prefixLength - 1])) { prefixLength--; }

        return (path[..prefixLength] + "~" + hash[..16], true);
    }
}
