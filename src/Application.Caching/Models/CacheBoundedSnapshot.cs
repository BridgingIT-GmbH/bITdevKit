// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Copies bounded metadata lists without retaining a caller's mutable collection.</summary>
/// <example><code>var tags = CacheBoundedSnapshot.Copy(sourceTags, 64, nameof(sourceTags));</code></example>
public static class CacheBoundedSnapshot
{
    /// <summary>Copies references by index into a read-only array after checking the declared member limit.</summary>
    /// <typeparam name="T">The non-null metadata member type.</typeparam>
    /// <param name="source">The source list, or null to produce an empty snapshot.</param>
    /// <param name="maximum">The largest accepted member count.</param>
    /// <param name="parameter">The caller parameter name used in validation exceptions.</param>
    /// <returns>A read-only positional copy. Members themselves are not cloned.</returns>
    /// <exception cref="ArgumentException">The list exceeds the bound or contains a null member.</exception>
    public static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, int maximum, string parameter) where T : class
    {
        if (source is null)
        {
            return Array.Empty<T>();
        }

        if (source.Count > maximum)
        {
            throw new ArgumentException("The cache metadata collection exceeds its fixed bound.", parameter);
        }

        var result = new T[source.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = source[i] ?? throw new ArgumentException("Cache metadata members cannot be null.", parameter);
        }

        return Array.AsReadOnly(result);
    }
}