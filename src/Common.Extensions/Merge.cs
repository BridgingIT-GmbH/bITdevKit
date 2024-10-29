// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;

/// <summary>
///     Provides extension methods for merging various collection types.
/// </summary>
public static partial class Extensions
{
    /// <summary>
    ///     Merges two enumerable collections into a new collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collections.</typeparam>
    /// <param name="primary">The primary collection.</param>
    /// <param name="secondary">The secondary collection.</param>
    /// <returns>A new collection containing elements from both collections.</returns>
    public static IEnumerable<T> Merge<T>(this IEnumerable<T> primary, IEnumerable<T> secondary)
    {
        var result = new List<T>();

        if (primary.SafeAny())
        {
            result.AddRange(primary);
        }

        if (secondary.SafeAny())
        {
            result.AddRange(secondary);
        }

        return result;
    }

    /// <summary>
    ///     Merges two lists into a new list.
    /// </summary>
    /// <typeparam name="T">The type of elements in the lists.</typeparam>
    /// <param name="primary">The primary list.</param>
    /// <param name="secondary">The secondary list.</param>
    /// <returns>A new list containing elements from both lists.</returns>
    public static List<T> Merge<T>(this List<T> primary, List<T> secondary)
    {
        var result = new List<T>();

        if (primary.SafeAny())
        {
            result.AddRange(primary);
        }

        if (secondary.SafeAny())
        {
            result.AddRange(secondary);
        }

        return result;
    }

    /// <summary>
    ///     Merges two dictionaries into a new dictionary, with primary values taking precedence.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionaries.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionaries.</typeparam>
    /// <param name="primary">The primary dictionary.</param>
    /// <param name="secondary">The secondary dictionary.</param>
    /// <returns>A new dictionary containing merged key-value pairs.</returns>
    public static Dictionary<TKey, TValue> Merge<TKey, TValue>(
        this Dictionary<TKey, TValue> primary,
        Dictionary<TKey, TValue> secondary) where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();

        if (secondary.SafeAny())
        {
            foreach (var pair in secondary)
            {
                result[pair.Key] = pair.Value;
            }
        }

        if (primary.SafeAny())
        {
            foreach (var pair in primary)
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    /// <summary>
    ///     Merges two hash sets into a new hash set.
    /// </summary>
    /// <typeparam name="T">The type of elements in the hash sets.</typeparam>
    /// <param name="primary">The primary hash set.</param>
    /// <param name="secondary">The secondary hash set.</param>
    /// <returns>A new hash set containing unique elements from both sets.</returns>
    public static HashSet<T> Merge<T>(this HashSet<T> primary, HashSet<T> secondary)
    {
        var result = new HashSet<T>();

        if (secondary.SafeAny())
        {
            result.UnionWith(secondary);
        }

        if (primary.SafeAny())
        {
            result.UnionWith(primary);
        }

        return result;
    }

    /// <summary>
    ///     Merges two sorted sets into a new sorted set.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sorted sets.</typeparam>
    /// <param name="primary">The primary sorted set.</param>
    /// <param name="secondary">The secondary sorted set.</param>
    /// <returns>A new sorted set containing unique elements from both sets.</returns>
    public static SortedSet<T> Merge<T>(this SortedSet<T> primary, SortedSet<T> secondary)
    {
        var result = new SortedSet<T>();

        if (secondary.SafeAny())
        {
            result.UnionWith(secondary);
        }

        if (primary.SafeAny())
        {
            result.UnionWith(primary);
        }

        return result;
    }

    /// <summary>
    ///     Merges two sorted dictionaries into a new sorted dictionary, with primary values taking precedence.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionaries.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionaries.</typeparam>
    /// <param name="primary">The primary sorted dictionary.</param>
    /// <param name="secondary">The secondary sorted dictionary.</param>
    /// <returns>A new sorted dictionary containing merged key-value pairs.</returns>
    public static SortedDictionary<TKey, TValue> Merge<TKey, TValue>(
        this SortedDictionary<TKey, TValue> primary,
        SortedDictionary<TKey, TValue> secondary) where TKey : notnull
    {
        var result = new SortedDictionary<TKey, TValue>();

        if (secondary.SafeAny())
        {
            foreach (var pair in secondary)
            {
                result[pair.Key] = pair.Value;
            }
        }

        if (primary.SafeAny())
        {
            foreach (var pair in primary)
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    /// <summary>
    ///     Merges two concurrent dictionaries into a new concurrent dictionary, with primary values taking precedence.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionaries.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionaries.</typeparam>
    /// <param name="primary">The primary concurrent dictionary.</param>
    /// <param name="secondary">The secondary concurrent dictionary.</param>
    /// <returns>A new concurrent dictionary containing merged key-value pairs.</returns>
    public static ConcurrentDictionary<TKey, TValue> Merge<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> primary,
        ConcurrentDictionary<TKey, TValue> secondary) where TKey : notnull
    {
        var result = new ConcurrentDictionary<TKey, TValue>();

        if (secondary.SafeAny())
        {
            foreach (var pair in secondary)
            {
                result.TryAdd(pair.Key, pair.Value);
            }
        }

        if (primary.SafeAny())
        {
            foreach (var pair in primary)
            {
                result.AddOrUpdate(pair.Key, pair.Value, (_, _) => pair.Value);
            }
        }

        return result;
    }

    /// <summary>
    ///     Merges two queues into a new queue.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queues.</typeparam>
    /// <param name="primary">The primary queue.</param>
    /// <param name="secondary">The secondary queue.</param>
    /// <returns>A new queue containing elements from both queues.</returns>
    public static Queue<T> Merge<T>(this Queue<T> primary, Queue<T> secondary)
    {
        var result = new Queue<T>();

        if (secondary.SafeAny())
        {
            foreach (var item in secondary)
            {
                result.Enqueue(item);
            }
        }

        if (primary.SafeAny())
        {
            foreach (var item in primary)
            {
                result.Enqueue(item);
            }
        }

        return result;
    }

    /// <summary>
    ///     Merges two stacks into a new stack.
    /// </summary>
    /// <typeparam name="T">The type of elements in the stacks.</typeparam>
    /// <param name="primary">The primary stack.</param>
    /// <param name="secondary">The secondary stack.</param>
    /// <returns>A new stack containing elements from both stacks.</returns>
    public static Stack<T> Merge<T>(this Stack<T> primary, Stack<T> secondary)
    {
        var result = new Stack<T>();

        // Note: We reverse the order here to maintain the original stack order
        if (primary.SafeAny())
        {
            foreach (var item in primary.Reverse())
            {
                result.Push(item);
            }
        }

        if (secondary.SafeAny())
        {
            foreach (var item in secondary.Reverse())
            {
                result.Push(item);
            }
        }

        return result;
    }

    /// <summary>
    ///     Merges two linked lists into a new linked list.
    /// </summary>
    /// <typeparam name="T">The type of elements in the linked lists.</typeparam>
    /// <param name="primary">The primary linked list.</param>
    /// <param name="secondary">The secondary linked list.</param>
    /// <returns>A new linked list containing elements from both lists.</returns>
    public static LinkedList<T> Merge<T>(this LinkedList<T> primary, LinkedList<T> secondary)
    {
        var result = new LinkedList<T>();

        if (secondary.SafeAny())
        {
            foreach (var item in secondary)
            {
                result.AddLast(item);
            }
        }

        if (primary.SafeAny())
        {
            foreach (var item in primary)
            {
                result.AddLast(item);
            }
        }

        return result;
    }
}