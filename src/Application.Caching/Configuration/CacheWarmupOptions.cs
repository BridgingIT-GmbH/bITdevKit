// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines optional predefined bounded startup or periodic warmup.</summary>
/// <example><code>var options = new CacheWarmupOptions();</code></example>
public sealed record CacheWarmupOptions
{
    /// <summary>Gets whether registered warmup work is enabled.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Gets trusted registered scoped warmup loader alias.</summary>
    public string Loader { get; init; } = null;

    /// <summary>Gets an optional code-registered scoped asynchronous key-source alias; null selects the configured items.</summary>
    public string KeySource { get; init; }

    /// <summary>Gets predefined bounded immutable warmup identities and owner dimensions.</summary>
    public IReadOnlyList<CacheItem> Items { get; init; } = [];

    /// <summary>Gets explicit periodic interval; null means startup only.</summary>
    public TimeSpan? Interval { get; init; } = null;
}