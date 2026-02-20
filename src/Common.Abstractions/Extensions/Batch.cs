// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Extensions
{
    /// <summary>
    ///     Splits a collection into batches of a specified size.
    /// </summary>
    /// <example>
    /// <code>
    /// var numbers = new[] { 1, 2, 3, 4, 5, 6, 7 };
    /// var batches = numbers.Batch(3);
    /// // Result: [[1,2,3], [4,5,6], [7]]
    /// </code>
    /// </example>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <param name="batchSize">Size of each batch.</param>
    /// <returns>A collection of batches.</returns>
    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentException("Batch size must be greater than 0.", nameof(batchSize));
        }

        return source.SafeNull()
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.item));
    }
}