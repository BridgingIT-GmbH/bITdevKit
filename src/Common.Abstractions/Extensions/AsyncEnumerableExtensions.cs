// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.CompilerServices;

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

    public static ValueTask<bool> ContainsAsync<T>(
        this IAsyncEnumerable<T> source,
        T value,
        CancellationToken cancellationToken = default)
    {
        return ContainsAsync(source, value, null, cancellationToken);
    }

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

    public static IAsyncEnumerable<T> DistinctAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        return DistinctAsync(source, null, cancellationToken);
    }

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

    public static IAsyncEnumerable<T> DistinctByAsync<T, TKey>(
        this IAsyncEnumerable<T> source,
        Func<T, TKey> getKey,
        CancellationToken cancellationToken = default)
    {
        return DistinctByAsync(source, getKey, null, cancellationToken);
    }

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

    public static ValueTask<T> FirstAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        return FirstAsync(source, _ => true, cancellationToken);
    }

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

    public static ValueTask<T> LastAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        return LastAsync(source, _ => true, cancellationToken);
    }

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

    public static ValueTask<T> LastOrDefaultAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        return LastOrDefaultAsync(source, _ => true, cancellationToken);
    }

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

    public static IAsyncEnumerable<T> WhereNotNull<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
        where T : class
    {
        return source.WhereAsync(item => item is not null, cancellationToken)!;
    }

    public static IAsyncEnumerable<string> WhereNotNullOrEmpty(
        this IAsyncEnumerable<string> source,
        CancellationToken cancellationToken = default)
    {
        return source.WhereAsync(item => !string.IsNullOrEmpty(item), cancellationToken)!;
    }

    public static IAsyncEnumerable<string> WhereNotNullOrWhiteSpace(
        this IAsyncEnumerable<string> source,
        CancellationToken cancellationToken = default)
    {
        return source.WhereAsync(item => !string.IsNullOrWhiteSpace(item), cancellationToken)!;
    }
}