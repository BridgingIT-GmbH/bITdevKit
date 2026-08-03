// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>Returns the portion of a string between the first matching start and end delimiters.</summary>
    /// <param name="source">The string to slice.</param>
    /// <param name="start">The delimiter after which the slice begins.</param>
    /// <param name="end">The delimiter before which the slice ends.</param>
    /// <param name="comparison">The comparison mode for both delimiters.</param>
    /// <returns>The portion between the delimiters.</returns>
    /// <example><code>var value = "[content]".Slice("[", "]"); // "content"</code></example>
    [DebuggerStepThrough]
    public static string Slice(
        this string source,
        string start,
        string end,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        return SliceFrom(source, start, comparison).SliceTill(end, comparison);
    }

    /// <summary>Returns the substring between two zero-based indexes.</summary>
    /// <param name="source">The string to slice.</param>
    /// <param name="start">The inclusive start index.</param>
    /// <param name="end">The exclusive end index. Values lower than <paramref name="start" /> use the end of the string.</param>
    /// <returns>The selected substring.</returns>
    /// <example><code>var value = "abcdef".Slice(1, 4); // "bcd"</code></example>
    [DebuggerStepThrough]
    public static string Slice(this string source, int start, int end)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        if (end < start)
        {
            end = source.Length;
        }

        return source[start..end];
    }
}
