// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Exposes bounded named-provider breaker state independently of provider correctness contracts.</summary>
/// <example><code>var state = provider is ICacheProviderCircuitState circuit ? circuit.Snapshot() : null;</code></example>
public interface ICacheProviderCircuitState
{
    /// <summary>Reads current process-local circuit observations without probing or opening the provider.</summary>
    /// <returns>The current circuit state and finite cooldown.</returns>
    CacheProviderBreakerSnapshot Snapshot();
}