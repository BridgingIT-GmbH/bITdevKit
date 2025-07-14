// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections;
using System.Collections.Concurrent;

/// <summary>
/// Defines a cache for mapping request/notification handler interfaces to their concrete types.
/// </summary>
public interface IHandlerCache : IReadOnlyDictionary<Type, Type>
{
    /// <summary>
    /// Attempts to add a handler type mapping to the cache.
    /// </summary>
    /// <param name="key">The request handler interface type.</param>
    /// <param name="value">The concrete handler type.</param>
    /// <returns>True if the mapping was added; otherwise, false.</returns>
    bool TryAdd(Type key, Type value);
}

/// <summary>
/// Implements <see cref="IHandlerCache"/> using a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
public class HandlerCache : IHandlerCache
{
    private readonly ConcurrentDictionary<Type, Type> cache;

    public HandlerCache()
    {
        this.cache = [];
    }

    public Type this[Type key] => this.cache[key];

    public IEnumerable<Type> Keys => this.cache.Keys;

    public IEnumerable<Type> Values => this.cache.Values;

    public int Count => this.cache.Count;

    public bool ContainsKey(Type key) => this.cache.ContainsKey(key);

    public bool TryGetValue(Type key, out Type value) => this.cache.TryGetValue(key, out value);

    public bool TryAdd(Type key, Type value) => this.cache.TryAdd(key, value);

    public IEnumerator<KeyValuePair<Type, Type>> GetEnumerator() => this.cache.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
