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
    public event Action<string, object> ItemChanged;

    public PropertyBag() { }

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
            return raw.TryTo<T>(out value);
        }
        value = default;
        return false;
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

    public void Set<T>(PropertyBagKey<T> key, T value) => this.Set(key.Name, value);

    public T Get<T>(PropertyBagKey<T> key, T defaultValue = default) => this.Get(key.Name, defaultValue);

    public bool TryGet<T>(PropertyBagKey<T> key, out T value) => this.TryGet(key.Name, out value);

    public bool Contains<T>(PropertyBagKey<T> key) => this.Contains(key.Name);

    public bool Remove<T>(PropertyBagKey<T> key) => this.Remove(key.Name);
}

/// <summary>
/// Strongly-typed key for PropertyBag (optional).
/// </summary>
public sealed class PropertyBagKey<T>(string name)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public override string ToString() => this.Name;
}