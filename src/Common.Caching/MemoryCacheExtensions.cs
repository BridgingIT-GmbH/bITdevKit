// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;

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

    public static ICollection GetKeys(this IMemoryCache memoryCache)
    {
        return GetEntries((MemoryCache)memoryCache).Keys;
    }

    public static IEnumerable<T> GetKeys<T>(this IMemoryCache memoryCache)
    {
        return memoryCache.GetKeys().OfType<T>();
    }

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