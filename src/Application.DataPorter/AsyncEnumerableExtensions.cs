// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

public static class AsyncEnumerableExtensions
{
    /// <summary>
    ///   Converts the asynchronous enumerable to a list asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the asynchronous enumerable.</typeparam>
    /// <param name="source">The source asynchronous enumerable.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of elements.</returns>
    public static async Task<List<T>> ToListAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        var list = new List<T>();

        if (source is null)
        {
            return list;
        }

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            list.Add(item);
        }

        return list;
    }
}
