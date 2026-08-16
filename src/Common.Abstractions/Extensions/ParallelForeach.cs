// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Extensions
{
    /// <summary>Processes sequence items concurrently using the environment processor count as the maximum concurrency.</summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The items to process.</param>
    /// <param name="action">The asynchronous action invoked for each item.</param>
    /// <returns>A task that completes when all actions complete.</returns>
    public static Task ParallelForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> action)
    {
        return ParallelForEachAsync(source, action, CancellationToken.None);
    }

    /// <summary>Processes sequence items concurrently using the environment processor count as the maximum concurrency.</summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The items to process.</param>
    /// <param name="action">The asynchronous action invoked for each item.</param>
    /// <param name="cancellationToken">A token accepted for API compatibility; the current implementation does not apply it to parallel execution.</param>
    /// <returns>A task that completes when all actions complete.</returns>
    public static Task ParallelForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
    {
        return ParallelForEachAsync(source, action, Environment.ProcessorCount, cancellationToken);
    }

    /// <summary>Processes sequence items concurrently with a caller-specified maximum degree of parallelism.</summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The items to process.</param>
    /// <param name="action">The asynchronous action invoked for each item.</param>
    /// <param name="degreeOfParallelism">The maximum number of actions scheduled concurrently.</param>
    /// <returns>A task that completes when all actions complete.</returns>
    public static Task ParallelForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> action,
        int degreeOfParallelism)
    {
        return ParallelForEachAsync(source, action, degreeOfParallelism, CancellationToken.None);
    }

    /// <summary>Processes sequence items concurrently with a caller-specified maximum degree of parallelism.</summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The items to process; null or empty input completes immediately.</param>
    /// <param name="action">The asynchronous action invoked for each item; a null action completes immediately.</param>
    /// <param name="degreeOfParallelism">The maximum number of actions scheduled concurrently.</param>
    /// <param name="cancellationToken">A token accepted for API compatibility; the current implementation replaces it with a default token.</param>
    /// <returns>A task that completes when all actions complete.</returns>
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
