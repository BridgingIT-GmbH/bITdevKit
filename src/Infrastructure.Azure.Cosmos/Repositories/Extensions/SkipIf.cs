// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Cosmos.Repositories;

public static partial class Extensions
{
    /// <summary>
    /// Executes the skip if operation.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="count">The number of values to process.</param>
    /// <param name="condition">The condition used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static IQueryable<T> SkipIf<T>(this IQueryable<T> source, int? count = null, bool? condition = true)
    {
        if (condition == true && count.HasValue && count.Value > 0)
        {
            return source.Skip(count.Value);
        }

        return source;
    }
}
