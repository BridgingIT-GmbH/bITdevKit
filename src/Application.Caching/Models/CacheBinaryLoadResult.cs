// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Distinguishes a successfully written binary payload, including empty bytes, from confirmed source absence.</summary>
/// <example><code>return CacheBinaryLoadResult.NotFound();</code></example>
public sealed record CacheBinaryLoadResult
{
    /// <summary>Creates a successful binary payload result. Publication still must be confirmed before delivery.</summary>
    public CacheBinaryLoadResult(CacheEntryDescriptor descriptor = null)
    {
        this.Descriptor = descriptor ?? new();
    }

    /// <summary>Gets source-produced owner metadata.</summary>
    public CacheEntryDescriptor Descriptor { get; }

    /// <summary>Gets whether the source explicitly confirmed absence. No payload stream is returned for absence.</summary>
    public bool IsNotFound { get; private init; }

    /// <summary>Creates metadata-only confirmed source absence, independently of zero-length successful payloads.</summary>
    public static CacheBinaryLoadResult NotFound(CacheEntryDescriptor descriptor = null) =>
        new(descriptor ?? new() { Priority = CacheEvictionPriority.Low }) { IsNotFound = true };
}