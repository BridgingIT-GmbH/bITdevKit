// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A mutable dictionary that provides safe (non-throwing)
/// access to values and optional case-insensitive keys.
/// </summary>
/// <typeparam name="TKey">The type of dictionary key.</typeparam>
/// <typeparam name="TValue">The type of dictionary value.</typeparam>
/// <remarks>
/// <para>
/// <see cref="SafeDictionary{TKey, TValue}"/> behaves similar to
/// <see cref="Dictionary{TKey, TValue}"/> but with two key differences:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// Accessing a missing key using the indexer returns
/// <c>default(TValue)</c> instead of throwing a
/// <see cref="KeyNotFoundException"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// When <typeparamref name="TKey"/> is a <see cref="string"/>,
/// lookups are case-insensitive by default.
/// </description>
/// </item>
/// </list>
/// <para>
/// The dictionary is fully mutable and suitable for scenarios where
/// configuration or runtime state must be safely modifiable.
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="SafeDictionary{TKey, TValue}"/> class
/// using the specified data source and optional comparer.
/// </remarks>
/// <param name="source">
/// Initial dictionary data or <c>null</c> to create an empty instance.
/// </param>
/// <param name="comparer">
/// Custom key comparer or <c>null</c> to use the default.
/// </param>
public sealed class SafeDictionary<TKey, TValue>(
    IDictionary<TKey, TValue> source,
    IEqualityComparer<TKey> comparer = null) : IDictionary<TKey, TValue>
{
    private readonly Dictionary<TKey, TValue> inner = new(
            source ?? new Dictionary<TKey, TValue>(),
            comparer ?? GetDefaultComparer());

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeDictionary{TKey, TValue}"/> class.
    /// </summary>
    public SafeDictionary()
        : this(new Dictionary<TKey, TValue>(GetDefaultComparer())) { }

    /// <summary>
    /// Provides the default comparer used when none is specified.
    /// </summary>
    private static IEqualityComparer<TKey> GetDefaultComparer()
        => typeof(TKey) == typeof(string)
            ? (IEqualityComparer<TKey>)StringComparer.OrdinalIgnoreCase
            : EqualityComparer<TKey>.Default;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <returns>
    /// The value associated with the key if it exists; otherwise,
    /// <c>default(TValue)</c>.
    /// </returns>
    /// <remarks>
    /// Setting a value for an existing key replaces it if present;
    /// adding a value for a new key inserts it.
    /// </remarks>
    public TValue this[TKey key]
    {
        get
        {
            this.inner.TryGetValue(key, out var value);
            return value;
        }
        set => this.inner[key] = value;
    }

    /// <inheritdoc/>
    public ICollection<TKey> Keys => this.inner.Keys;

    /// <inheritdoc/>
    public ICollection<TValue> Values => this.inner.Values;

    /// <inheritdoc/>
    public int Count => this.inner.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(TKey key, TValue value) => this.inner[key] = value;

    /// <inheritdoc/>
    public bool ContainsKey(TKey key) => this.inner.ContainsKey(key);

    /// <inheritdoc/>
    public bool Remove(TKey key) => this.inner.Remove(key);

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, out TValue value)
        => this.inner.TryGetValue(key, out value);

    /// <inheritdoc/>
    public void Add(KeyValuePair<TKey, TValue> item)
        => this.inner[item.Key] = item.Value;

    /// <inheritdoc/>
    public void Clear() => this.inner.Clear();

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<TKey, TValue> item)
        => this.inner.TryGetValue(item.Key, out var value) &&
           EqualityComparer<TValue>.Default.Equals(value, item.Value);

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        => ((IDictionary<TKey, TValue>)this.inner).CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<TKey, TValue> item)
        => this.inner.Remove(item.Key);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        => this.inner.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>
    /// Creates a read-only view of this dictionary.
    /// </summary>
    /// <returns>
    /// A new <see cref="SafeReadOnlyDictionary{TKey, TValue}"/> sharing
    /// the same data snapshot.
    /// </returns>
    public SafeReadOnlyDictionary<TKey, TValue> AsReadOnly()
        => new(this.inner, this.inner.Comparer);
}
