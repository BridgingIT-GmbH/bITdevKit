// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Exposes the owned source scope without retaining the initiating request's lifetime.</summary>
/// <param name="Identity">The complete identity whose source work is owned.</param>
/// <param name="Item">The owner's captured key and known dependency snapshot.</param>
/// <param name="Services">The cache-owned dependency scope for this execution.</param>
/// <param name="Deadline">The fixed overall execution deadline, including publication.</param>
/// <example><code>var source = context.Services.GetService(typeof(IProductSource));</code></example>
public sealed record CacheLoadContext(
    CacheEntryIdentity Identity,
    CacheItem Item,
    IServiceProvider Services,
    DateTimeOffset Deadline);

/// <summary>Bounds an operation choice without overriding finalized collection limits.</summary>
/// <param name="Mode">Normal lookup or typed-only bypass.</param>
/// <example><code>var options = new CacheAccessOptions(CacheAccessMode.Bypass);</code></example>
/// <param name="Background">Whether this cache-owned background call must avoid foreground activity and use background source sublimits.</param>
/// <param name="ExpectedVersion">The selected version required by a conditional refresh; null leaves explicit foreground refresh unchanged.</param>
public sealed record CacheAccessOptions(CacheAccessMode Mode = CacheAccessMode.Normal, bool Background = false, CacheEntryVersion? ExpectedVersion = null);