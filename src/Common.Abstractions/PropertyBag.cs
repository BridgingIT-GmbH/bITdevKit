// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// PropertyBag is a type-safe dictionary abstraction for key-value pairs, with
/// support for type conversion, change notification, and basic merging/cloning.
/// Strongly-typed keys are supported optionally via <see cref="PropertyBagKey{T}"/>.
/// </summary>
public class PropertyBag : IEnumerable<KeyValuePair<string, object>>
{
    private readonly Dictionary<string, object> items = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReaderWriterLockSlim @lock = new();

    /// <summary>
    /// Occurs after <see cref="Set(string, object)"/> adds or replaces an entry.
    /// </summary>
    public event Action<string, object> ItemChanged;

    /// <summary>
    /// Initializes an empty property bag that compares string keys without regard to case.
    /// </summary>
    public PropertyBag() { }

    /// <summary>
    /// Initializes a property bag with a shallow copy of the supplied entries, using case-insensitive string keys.
    /// </summary>
    /// <param name="items">The entries to copy, or <see langword="null"/> to create an empty bag.</param>
    public PropertyBag(IDictionary<string, object> items)
    {
        if (items != null)
        {
            foreach (var kv in items)
            {
                this.items[kv.Key] = kv.Value;
            }
        }
    }

    /// <summary>
    /// Gets the current entry count.
    /// </summary>
    public int Count
    {
        get
        {
            this.@lock.EnterReadLock();
            try
            {
                return this.items.Count;
            }
            finally
            {
                this.@lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Adds or replaces a value for a key.
    /// </summary>
    public void Add(string key, object value)
    {
        this.Set(key, value);
    }

    /// <summary>
    /// Set a value for a key.
    /// </summary>
    public void Set(string key, object value)
    {
        this.@lock.EnterWriteLock();
        try
        {
            this.items[key] = value;
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }

        this.ItemChanged?.Invoke(key, value);
    }

    /// <summary>
    /// Get the value for a key as object, or null if not found.
    /// </summary>
    public object Get(string key)
    {
        this.@lock.EnterReadLock();
        try
        {
            return this.items.TryGetValue(key, out var value) ? value : null;
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get the value for a key and convert it to the specified type using .To&lt;T&gt;().
    /// Returns default(T) if not found or conversion fails.
    /// </summary>
    public T Get<T>(string key, T defaultValue = default)
    {
        var value = this.Get(key);
        return value == null ? defaultValue : value.To(defaultValue: defaultValue);
    }

    /// <summary>
    /// Try to get the value for a key and convert it to the specified type.
    /// Returns true on success.
    /// </summary>
    public bool TryGet<T>(string key, out T value)
    {
        var raw = this.Get(key);
        if (raw != null)
        {
            return raw.TryTo(out value);
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Try to get the raw value for a key.
    /// </summary>
    public bool TryGetValue(string key, out object value)
    {
        this.@lock.EnterReadLock();
        try
        {
            return this.items.TryGetValue(key, out value);
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Remove a key from the bag.
    /// </summary>
    public bool Remove(string key)
    {
        this.@lock.EnterWriteLock();
        try
        {
            return this.items.Remove(key);
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Remove all keys matching the predicate.
    /// </summary>
    public void RemoveAll(Func<string, object, bool> predicate)
    {
        this.@lock.EnterWriteLock();
        try
        {
            foreach (var key in new List<string>(this.items.Keys))
            {
                if (predicate(key, this.items[key]))
                {
                    this.items.Remove(key);
                }
            }
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clear all entries.
    /// </summary>
    public void Clear()
    {
        this.@lock.EnterWriteLock();
        try
        {
            this.items.Clear();
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Check if the bag contains a key.
    /// </summary>
    public bool Contains(string key)
    {
        this.@lock.EnterReadLock();
        try
        {
            return this.items.ContainsKey(key);
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Indexer for object access.
    /// </summary>
    public object this[string key]
    {
        get => this.Get(key);
        set => this.Set(key, value);
    }

    /// <summary>
    /// Get all keys in the bag.
    /// </summary>
    public IEnumerable<string> Keys
    {
        get
        {
            this.@lock.EnterReadLock();
            try
            {
                return [.. this.items.Keys];
            }
            finally
            {
                this.@lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Get all values in the bag.
    /// </summary>
    public IEnumerable<object> Values
    {
        get
        {
            this.@lock.EnterReadLock();
            try
            {
                return [.. this.items.Values];
            }
            finally
            {
                this.@lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Clone the property bag.
    /// </summary>
    public PropertyBag Clone()
    {
        this.@lock.EnterReadLock();
        try
        {
            //return [.. this.items];
#pragma warning disable IDE0028 // Simplify collection initialization
            return new PropertyBag(this.items);
#pragma warning restore IDE0028 // Simplify collection initialization
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Merge another property bag into this one (overwriting existing keys).
    /// </summary>
    public void Merge(PropertyBag other)
    {
        if (other == null) return;
        this.@lock.EnterWriteLock();
        try
        {
            foreach (var kv in other)
                this.items[kv.Key] = kv.Value;
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        this.@lock.EnterReadLock();
        try
        {
            foreach (var kv in this.items)
                yield return kv;
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>Associates a value with a strongly typed key and raises <see cref="ItemChanged"/>.</summary>
    /// <typeparam name="T">The value type represented by the key.</typeparam>
    /// <param name="key">The strongly typed key whose name identifies the entry.</param>
    /// <param name="value">The value to store.</param>
    public void Set<T>(PropertyBagKey<T> key, T value) => this.Set(key.Name, value);

    /// <summary>Gets and converts the value associated with a strongly typed key.</summary>
    /// <typeparam name="T">The requested value type.</typeparam>
    /// <param name="key">The strongly typed key whose name identifies the entry.</param>
    /// <param name="defaultValue">The value returned when the entry is absent or cannot be converted.</param>
    /// <returns>The converted entry value, or <paramref name="defaultValue"/> when retrieval fails.</returns>
    public T Get<T>(PropertyBagKey<T> key, T defaultValue = default) => this.Get(key.Name, defaultValue);

    /// <summary>Attempts to get and convert the value associated with a strongly typed key.</summary>
    /// <typeparam name="T">The requested value type.</typeparam>
    /// <param name="key">The strongly typed key whose name identifies the entry.</param>
    /// <param name="value">Receives the converted value on success; otherwise, the default value of <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> when the entry exists and conversion succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryGet<T>(PropertyBagKey<T> key, out T value) => this.TryGet(key.Name, out value);

    /// <summary>Determines whether an entry exists for a strongly typed key.</summary>
    /// <typeparam name="T">The value type represented by the key.</typeparam>
    /// <param name="key">The strongly typed key whose name identifies the entry.</param>
    /// <returns><see langword="true"/> when the key exists; otherwise, <see langword="false"/>.</returns>
    public bool Contains<T>(PropertyBagKey<T> key) => this.Contains(key.Name);

    /// <summary>Removes the entry associated with a strongly typed key.</summary>
    /// <typeparam name="T">The value type represented by the key.</typeparam>
    /// <param name="key">The strongly typed key whose name identifies the entry.</param>
    /// <returns><see langword="true"/> when an entry was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove<T>(PropertyBagKey<T> key) => this.Remove(key.Name);
}

/// <summary>
/// Strongly-typed key for PropertyBag (optional).
/// </summary>
public sealed class PropertyBagKey<T>(string name)
{
    /// <summary>Gets the string name used to address the property-bag entry.</summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <inheritdoc/>
    public override string ToString() => this.Name;
}
