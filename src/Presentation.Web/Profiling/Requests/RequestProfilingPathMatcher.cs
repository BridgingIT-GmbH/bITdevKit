// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Security.Cryptography;
using System.Text;

/// <summary>Matches anchored path globs with bounded state and no regular-expression backtracking.</summary>
/// <example><code>var matcher = new RequestProfilingPathMatcher(["/health/**"]);</code></example>
public sealed class RequestProfilingPathMatcher
{
    private readonly string[][] patterns;

    /// <summary>Validates and prepares at most 128 patterns, each at most 256 characters.</summary>
    /// <example><code>var excluded = new RequestProfilingPathMatcher(["/swagger/**"]).IsMatch(path);</code></example>
    public RequestProfilingPathMatcher(IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Count > 128)
        {
            throw new ArgumentException("At most 128 request blacklist patterns are supported.", nameof(patterns));
        }

        this.patterns = new string[patterns.Count][];
        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 256 || pattern[0] != '/' || pattern.Contains('?') || pattern.Contains('#')
                || pattern.Any(c => char.IsControl(c) || c is '{' or '}' or '[' or ']' or '(' or ')' or '\\'))
            {
                throw new ArgumentException("A blacklist pattern must be an anchored literal path with supported wildcard segments.", nameof(patterns));
            }

            var segments = pattern.TrimEnd('/').Split('/').Skip(1).ToArray();
            if (segments.Any(segment => segment.Contains("**", StringComparison.Ordinal) && segment != "**"))
            {
                throw new ArgumentException("Double star must occupy a complete path segment.", nameof(patterns));
            }

            this.patterns[i] = segments;
        }
    }

    /// <summary>Matches the original incoming path before prefix stripping or key shortening.</summary>
    /// <example><code>if (matcher.IsMatch("/health/live")) ExcludeCapture();</code></example>
    public bool IsMatch(string path)
    {
        var input = (string.IsNullOrEmpty(path) ? "/" : path).AsSpan().TrimEnd('/');
        Span<bool> current = stackalloc bool[257];
        Span<bool> next = stackalloc bool[257];
        foreach (var pattern in this.patterns)
        {
            current.Clear();
            current[0] = true;
            Close(pattern, current);
            var remaining = input.Length == 0 ? ReadOnlySpan<char>.Empty : input[1..];
            while (!remaining.IsEmpty)
            {
                var slash = remaining.IndexOf('/');
                var segment = slash < 0 ? remaining : remaining[..slash];
                next.Clear();
                for (var state = 0; state < pattern.Length; state++)
                {
                    if (current[state])
                    {
                        if (pattern[state] == "**") { next[state] = true; }
                        else if (MatchSegment(pattern[state], segment)) { next[state + 1] = true; }
                    }
                }

                Close(pattern, next);
                next.CopyTo(current);
                remaining = slash < 0 ? ReadOnlySpan<char>.Empty : remaining[(slash + 1)..];
            }

            if (current[pattern.Length])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Strips one prefix on a path boundary and hashes overlong canonical keys.</summary>
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

    private static void Close(string[] pattern, Span<bool> states)
    {
        for (var i = 0; i < pattern.Length; i++)
        {
            if (states[i] && pattern[i] == "**") { states[i + 1] = true; }
        }
    }

    private static bool MatchSegment(string pattern, ReadOnlySpan<char> value)
    {
        var p = 0;
        var v = 0;
        var star = -1;
        var retry = 0;
        while (v < value.Length)
        {
            if (p < pattern.Length && pattern[p] != '*' && char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(value[v])) { p++; v++; }
            else if (p < pattern.Length && pattern[p] == '*') { star = p++; retry = v; }
            else if (star >= 0) { p = star + 1; v = ++retry; }
            else { return false; }
        }

        while (p < pattern.Length && pattern[p] == '*') { p++; }

        return p == pattern.Length;
    }
}
