// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>
    ///     Performs an action on each value of the enumerable.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">The items.</param>
    /// <param name="action">Action to perform on every item.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>the source with the actions applied.</returns>
    [DebuggerStepThrough]
    public static IEnumerable<T> ForEach<T>(
        this IEnumerable<T> source,
        Action<T> action,
        CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            action(item);
        }

        return source;
    }

    /// <summary>Awaits an action sequentially for each item and returns the original enumerable.</summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The sequence to enumerate; null or empty input is returned unchanged.</param>
    /// <param name="action">The asynchronous action to await for each item.</param>
    /// <param name="cancellationToken">A token checked before each action invocation.</param>
    /// <returns>The original <paramref name="source"/> after all actions complete.</returns>
    public static async Task<IEnumerable<T>> ForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(item).ConfigureAwait(false);
        }

        return source;
    }

    /// <summary>Applies an action to each collection item and returns a new list containing the enumerated items.</summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="source">The collection to enumerate; null or empty input is returned unchanged.</param>
    /// <param name="action">The action invoked for each item.</param>
    /// <param name="cancellationToken">A token checked before each action invocation.</param>
    /// <returns>A list of the source items after processing, or the unchanged source when no work is performed.</returns>
    [DebuggerStepThrough]
    public static ICollection<T> ForEach<T>(
        this ICollection<T> source,
        Action<T> action,
        CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        return source.AsEnumerable().ForEach(action, cancellationToken).ToList();
    }

    /// <summary>Awaits an action sequentially for each collection item and returns the original collection.</summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="source">The collection to enumerate; null or empty input is returned unchanged.</param>
    /// <param name="action">The asynchronous action to await for each item.</param>
    /// <param name="cancellationToken">A token checked before each action invocation.</param>
    /// <returns>The original <paramref name="source"/> after all actions complete.</returns>
    public static async Task<ICollection<T>> ForEachAsync<T>(
        this ICollection<T> source,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(item).ConfigureAwait(false);
        }

        return source;
    }

    /// <summary>Traverses a sequence depth-first, invoking an action on each item before visiting its selected children.</summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="source">The root sequence to traverse.</param>
    /// <param name="childSelector">The optional function that selects child nodes for recursive traversal.</param>
    /// <param name="action">The action invoked for every visited node.</param>
    /// <param name="cancellationToken">A token checked before each node is processed.</param>
    /// <returns>The original root sequence.</returns>
    [DebuggerStepThrough]
    public static IEnumerable<T> ForEach<T>(
        this IEnumerable<T> source,
        Func<T, IEnumerable<T>> childSelector,
        Action<T> action,
        CancellationToken cancellationToken = default)
    {
        if (source.IsNullOrEmpty() || action is null)
        {
            return source;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            action(item);
            childSelector?.Invoke(item).ForEach(childSelector, action, cancellationToken);
        }

        return source;
    }

    /// <summary>
    ///     Executes an action for each batch of items in parallel.
    /// </summary>
    /// <example>
    /// <code>
    /// var items = Enumerable.Range(1, 1000);
    /// await items.ParallelForEachAsync(
    ///     async number => {
    ///         await ProcessItemAsync(number);
    ///     },
    ///     maxDegreeOfParallelism: 5,
    ///     batchSize: 100
    /// );
    /// </code>
    /// </example>
    public static async Task ForEachParallelAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> action,
        int maxDegreeOfParallelism = 5,
        int batchSize = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDegreeOfParallelism);

        if (source.IsNullOrEmpty() || action is null)
        {
            return;
        }

        var batches = source.Batch(batchSize);

        await Parallel.ForEachAsync(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            async (batch, token) =>
            {
                foreach (var item in batch)
                {
                    await action(item);
                }
            });
    }
}
