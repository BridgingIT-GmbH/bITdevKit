// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
///     Provides key enumeration and pattern-based removal for the built-in <see cref="MemoryCache"/> implementation.
/// </summary>
/// <remarks>
///     Key enumeration relies on private implementation details of <see cref="MemoryCache"/> and requires the supplied
///     <see cref="IMemoryCache"/> to be a <see cref="MemoryCache"/> instance.
/// </remarks>
public static class MemoryCacheExtensions
{
    private static readonly Func<MemoryCache, IDictionary> GetEntries =
        Assembly.GetAssembly(typeof(MemoryCache)).GetName().Version.Major < 7
            ? cache => (IDictionary)GetEntries6.Value(cache)
            : cache => GetEntries7.Value(GetCoherentState.Value(cache));

    // Microsoft.Extensions.Caching.Memory_6_OR_OLDER
    private static readonly Lazy<Func<MemoryCache, object>> GetEntries6 = new(() =>
        (Func<MemoryCache, object>)Delegate.CreateDelegate(typeof(Func<MemoryCache, object>),
            typeof(MemoryCache).GetProperty("EntriesCollection", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetGetMethod(true),
            true));

    // Microsoft.Extensions.Caching.Memory_7_OR_NEWER
    private static readonly Lazy<Func<MemoryCache, object>> GetCoherentState = new(() =>
        ReflectionHelper.CreateGetter<MemoryCache, object>(typeof(MemoryCache).GetField("_coherentState",
            BindingFlags.NonPublic | BindingFlags.Instance)));

    // TODO: .NET 8 use new way for reflection (AOT safe) > https://steven-giesel.com/blogPost/05ecdd16-8dc4-490f-b1cf-780c994346a4

    private static readonly Lazy<Func<object, IDictionary>> GetEntries7 = new(() =>
        ReflectionHelper.CreateGetter<object, IDictionary>(typeof(MemoryCache)
            .GetNestedType("CoherentState", BindingFlags.NonPublic)
            .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)));

    /// <summary>
    ///     Gets all keys currently stored by a memory cache.
    /// </summary>
    /// <param name="memoryCache">The memory cache to inspect.</param>
    /// <returns>The cache's key collection.</returns>
    public static ICollection GetKeys(this IMemoryCache memoryCache)
    {
        return GetEntries((MemoryCache)memoryCache).Keys;
    }

    /// <summary>
    ///     Gets the cache keys assignable to a specified type.
    /// </summary>
    /// <typeparam name="T">The key type to select.</typeparam>
    /// <param name="memoryCache">The memory cache to inspect.</param>
    /// <returns>The cache keys of type <typeparamref name="T"/>.</returns>
    public static IEnumerable<T> GetKeys<T>(this IMemoryCache memoryCache)
    {
        return memoryCache.GetKeys().OfType<T>();
    }

    /// <summary>
    ///     Removes entries whose string keys start with a case-sensitive prefix.
    /// </summary>
    /// <param name="source">The memory cache to modify.</param>
    /// <param name="key">The key prefix. Null and empty values are ignored.</param>
    public static void RemoveStartsWith(this IMemoryCache source, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        var keys = source.GetKeys<string>();
        if (keys is not null)
        {
            foreach (var foundKey in keys)
            {
                if (foundKey.StartsWith(key))
                {
                    source.Remove(foundKey);
                }
            }
        }
    }

    /// <summary>
    ///     Removes entries whose string keys contain a case-sensitive value.
    /// </summary>
    /// <param name="source">The memory cache to modify.</param>
    /// <param name="key">The key fragment. Null and empty values are ignored.</param>
    public static void RemoveContains(this IMemoryCache source, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        var keys = source.GetKeys<string>();
        if (keys is not null)
        {
            foreach (var foundKey in keys)
            {
                if (foundKey.Contains(key))
                {
                    source.Remove(foundKey);
                }
            }
        }
    }
}
