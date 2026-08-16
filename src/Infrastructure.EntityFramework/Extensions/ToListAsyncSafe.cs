// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    /// <summary>
    /// Executes the to list async safe operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task<List<TSource>> ToListAsyncSafe<TSource>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(source, nameof(source));

        if (source is not IAsyncEnumerable<TSource>)
        {
            return Task.FromResult(source.ToList());
        }

        return source.ToListAsync(cancellationToken);
    }
}
