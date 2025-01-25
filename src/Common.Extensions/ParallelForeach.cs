// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Extensions
{
    public static Task ParallelForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> action)
    {
        return ParallelForEachAsync(source, action, CancellationToken.None);
    }

    public static Task ParallelForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
    {
        return ParallelForEachAsync(source, action, Environment.ProcessorCount, cancellationToken);
    }

    public static Task ParallelForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> action,
        int degreeOfParallelism)
    {
        return ParallelForEachAsync(source, action, degreeOfParallelism, CancellationToken.None);
    }

    public static Task ParallelForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> action,
        int degreeOfParallelism,
        CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return Task.CompletedTask;
        }

        return Parallel.ForEachAsync(source,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = degreeOfParallelism,
                CancellationToken = cancellationToken = default
            },
            (item, ct) => new ValueTask(action(item)));
    }
}