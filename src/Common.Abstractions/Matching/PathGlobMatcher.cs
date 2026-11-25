// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Matches complete slash-delimited paths against anchored globs with bounded matching state.</summary>
/// <remarks>
/// Use * within a segment and ** as a complete segment to match zero or more segments.
/// Trailing slashes are ignored. Literal comparison uses invariant casing on individual UTF-16 code units.
/// Instances can be shared across callers.
/// </remarks>
/// <example><code>
/// var matcher = new PathGlobMatcher(["/reports/**", "/jobs/archive-*"]);
/// var matches = matcher.IsMatch("/reports/monthly/sales");
/// </code></example>
public sealed class PathGlobMatcher
{
    private const int MaximumPatternCount = 128;
    private const int MaximumPatternLength = 256;
    private readonly string[][] patterns;
    private readonly Dictionary<string, GlobPatternMatcher> asciiPatterns = new(StringComparer.Ordinal);

    /// <summary>Validates and prepares up to 128 anchored patterns of at most 256 UTF-16 code units each.</summary>
    /// <param name="patterns">The slash-rooted patterns to match. An empty list matches nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="patterns"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The list exceeds its count limit or contains a null, blank, overlong, unrooted, or unsupported pattern.
    /// </exception>
    /// <example><code>var matcher = new PathGlobMatcher(["/reports/**"]);</code></example>
    public PathGlobMatcher(IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        // Retain validation diagnostics for the Request Profiling facade.
        if (patterns.Count > MaximumPatternCount)
        {
            throw new ArgumentException("At most 128 request path patterns are supported.", nameof(patterns));
        }

        this.patterns = new string[patterns.Count][];
        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > MaximumPatternLength || pattern[0] != '/' || pattern.Contains('?') || pattern.Contains('#')
                || pattern.Any(c => char.IsControl(c) || c is '{' or '}' or '[' or ']' or '(' or ')' or '\\'))
            {
                throw new ArgumentException("A request path pattern must be an anchored literal path with supported wildcard segments.", nameof(patterns));
            }

            var segments = pattern.TrimEnd('/').Split('/').Skip(1).ToArray();
            if (segments.Any(segment => segment.Contains("**", StringComparison.Ordinal) && segment != "**"))
            {
                throw new ArgumentException("Double star must occupy a complete path segment.", nameof(patterns));
            }

            foreach (var segment in segments)
            {
                if (segment != "**" && Ascii.IsValid(segment) && !this.asciiPatterns.ContainsKey(segment))
                {
                    this.asciiPatterns.Add(segment, new(segment));
                }
            }

            this.patterns[i] = segments;
        }
    }

    /// <summary>Checks whether any pattern matches the complete path.</summary>
    /// <param name="path">The slash-rooted path. Null and empty inputs are treated as the root path.</param>
    /// <returns>True if a pattern matches. Nonempty paths lacking a leading slash return false.</returns>
    /// <example><code>var matches = matcher.IsMatch("/reports/monthly");</code></example>
    public bool IsMatch(string path)
    {
        if (!string.IsNullOrEmpty(path) && path[0] != '/')
        {
            return false;
        }

        var input = path.AsSpan().TrimEnd('/');
        Span<bool> current = stackalloc bool[MaximumPatternLength + 1];
        Span<bool> next = stackalloc bool[MaximumPatternLength + 1];
        foreach (var pattern in this.patterns)
        {
            current.Clear();
            current[0] = true;
            AdvancePastDoubleStars(pattern, current);
            var remaining = input.Length == 0 ? ReadOnlySpan<char>.Empty : input[1..];
            while (!remaining.IsEmpty)
            {
                var slash = remaining.IndexOf('/');
                var segment = slash < 0 ? remaining : remaining[..slash];
                next.Clear();
                for (var state = 0; state < pattern.Length; state++)
                {
                    if (!current[state])
                    {
                        continue;
                    }

                    if (pattern[state] == "**")
                    {
                        next[state] = true;
                    }
                    else if (this.MatchSegment(pattern[state], segment))
                    {
                        next[state + 1] = true;
                    }
                }

                AdvancePastDoubleStars(pattern, next);
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

    private static void AdvancePastDoubleStars(string[] pattern, Span<bool> states)
    {
        for (var i = 0; i < pattern.Length; i++)
        {
            if (states[i] && pattern[i] == "**")
            {
                states[i + 1] = true;
            }
        }
    }

    private bool MatchSegment(string pattern, ReadOnlySpan<char> value)
    {
        if (Ascii.IsValid(value) && this.asciiPatterns.TryGetValue(pattern, out var matcher))
        {
            return matcher.IsMatch(value);
        }

        // Compare UTF-16 code units so non-ASCII paths retain the original path matching rules.
        var patternIndex = 0;
        var valueIndex = 0;
        var starIndex = -1;
        var retryIndex = 0;
        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] != '*'
                && char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex]))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                retryIndex = valueIndex;
            }
            else if (starIndex >= 0)
            {
                patternIndex = starIndex + 1;
                valueIndex = ++retryIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
