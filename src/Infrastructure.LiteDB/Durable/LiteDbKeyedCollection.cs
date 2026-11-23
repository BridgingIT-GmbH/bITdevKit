// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Provides command-local keyed access to separately persisted native records.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The detached value type.</typeparam>
/// <param name="database">The owning feature command executor.</param>
/// <param name="collectionName">The declared collection name.</param>
/// <param name="ignoreCase">Whether string identity lookup ignores ordinal casing.</param>
public sealed class LiteDbKeyedCollection<TKey, TValue>(LiteDbFeatureDatabase database, string collectionName, bool ignoreCase = false) : IEnumerable<KeyValuePair<TKey, TValue>>
{
    /// <summary>Gets or sets the command-local model associated with a key.</summary>
    /// <param name="key">The logical identity.</param>
    /// <returns>The stored model.</returns>
    public TValue this[TKey key]
    {
        get => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
        set => database.Write(collectionName, this.Normalize(key), key, value);
    }

    /// <summary>Gets a bounded snapshot of the collection's detached values.</summary>
    public IReadOnlyCollection<TValue> Values => this.Select(pair => pair.Value).ToArray();

    /// <summary>Gets a bounded snapshot of the collection's original keys.</summary>
    public IReadOnlyCollection<TKey> Keys => database.Keys<TKey>(collectionName).Select(row => row.Key).ToArray();

    /// <summary>Gets the bounded logical record count.</summary>
    public int Count => this.Keys.Count;

    /// <summary>Reads a logical record.</summary>
    /// <param name="key">The logical key.</param>
    /// <param name="value">The detached value when present.</param>
    /// <returns>Whether the record exists.</returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        value = database.Read<TValue>(collectionName, this.Normalize(key), out var found);
        return found;
    }

    /// <summary>Checks a key without adding or updating records.</summary>
    /// <param name="key">The logical key.</param>
    /// <returns>Whether it exists.</returns>
    public bool ContainsKey(TKey key) => this.TryGetValue(key, out _);

    /// <summary>Inserts a key only when no record currently owns it.</summary>
    /// <param name="key">The logical key.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>Whether insertion succeeded.</returns>
    public bool TryAdd(TKey key, TValue value)
    {
        if (this.ContainsKey(key)) { return false; }

        this[key] = value;
        return true;
    }

    /// <summary>Returns an existing value or atomically records the supplied factory's value.</summary>
    /// <param name="key">The logical key.</param>
    /// <param name="factory">The command-local synchronous value factory.</param>
    /// <returns>The existing or newly stored value.</returns>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (this.TryGetValue(key, out var value)) { return value; }

        value = factory(key);
        this[key] = value;
        return value;
    }

    /// <summary>Removes a key and returns its prior value.</summary>
    /// <param name="key">The logical key.</param>
    /// <param name="value">The prior detached value.</param>
    /// <returns>Whether a record was removed.</returns>
    public bool TryRemove(TKey key, out TValue value) => this.TryGetValue(key, out value) && this.Remove(key);

    /// <summary>Removes a logical record.</summary>
    /// <param name="key">The logical key.</param>
    /// <returns>Whether removal succeeded.</returns>
    public bool Remove(TKey key) => database.Remove(collectionName, this.Normalize(key));

    /// <summary>Inserts a record or throws when the key already exists.</summary>
    /// <param name="key">The logical key.</param>
    /// <param name="value">The model to persist.</param>
    public void Add(TKey key, TValue value)
    {
        if (!this.TryAdd(key, value)) { throw new ArgumentException("The durable key already exists.", nameof(key)); }
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        var items = database.Keys<TKey>(collectionName).Select(row => new KeyValuePair<TKey, TValue>(row.Key, database.Read<TValue>(collectionName, row.Id, out _))).ToArray();
        return ((IEnumerable<KeyValuePair<TKey, TValue>>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private string Normalize(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var normalized = ignoreCase && key is string text ? JsonSerializer.Serialize(text.ToUpperInvariant()) : JsonSerializer.Serialize(key);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
