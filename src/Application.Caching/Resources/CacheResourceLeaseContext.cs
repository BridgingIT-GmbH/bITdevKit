// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Retains caller-owned cache resource accounts through the shared ambient lease context.</summary>
/// <remarks>Scopes grant no admission. Dispose scopes in reverse creation order and transfer account ownership only once.</remarks>
/// <example><code>using var scope = CacheResourceLeaseContext.Begin(decodedGraphCharge); await collection.SetAsync(item, value);</code></example>
public static class CacheResourceLeaseContext
{
    /// <summary>Takes ownership of a resource account until this scope and all retained captures end.</summary>
    /// <param name="resource">The sole-owned account, or null to leave the current context unchanged.</param>
    /// <returns>A restoring scope, or null when no account was supplied.</returns>
    /// <example><code>using var scope = CacheResourceLeaseContext.Begin(reservation);</code></example>
    public static IDisposable Begin(IDisposable resource) => ResourceLeaseContext.Begin(resource);

    /// <summary>Captures the current live account without renewing admission or changing the ambient scope.</summary>
    /// <returns>An independently disposable retained reference, or null when no live account remains.</returns>
    /// <example><code>using var captured = CacheResourceLeaseContext.Capture(); await actualNativeWork;</code></example>
    public static IDisposable Capture() => ResourceLeaseContext.Capture();
}