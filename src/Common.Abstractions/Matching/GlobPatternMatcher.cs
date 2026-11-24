// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Buffers;
using System.Text;

/// <summary>Matches whole-text globs with Unicode-scalar wildcards and no recursive backtracking.</summary>
/// <remarks>Patterns support * for zero or more scalars and ? for one scalar, including line breaks.
/// Backslash escapes only *, ?, or backslash. Literal matching uses invariant scalar casing unless case sensitivity is selected.
/// Instances are immutable and can be shared between callers. Callers own limits on pattern and input sizes.</remarks>
/// <example><code>var matcher = new GlobPatternMatcher(@"report:\?*");
/// var matches = matcher.IsMatch("REPORT:?42");</code></example>
public sealed class GlobPatternMatcher
{
    private readonly (Rune Value, byte Kind)[] tokens;
    private readonly bool ignoreCase;

    /// <summary>Parses a reusable glob, allowing an empty pattern which matches only empty text.</summary>
    /// <param name="pattern">The valid UTF-16 glob to parse.</param>
    /// <param name="ignoreCase">Whether literal comparisons use invariant case folding.</param>
    /// <exception cref="ArgumentException">The pattern contains invalid UTF-16 or an unsupported or incomplete escape.</exception>
    /// <example><code>var matcher = new GlobPatternMatcher("report:*", ignoreCase: false);</code></example>
    public GlobPatternMatcher(string pattern, bool ignoreCase = true)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        this.ignoreCase = ignoreCase;
        var parsed = new List<(Rune Value, byte Kind)>();
        var escaping = false;
        for (var position = 0; position < pattern.Length;)
        {
            var rune = ReadRune(pattern, position);
            position += rune.Utf16SequenceLength;
            if (escaping)
            {
                if (rune.Value is not ('*' or '?' or '\\'))
                {
                    throw new ArgumentException("A pattern contains an invalid escape.", nameof(pattern));
                }

                parsed.Add((rune, 0));
                escaping = false;
            }
            else if (rune.Value == '\\')
            {
                escaping = true;
            }
            else if (rune.Value == '*')
            {
                if (parsed.Count == 0 || parsed[^1].Kind != 2)
                {
                    parsed.Add((rune, 2));
                }
            }
            else
            {
                parsed.Add((ignoreCase ? Rune.ToUpperInvariant(rune) : rune, rune.Value == '?' ? (byte)1 : (byte)0));
            }
        }

        if (escaping)
        {
            throw new ArgumentException("A pattern ends with an incomplete escape.", nameof(pattern));
        }

        this.tokens = parsed.ToArray();
    }

    /// <summary>Matches the complete text, with ? consuming one Unicode scalar rather than one UTF-16 code unit.</summary>
    /// <param name="text">The text to match.</param>
    /// <returns>Whether the complete text satisfies the parsed pattern.</returns>
    /// <exception cref="ArgumentException">Matching encounters invalid UTF-16 text.</exception>
    /// <example><code>var matches = new GlobPatternMatcher("?").IsMatch("😀");</code></example>
    public bool IsMatch(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return this.IsMatch(text.AsSpan());
    }

    /// <summary>Matches a complete text view without allocating an input string.</summary>
    /// <param name="text">The valid UTF-16 text view to match.</param>
    /// <returns>Whether the complete view satisfies the parsed pattern.</returns>
    /// <exception cref="ArgumentException">Matching encounters invalid UTF-16 text.</exception>
    /// <example><code>var matches = matcher.IsMatch(buffer.AsSpan(offset, length));</code></example>
    public bool IsMatch(ReadOnlySpan<char> text)
    {
        var input = 0;
        var token = 0;
        var star = -1;
        var retry = 0;
        while (input < text.Length)
        {
            var rune = ReadRune(text, input);
            if (token < this.tokens.Length && this.tokens[token].Kind != 2 &&
                (this.tokens[token].Kind == 1 || (this.ignoreCase ? Rune.ToUpperInvariant(rune) : rune) == this.tokens[token].Value))
            {
                input += rune.Utf16SequenceLength;
                token++;
            }
            else if (token < this.tokens.Length && this.tokens[token].Kind == 2)
            {
                star = token++;
                retry = input;
            }
            else if (star >= 0)
            {
                token = star + 1;
                retry += ReadRune(text, retry).Utf16SequenceLength;
                input = retry;
            }
            else
            {
                return false;
            }
        }

        while (token < this.tokens.Length && this.tokens[token].Kind == 2)
        {
            token++;
        }

        return token == this.tokens.Length;
    }

    private static Rune ReadRune(ReadOnlySpan<char> text, int position) =>
        Rune.DecodeFromUtf16(text[position..], out var rune, out _) == OperationStatus.Done
            ? rune : throw new ArgumentException("Text must contain valid Unicode scalars.", nameof(text));
}