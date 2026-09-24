// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides immutable-style insertion helpers and cancellation-aware conversion to asynchronous sequences.
/// </summary>
public static class EnumerableExtensions
{
    /// <param name="source">the source collection.</param>
    /// <typeparam name="T">the source.</typeparam>
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        ///     Adds the item to the collection.
        /// </summary>
        /// <param name="item">The item to add.</param>
        public IEnumerable<T> Add(T item)
        {
            return source.Insert(item, -1);
        }

        /// <summary>
        ///     Adds the items to the collection.
        /// </summary>
        /// <param name="items">The items to add.</param>
        public IEnumerable<T> Add(IEnumerable<T> items)
        {
            return source.InsertRange(items, -1);
        }

        /// <summary>
        ///     Inserts the item in the collection.
        /// </summary>
        /// <param name="item">The item to insert.</param>
        /// <param name="index">the index at which the item should inserted.</param>
        public IEnumerable<T> Insert(T item, int index = 0)
        {
            if (item is null)
            {
                return source;
            }

            if (source is null)
            {
                return new List<T> { item };
            }

            var result = new List<T>(source);
            if (index >= 0)
            {
                result.Insert(index, item);
            }
            else
            {
                result.Add(item);
            }

            return result;
        }

        /// <summary>
        ///     Inserts the items in the collection.
        /// </summary>
        /// <param name="items">The items to insert.</param>
        /// <param name="index">the index at which the item should inserted.</param>
        public IEnumerable<T> InsertRange(IEnumerable<T> items, int index = 0)
        {
            if (items is null)
            {
                return source;
            }

            if (source is null)
            {
                return new List<T>(items);
            }

            var result = new List<T>(source);
            if (index >= 0)
            {
                result.InsertRange(index, items);
            }
            else
            {
                result.AddRange(items);
            }

            return result;
        }

        /// <summary>
        ///    Converts the enumerable to an asynchronous enumerable.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An asynchronous enumerable representing the source enumerable.</returns>
        public async IAsyncEnumerable<T> ToAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (source is null)
            {
                yield break;
            }

            foreach (var item in source)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();

                yield return item;
            }
        }
    }
}
