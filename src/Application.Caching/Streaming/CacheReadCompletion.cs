// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reports payload validation separately from destination/transport success.</summary>
public enum CacheReadCompletion
{
    /// <summary>No terminal read outcome is known yet.</summary>
    Pending,

    /// <summary>The full logical representation reached EOF and passed integrity validation.</summary>
    Validated,

    /// <summary>The declared partial range completed; full-entry validation is not implied.</summary>
    RangeCompleted,

    /// <summary>Consumption stopped without full validation.</summary>
    Incomplete
}

/// <summary>Requests explicit read capabilities; unsupported combinations fail before payload delivery.</summary>
/// <param name="ExpectedVersion">An optional version that must match this independent acquisition.</param>
/// <param name="RequireSeek">Whether seeking is required.</param>
/// <param name="Offset">Optional inclusive logical-byte range start.</param>
/// <param name="Length">Optional nonnegative range length.</param>
/// <example><code>var options = new CacheReadOptions(lookup.Version, Offset: 0, Length: 1024);</code></example>
public sealed record CacheReadOptions(
    CacheEntryVersion? ExpectedVersion = null,
    bool RequireSeek = false,
    long? Offset = null,
    long? Length = null);