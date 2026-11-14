// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Bounds transfer buffers and occupied native resources.</summary>
/// <example><code>var options = new CacheStreamOptions();</code></example>
public sealed record CacheStreamOptions
{
    /// <summary>Gets transfer buffer capacity charged to active work.</summary>
    public int BufferSize { get; init; } = 64 * (int)CacheDefaults.KiB;

    /// <summary>Gets maximum transfer inactivity.</summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets absolute acquired-handle lifetime.</summary>
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromMinutes(5);
}