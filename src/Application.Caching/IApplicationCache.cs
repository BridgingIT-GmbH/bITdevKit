// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Resolves precomposed collection handles from the immutable startup catalog without lookup I/O.</summary>
/// <remarks>Resolution performs no source execution or provider I/O. The returned collection handle can be reused across requests and preserves its declared value contract.</remarks>
/// <example><code>var cards = cache.GetCollection&lt;ProductCard&gt;("product-cards");</code></example>
public interface IApplicationCache
{
    /// <summary>Resolves the reusable handle for a declared typed collection.</summary>
    /// <typeparam name="T">The exact value type declared for the collection at startup.</typeparam>
    /// <param name="name">The declared collection name, compared using invariant case equivalence.</param>
    /// <returns>The precomposed typed collection handle. Resolution does not read or load an entry.</returns>
    /// <exception cref="ArgumentException">The name is invalid or undeclared, or the collection does not declare the requested typed contract.</exception>
    /// <example><code>var cards = cache.GetCollection&lt;ProductCard&gt;("product-cards");</code></example>
    ICacheCollection<T> GetCollection<T>(string name);

    /// <summary>Resolves the reusable handle for a declared raw binary collection.</summary>
    /// <param name="name">The declared collection name, compared using invariant case equivalence.</param>
    /// <returns>The precomposed raw binary collection handle. Resolution does not open a stream.</returns>
    /// <exception cref="ArgumentException">The name is invalid or undeclared, or the collection declares a typed contract instead of raw binary content.</exception>
    /// <example><code>var reports = cache.GetBinaryCollection("reports");</code></example>
    ICacheBinaryCollection GetBinaryCollection(string name);
}