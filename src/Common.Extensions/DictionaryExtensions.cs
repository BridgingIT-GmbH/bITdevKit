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
        if (source is null || key is null)
        {
            return source;
        }

        source.Remove(key);
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

    /// <summary>
    /// Adds a key-value pair to the dictionary if the specified condition is true.
    /// If the dictionary is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to add to, or null.</param>
    /// <param name="key">The key of the item to add.</param>
    /// <param name="value">The value of the item to add.</param>
    /// <param name="condition">The condition that must be true to add the key-value pair.</param>
    /// <returns>The dictionary (for chaining if desired), or null if the dictionary was null.</returns>
    /// <example>
    /// var dict = new Dictionary<int, string>();
    /// dict.AddIf(1, "one", true); // Adds key-value pair
    /// dict = null;
    /// dict.AddIf(2, "two", true); // Does nothing and returns null silently
    /// </example>
    public static IDictionary<TKey, TValue> AddIf<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value,
        bool condition)
    {
        if (dictionary == null)
        {
            return null;
        }

        if (condition)
        {
            dictionary.Add(key, value);
        }
        return dictionary;
    }

    /// <summary>
    /// Adds a key-value pair to the dictionary if the specified predicate returns true for the value.
    /// If the dictionary is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to add to, or null.</param>
    /// <param name="key">The key of the item to add.</param>
    /// <param name="value">The value of the item to add.</param>
    /// <param name="predicate">The predicate that determines if the value should be added.</param>
    /// <returns>The dictionary (for chaining if desired), or null if the dictionary was null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null and the dictionary is not null.</exception>
    /// <example>
    /// var dict = new Dictionary<int, string>();
    /// dict.AddIf(1, "one", v => v.Length > 2); // Adds if value length > 2
    /// dict = null;
    /// dict.AddIf(2, "two", v => v.Length > 2); // Does nothing and returns null silently
    /// </example>
    public static IDictionary<TKey, TValue> AddIf<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value,
        Func<TValue, bool> predicate)
    {
        if (dictionary == null)
        {
            return null;
        }

        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (predicate(value))
        {
            dictionary.Add(key, value);
        }
        return dictionary;
    }

    /// <summary>
    /// Adds a key-value pair to the dictionary if the value is not null.
    /// If the dictionary is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to add to, or null.</param>
    /// <param name="key">The key of the item to add.</param>
    /// <param name="value">The value of the item to add.</param>
    /// <returns>The dictionary (for chaining if desired), or null if the dictionary was null.</returns>
    /// <example>
    /// var dict = new Dictionary<int, string>();
    /// string val = null;
    /// dict.AddIfNotNull(1, val); // Won’t add because value is null
    /// dict.AddIfNotNull(2, "two"); // Adds because value is not null
    /// dict = null;
    /// dict.AddIfNotNull(3, "three"); // Does nothing and returns null silently
    /// </example>
    public static IDictionary<TKey, TValue> AddIfNotNull<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value)
    {
        if (dictionary == null)
        {
            return null;
        }

        if (value != null)
        {
            dictionary.Add(key, value);
        }
        return dictionary;
    }

    /// <summary>
    /// Adds a range of key-value pairs to the dictionary if the specified condition is true.
    /// If the dictionary is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to add to, or null.</param>
    /// <param name="items">The key-value pairs to add.</param>
    /// <param name="condition">The condition that must be true to add the items.</param>
    /// <returns>The dictionary (for chaining if desired), or null if the dictionary was null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null and the dictionary is not null.</exception>
    /// <example>
    /// var dict = new Dictionary<int, string>();
    /// var items = new Dictionary<int, string> { { 1, "one" }, { 2, "two" } };
    /// dict.AddRangeIf(items, items.Any(kvp => kvp.Value.Length > 2)); // Adds all if any value length > 2
    /// dict = null;
    /// dict.AddRangeIf(items, true); // Does nothing and returns null silently
    /// </example>
    public static IDictionary<TKey, TValue> AddRangeIf<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        IEnumerable<KeyValuePair<TKey, TValue>> items,
        bool condition)
    {
        if (dictionary == null)
        {
            return null;
        }

        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (condition)
        {
            foreach (var item in items)
            {
                dictionary.Add(item.Key, item.Value);
            }
        }
        return dictionary;
    }

    /// <summary>
    /// Adds a key-value pair to the dictionary if the key doesn’t already exist.
    /// If the dictionary is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to add to, or null.</param>
    /// <param name="key">The key of the item to add.</param>
    /// <param name="value">The value of the item to add.</param>
    /// <returns>The dictionary (for chaining if desired), or null if the dictionary was null.</returns>
    /// <example>
    /// var dict = new Dictionary<int, string> { { 1, "one" } };
    /// dict.AddIfUnique(1, "uno"); // Won’t add because key 1 exists
    /// dict.AddIfUnique(2, "two"); // Adds because key 2 doesn’t exist
    /// dict = null;
    /// dict.AddIfUnique(3, "three"); // Does nothing and returns null silently
    /// </example>
    public static IDictionary<TKey, TValue> AddIfUnique<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value)
    {
        if (dictionary == null)
        {
            return null;
        }

        if (!dictionary.ContainsKey(key))
        {
            dictionary.Add(key, value);
        }
        return dictionary;
    }

    /// <summary>
    /// Adds a key-value pair to the dictionary if all specified conditions are true for the value.
    /// If the dictionary is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to add to, or null.</param>
    /// <param name="key">The key of the item to add.</param>
    /// <param name="value">The value of the item to add.</param>
    /// <param name="predicates">The predicates that must all return true. Adds if empty.</param>
    /// <returns>The dictionary (for chaining if desired), or null if the dictionary was null.</returns>
    /// <example>
    /// var dict = new Dictionary<int, string>();
    /// dict.AddIfAll(1, "one", v => v.Length > 2, v => v.StartsWith("o")); // Adds if both true
    /// dict = null;
    /// dict.AddIfAll(2, "two", v => v.Length > 2); // Does nothing and returns null silently
    /// </example>
    public static IDictionary<TKey, TValue> AddIfAll<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value,
        params Func<TValue, bool>[] predicates)
    {
        if (dictionary == null)
        {
            return null;
        }

        if (predicates.IsNullOrEmpty() || predicates.All(predicate => predicate(value)))
        {
            dictionary.Add(key, value);
        }
        return dictionary;
    }

    /// <summary>
    /// Adds a key-value pair to the dictionary if any specified condition is true for the value.
    /// If the dictionary is null, returns silently without adding.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to add to, or null.</param>
    /// <param name="key">The key of the item to add.</param>
    /// <param name="value">The value of the item to add.</param>
    /// <param name="predicates">The predicates, at least one of which must return true.</param>
    /// <returns>The dictionary (for chaining if desired), or null if the dictionary was null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicates"/> is null or empty and the dictionary is not null.</exception>
    /// <example>
    /// var dict = new Dictionary<int, string>();
    /// dict.AddIfAny(1, "one", v => v.Length > 2, v => v.Contains("n")); // Adds if either true
    /// dict = null;
    /// dict.AddIfAny(2, "two", v => v.Length > 2); // Does nothing and returns null silently
    /// </example>
    public static IDictionary<TKey, TValue> AddIfAny<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value,
        params Func<TValue, bool>[] predicates)
    {
        if (dictionary == null)
        {
            return null;
        }

        if (predicates.SafeAny(predicate => predicate(value)))
        {
            dictionary.Add(key, value);
        }
        return dictionary;
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