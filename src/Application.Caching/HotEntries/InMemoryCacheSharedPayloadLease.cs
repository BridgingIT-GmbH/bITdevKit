// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Shares an in-memory provider's frozen payload without copying buffers or occupying native reader admission.</summary>
/// <example><code>using var shared = ((ICacheProviderSharedReadHandle)handle).RetainSharedPayload();</code></example>
public sealed class InMemoryCacheSharedPayloadLease : ICacheSharedPayloadLease
{
    private readonly Lock gate = new();
    private InMemoryCachePayload payload;

    /// <summary>Pins an existing owned immutable InMemory payload without duplicating its byte-capacity charge.</summary>
    public InMemoryCacheSharedPayloadLease(InMemoryCachePayload payload)
    {
        payload.AddReference();
        this.payload = payload;
        this.CapacityBytes = payload.ResidentCapacity;
    }

    /// <inheritdoc />
    public long CapacityBytes { get; }

    /// <inheritdoc />
    public Stream OpenRead()
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.payload is null, this);
            return this.payload.OpenRead();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this.gate)
        {
            this.payload?.Release();
            this.payload = null;
        }
    }
}