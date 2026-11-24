// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Enumerates repeatable predefined warm-up keys inside one cache-owned bounded pass.</summary>
/// <remarks>The worker disposes enumeration at pause, cancellation, completion, or budget exhaustion; it never retains a scoped cursor between passes.</remarks>
/// <example><code>await foreach (var item in source.ReadAsync(context, cancellationToken)) { Inspect(item.Key); }</code></example>
public interface ICacheWarmupSource
{
    /// <summary>Produces only keys approved for the declared collection; ordinary cache loading handles every returned item.</summary>
    /// <param name="context">The declared scope and fixed new-work deadline.</param>
    /// <param name="cancellationToken">The owned pass cancellation token.</param>
    /// <returns>A scoped asynchronous key sequence. Consumers enforce their finite item and work limits.</returns>
    IAsyncEnumerable<CacheItem> ReadAsync(CacheWarmupContext context, CancellationToken cancellationToken = default);
}

/// <summary>Describes a bounded warm-up enumeration without retaining a request principal or source scope.</summary>
/// <param name="Namespace">The configured cache namespace.</param>
/// <param name="Collection">The declared collection name.</param>
/// <param name="Deadline">The fixed deadline for admitting more keys.</param>
/// <example><code>var scopeName = context.Collection;</code></example>
public sealed record CacheWarmupContext(string Namespace, string Collection, DateTimeOffset Deadline);