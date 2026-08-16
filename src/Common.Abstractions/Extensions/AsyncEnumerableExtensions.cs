// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides asynchronous sequence operators that enumerate sources with cancellation support.
/// </summary>
public static class AsyncEnumerableExtensions
{
    //public static async ValueTask<bool> AnyAsync<T>(
    //    this IAsyncEnumerable<T> source,
    //    CancellationToken cancellationToken = default)
    //{
    //    await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
    //    {
    //        return true;
    //    }

    //    return false;
    //}

    /// <summary>
    /// Determines whether an asynchronous sequence contains an item accepted by a predicate, stopping at the first match.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to inspect.</param>
    /// <param name="predicate">The condition used to identify a matching item.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns><see langword="true"/> when a matching item is encountered; otherwise, <see langword="false"/>.</returns>
    public static async ValueTask<bool> AnyAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Streams all items from the first asynchronous sequence followed by all items from the second sequence.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The sequence enumerated first.</param>
    /// <param name="second">The sequence enumerated after <paramref name="source"/> completes.</param>
    /// <param name="cancellationToken">A token that cancels enumeration of either sequence.</param>
    /// <returns>An asynchronous sequence preserving the order of both inputs.</returns>
    public static async IAsyncEnumerable<T> ConcatAsync<T>(
        this IAsyncEnumerable<T> source,
        IAsyncEnumerable<T> second,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }

        await foreach (var item in second.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Determines whether an asynchronous sequence contains a value using the default equality comparer.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to inspect.</param>
    /// <param name="value">The value to locate.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns><see langword="true"/> when an equal item is encountered; otherwise, <see langword="false"/>.</returns>
    public static ValueTask<bool> ContainsAsync<T>(
        this IAsyncEnumerable<T> source,
        T value,
        CancellationToken cancellationToken = default)
    {
        return ContainsAsync(source, value, null, cancellationToken);
    }

    /// <summary>
    /// Determines whether an asynchronous sequence contains a value using a supplied equality comparer.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="enumerable">The asynchronous sequence to inspect.</param>
    /// <param name="value">The value to locate.</param>
    /// <param name="comparer">The comparer to use, or <see langword="null"/> to use <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns><see langword="true"/> when an equal item is encountered; otherwise, <see langword="false"/>.</returns>
    public static async ValueTask<bool> ContainsAsync<T>(
        this IAsyncEnumerable<T> enumerable,
        T value,
        IEqualityComparer<T> comparer,
        CancellationToken cancellationToken = default)
    {
        comparer ??= EqualityComparer<T>.Default;

        await foreach (var item in enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (comparer.Equals(item, value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts all items produced by an asynchronous sequence.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to enumerate.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>The number of produced items.</returns>
    public static async ValueTask<int> CountAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        var result = 0;

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            result++;
        }

        return result;
    }

    /// <summary>
    /// Counts the items in an asynchronous sequence that satisfy a predicate.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to enumerate.</param>
    /// <param name="predicate">The condition that identifies items to count.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>The number of matching items.</returns>
    public static async ValueTask<int> CountAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        var result = 0;

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                result++;
            }
        }

        return result;
    }

    /// <summary>
    /// Streams the first occurrence of each value using the default equality comparer.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to filter.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence of distinct values in first-seen order.</returns>
    public static IAsyncEnumerable<T> DistinctAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        return DistinctAsync(source, null, cancellationToken);
    }

    /// <summary>
    /// Streams the first occurrence of each value using a supplied equality comparer.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to filter.</param>
    /// <param name="comparer">The comparer used to identify duplicate values.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence of distinct values in first-seen order.</returns>
    public static async IAsyncEnumerable<T> DistinctAsync<T>(
        this IAsyncEnumerable<T> source,
        IEqualityComparer<T> comparer,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var hashSet = new HashSet<T>(comparer);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (hashSet.Add(item))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Streams the first item for each projected key using the default key comparer.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <typeparam name="TKey">The projected key type.</typeparam>
    /// <param name="source">The asynchronous sequence to filter.</param>
    /// <param name="getKey">The function that selects a key from each item.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence containing the first item for each key.</returns>
    public static IAsyncEnumerable<T> DistinctByAsync<T, TKey>(
        this IAsyncEnumerable<T> source,
        Func<T, TKey> getKey,
        CancellationToken cancellationToken = default)
    {
        return DistinctByAsync(source, getKey, null, cancellationToken);
    }

    /// <summary>
    /// Streams the first item for each projected key using a supplied key comparer.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <typeparam name="TKey">The projected key type.</typeparam>
    /// <param name="enumerable">The asynchronous sequence to filter.</param>
    /// <param name="getKey">The function that selects a key from each item.</param>
    /// <param name="comparer">The comparer used to identify duplicate keys.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence containing the first item for each key.</returns>
    public static async IAsyncEnumerable<T> DistinctByAsync<T, TKey>(
        this IAsyncEnumerable<T> enumerable,
        Func<T, TKey> getKey,
        IEqualityComparer<TKey> comparer,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var hashSet = new HashSet<TKey>(comparer);

        await foreach (var item in enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var key = getKey(item);

            if (hashSet.Add(key))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Returns the first item produced by an asynchronous sequence.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to inspect.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>The first produced item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the sequence produces no items.</exception>
    public static ValueTask<T> FirstAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        return FirstAsync(source, _ => true, cancellationToken);
    }

    /// <summary>
    /// Returns the first item in an asynchronous sequence that satisfies a predicate.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to inspect.</param>
    /// <param name="predicate">The condition used to select an item.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>The first matching item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no item satisfies <paramref name="predicate"/>.</exception>
    public static async ValueTask<T> FirstAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                return item;
            }
        }

        throw new InvalidOperationException("The source sequence is empty");
    }

    //public static ValueTask<T> FirstOrDefaultAsync<T>(
    //    this IAsyncEnumerable<T> source,
    //    CancellationToken cancellationToken = default)
    //{
    //    return FirstOrDefaultAsync(source, _ => true, cancellationToken);
    //}

    /// <summary>
    /// Returns the first item in an asynchronous sequence that satisfies a predicate, or the default value when no match exists.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="enumerable">The asynchronous sequence to inspect.</param>
    /// <param name="predicate">The condition used to select an item.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>The first matching item, or <see langword="default"/>.</returns>
    public static async ValueTask<T> FirstOrDefaultAsync<T>(
        this IAsyncEnumerable<T> enumerable,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        await foreach (var item in enumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return default;
    }

    /// <summary>
    /// Returns the last item produced by an asynchronous sequence after enumerating it completely.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to inspect.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>The last produced item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the sequence produces no items.</exception>
    public static ValueTask<T> LastAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        return LastAsync(source, _ => true, cancellationToken);
    }

    /// <summary>
    /// Returns the last item in an asynchronous sequence that satisfies a predicate.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to inspect.</param>
    /// <param name="predicate">The condition used to select an item.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>The last matching item after the source has been enumerated completely.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no item satisfies <paramref name="predicate"/>.</exception>
    public static async ValueTask<T> LastAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        var hasValue = false;
        T result = default!;

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                hasValue = true;
                result = item;
            }
        }

        if (hasValue)
        {
            return result!;
        }

        throw new InvalidOperationException("The source sequence is empty");
    }

    /// <summary>
    /// Returns the last item produced by an asynchronous sequence, or the default value when the sequence is empty.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to inspect.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>The last produced item, or <see langword="default"/>.</returns>
    public static ValueTask<T> LastOrDefaultAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        return LastOrDefaultAsync(source, _ => true, cancellationToken);
    }

    /// <summary>
    /// Returns the last item in an asynchronous sequence that satisfies a predicate, or the default value when no match exists.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to inspect.</param>
    /// <param name="predicate">The condition used to select an item.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>The last matching item, or <see langword="default"/>.</returns>
    public static async ValueTask<T> LastOrDefaultAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        T result = default;

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                result = item;
            }
        }

        return result;
    }

    /// <summary>
    /// Lazily projects each item from an asynchronous sequence into a result value.
    /// </summary>
    /// <typeparam name="T">The source element type.</typeparam>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="source">The asynchronous sequence to transform.</param>
    /// <param name="selector">The synchronous projection applied to each item.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence of projected values.</returns>
    public static async IAsyncEnumerable<TResult> SelectAsync<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, TResult> selector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return selector(item);
        }
    }

    /// <summary>
    /// Streams at most a requested number of items from the start of an asynchronous sequence.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to enumerate.</param>
    /// <param name="count">The maximum number of items to produce; non-positive values produce an empty sequence.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence containing up to <paramref name="count"/> items.</returns>
    public static async IAsyncEnumerable<T> TakeAsync<T>(
        this IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            yield break;
        }

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;

            if (--count == 0)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Skips a requested number of items and streams the remainder of an asynchronous sequence.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to enumerate.</param>
    /// <param name="count">The number of leading items to skip.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>
    /// The items produced after the first <paramref name="count"/> items; an empty sequence when
    /// <paramref name="count"/> is not positive.
    /// </returns>
    public static async IAsyncEnumerable<T> SkipAsync<T>(
        this IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerator<T> enumerator = null;

        try
        {
            enumerator = source.GetAsyncEnumerator(cancellationToken);

            if (count > 0)
            {
                while (count > 0 && await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    count--;
                }

                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    yield return enumerator.Current;
                }
            }
        }
        finally
        {
            if (enumerator is not null)
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    //public static async ValueTask<List<T>> ToListAsync<T>(
    //    this IAsyncEnumerable<T> source,
    //    CancellationToken cancellationToken = default)
    //{
    //    var result = new List<T>();

    //    await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
    //    {
    //        result.Add(item);
    //    }

    //    return result;
    //}

    /// <summary>
    /// Lazily streams the items from an asynchronous sequence that satisfy a predicate.
    /// </summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The asynchronous sequence to filter.</param>
    /// <param name="selector">The condition used to retain items.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence containing only accepted items.</returns>
    public static async IAsyncEnumerable<T> WhereAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> selector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (selector(item))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Filters null references from an asynchronous sequence.
    /// </summary>
    /// <typeparam name="T">The reference type produced by the sequence.</typeparam>
    /// <param name="source">The asynchronous sequence to filter.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence containing only non-null items.</returns>
    public static IAsyncEnumerable<T> WhereNotNull<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
        where T : class
    {
        return source.WhereAsync(item => item is not null, cancellationToken)!;
    }

    /// <summary>
    /// Filters null and empty strings from an asynchronous sequence while retaining whitespace-only values.
    /// </summary>
    /// <param name="source">The asynchronous string sequence to filter.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence containing strings for which <see cref="string.IsNullOrEmpty(string)"/> is false.</returns>
    public static IAsyncEnumerable<string> WhereNotNullOrEmpty(
        this IAsyncEnumerable<string> source,
        CancellationToken cancellationToken = default)
    {
        return source.WhereAsync(item => !string.IsNullOrEmpty(item), cancellationToken)!;
    }

    /// <summary>
    /// Filters null, empty, and whitespace-only strings from an asynchronous sequence.
    /// </summary>
    /// <param name="source">The asynchronous string sequence to filter.</param>
    /// <param name="cancellationToken">A token that cancels source enumeration.</param>
    /// <returns>An asynchronous sequence containing strings with non-whitespace content.</returns>
    public static IAsyncEnumerable<string> WhereNotNullOrWhiteSpace(
        this IAsyncEnumerable<string> source,
        CancellationToken cancellationToken = default)
    {
        return source.WhereAsync(item => !string.IsNullOrWhiteSpace(item), cancellationToken)!;
    }
}
