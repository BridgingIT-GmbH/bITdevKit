// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Extensions
{
    /// <summary>
    ///     Partitions a collection into two groups based on a predicate.
    /// </summary>
    /// <example>
    /// <code>
    /// var numbers = new[] { 1, 2, 3, 4, 5, 6 };
    /// var (evens, odds) = numbers.Partition(n => n % 2 == 0);
    /// // evens: [2, 4, 6]
    /// // odds:  [1, 3, 5]
    ///
    /// var strings = new[] { "a", "b", "", "c", "" };
    /// var (nonEmpty, empty) = strings.Partition(s => !string.IsNullOrEmpty(s));
    /// // nonEmpty: ["a", "b", "c"]
    /// // empty:    ["", ""]
    /// </code>
    /// </example>
    public static (IEnumerable<T> Matches, IEnumerable<T> NonMatches) Partition<T>(
        this IEnumerable<T> source,
        Func<T, bool> predicate)
    {
        var matches = new List<T>();
        var nonMatches = new List<T>();

        foreach (var item in source.SafeNull())
        {
            if (predicate(item))
            {
                matches.Add(item);
            }
            else
            {
                nonMatches.Add(item);
            }
        }

        return (matches, nonMatches);
    }
}