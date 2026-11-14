// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records bounded payload buffers and occupied transfer lifetimes.</summary>
/// <example><code>streaming.BufferSize(checked((int)ByteSize.Kilobytes(32))).Lifetime(TimeSpan.FromMinutes(2));</code></example>
public sealed class CacheStreamingBuilder : PatchOptionsBuilder<CacheStreamOptions, CacheStreamingBuilder>
{
    /// <summary>Sets the buffer bytes reserved for each transfer.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>streaming.BufferSize(checked((int)ByteSize.Kilobytes(32)));</code></example>
    public CacheStreamingBuilder BufferSize(int value) => this.With(options => options.BufferSize, value);

    /// <summary>Sets the transfer inactivity timeout.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>streaming.IdleTimeout(TimeSpan.FromSeconds(15));</code></example>
    public CacheStreamingBuilder IdleTimeout(TimeSpan value) => this.With(options => options.IdleTimeout, value);

    /// <summary>Sets the absolute acquired-handle lifetime.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>streaming.Lifetime(TimeSpan.FromMinutes(2));</code></example>
    public CacheStreamingBuilder Lifetime(TimeSpan value) => this.With(options => options.Lifetime, value);
}