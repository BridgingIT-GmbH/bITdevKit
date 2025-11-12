// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace System.Collections.Generic;

using System.Collections;

/// <summary>
/// A read-only, safe, and case-insensitive dictionary implementation.
/// </summary>
/// <typeparam name="TKey">The type of dictionary key.</typeparam>
/// <typeparam name="TValue">The type of dictionary value.</typeparam>
/// <remarks>
/// <para>
/// This dictionary is designed for configuration scenarios where missing
/// keys should not throw exceptions and where key lookups
/// should, by default, be case-insensitive for string keys.
/// </para>
/// <para>
/// It implements <see cref="IReadOnlyDictionary{TKey,TValue}"/> so the
/// dictionary can be safely exposed without allowing external mutation.
/// However, it includes a public <see cref="Add"/> method and a
/// parameterless constructor so that the
/// <see href="https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration">.NET
/// configuration binder</see> can populate it directly during appsettings
/// binding.
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the
/// <see cref="SafeReadOnlyDictionary{TKey, TValue}"/> class using the
/// specified data source and optional comparer.
/// </remarks>
/// <param name="source">
/// Initial dictionary data or <c>null</c> to create an empty instance.
/// </param>
/// <param name="comparer">
/// Custom key comparer or <c>null</c> to use the default.
/// </param>
public sealed class SafeReadOnlyDictionary<TKey, TValue>(
    IDictionary<TKey, TValue> source,
    IEqualityComparer<TKey> comparer = null)
    : IReadOnlyDictionary<TKey, TValue>
{
    private readonly Dictionary<TKey, TValue> inner = new(
            source ?? new Dictionary<TKey, TValue>(),
            comparer ?? GetDefaultComparer());

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SafeReadOnlyDictionary{TKey, TValue}"/> class using the
    /// default (case-insensitive for strings) comparer.
    /// </summary>
    public SafeReadOnlyDictionary()
        : this(new Dictionary<TKey, TValue>(GetDefaultComparer())) { }

    /// <summary>
    /// Provides the default comparer used when none is specified.
    /// </summary>
    /// <remarks>
    /// If <typeparamref name="TKey"/> is a <see cref="string"/>, the
    /// comparer is <see cref="StringComparer.OrdinalIgnoreCase"/>;
    /// otherwise, it uses <see cref="EqualityComparer{T}.Default"/>.
    /// </remarks>
    private static IEqualityComparer<TKey> GetDefaultComparer()
        => typeof(TKey) == typeof(string)
            ? (IEqualityComparer<TKey>)StringComparer.OrdinalIgnoreCase
            : EqualityComparer<TKey>.Default;

    /// <summary>
    /// Adds or updates an entry in the dictionary.
    /// </summary>
    /// <param name="key">The key of the entry to add.</param>
    /// <param name="value">The value associated with the key.</param>
    /// <remarks>
    /// This method is public primarily to support population by the .NET
    /// configuration binder. External consumers should treat this class as
    /// read-only.
    /// </remarks>
    public void Add(TKey key, TValue value) => this.inner[key] = value;

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The lookup key.</param>
    /// <returns>
    /// The value if found; otherwise, <c>default(TValue)</c> (typically
    /// <c>null</c> for reference types).
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="Dictionary{TKey, TValue}"/>, this indexer never
    /// throws a <see cref="KeyNotFoundException"/>.
    /// </remarks>
    public TValue this[TKey key]
    {
        get
        {
            this.inner.TryGetValue(key, out var value);
            return value;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<TKey> Keys => this.inner.Keys;

    /// <inheritdoc/>
    public IEnumerable<TValue> Values => this.inner.Values;

    /// <inheritdoc/>
    public int Count => this.inner.Count;

    /// <inheritdoc/>
    public bool ContainsKey(TKey key) => this.inner.ContainsKey(key);

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, out TValue value)
        => this.inner.TryGetValue(key, out value);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        => this.inner.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
