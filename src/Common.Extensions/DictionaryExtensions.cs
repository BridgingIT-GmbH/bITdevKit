// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static class DictionaryExtensions
{
    /// <summary>
    ///     Gets a value by key
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key.</param>
    public static TValue GetValue<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key)
    {
        if (source is null || key is null)
        {
            return default;
        }

        return source.TryGetValue(key, out var value) ? value : default;
    }

    /// <summary>
    ///     Gets a value by key
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key.</param>
    public static TValue GetValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, TKey key)
    {
        if (source is null || key is null)
        {
            return default;
        }

        return source.TryGetValue(key, out var value) ? value : default;
    }

    /// <summary>
    ///     Gets a value by key or the default value if key not found
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key.</param>
    /// <param name="defaultValue">The default value.</param>
    public static TValue GetValueOrDefault<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        TKey key,
        TValue defaultValue = default)
    {
        if (source is null || key is null)
        {
            return defaultValue;
        }

        return source.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    ///     Gets a value by key or the default value if key not found
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key.</param>
    /// <param name="defaultValue">The default value.</param>
    public static TValue GetValueOrDefault<TKey, TValue>(
        this IReadOnlyDictionary<TKey, TValue> source,
        TKey key,
        TValue defaultValue = default)
    {
        if (source is null || key is null)
        {
            return defaultValue;
        }

        return source.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    ///     Adds or updates the entry in the dictionary.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    [DebuggerStepThrough]
    public static IDictionary<TKey, TValue> AddOrUpdate<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        TKey key,
        TValue value)
    {
        //source ??= new Dictionary<TKey, TValue>();

        if (source is null || key is null)
        {
            return source;
        }

        if (source.ContainsKey(key))
        {
            source.Remove(key);
        }

        source.Add(key, value);

        return source;
    }

    /// <summary>
    ///     Adds the given <paramref name="items" /> to the given <paramref name="source" />.
    ///     <remarks>This method is used to duck-type <see cref="IDictionary{TKey, TValue}" /> with multiple pairs.</remarks>
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items to add.</param>
    [DebuggerStepThrough]
    public static IDictionary<TKey, TValue> AddOrUpdate<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        IDictionary<TKey, TValue> items)
    {
        if (source is null)
        {
            return source;
        }

        foreach (var item in items.SafeNull())
        {
            source.AddOrUpdate(item.Key, item.Value);
        }

        return source;
    }

    public static bool ContainsKeyIgnoreCase<TValue>(this IDictionary<string, TValue> source, string key)
    {
        if (source is null)
        {
            return false;
        }

        return source.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }
}