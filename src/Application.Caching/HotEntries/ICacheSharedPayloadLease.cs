// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Optionally exposes immutable provider-owned bytes without keeping an ordinary provider reader admission occupied.</summary>
/// <example><code>using var shared = (handle as ICacheProviderSharedReadHandle)?.RetainSharedPayload();</code></example>
public interface ICacheProviderSharedReadHandle : ICacheProviderReadHandle
{
    /// <summary>Acquires independent immutable payload ownership, or returns null when safe sharing is unavailable.</summary>
    /// <remarks>The returned ownership must keep its original provider/process resident charge until its final reference ends.</remarks>
    ICacheSharedPayloadLease RetainSharedPayload();
}

/// <summary>Retains an already resident immutable persisted representation, including its original aggregate memory charge.</summary>
/// <example><code>using var stream = shared.OpenRead();</code></example>
public interface ICacheSharedPayloadLease : IDisposable
{
    /// <summary>Gets the conservative actual allocated capacity, including provider payload bookkeeping.</summary>
    long CapacityBytes { get; }

    /// <summary>Opens an independently positioned read-only raw snapshot. The stream owns its own payload reference.</summary>
    Stream OpenRead();
}