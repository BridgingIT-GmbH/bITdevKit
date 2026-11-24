// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Accounts retained representation capacity from actual attach, pin and last-release events without scanning the hot index.</summary>
/// <remarks>Binary capacity is exact. Decoded capacity follows the contract's conservative graph estimate. Pinned bytes count each protected generation once, including retired generations.</remarks>
/// <example><code>var snapshot = diagnostics.ForCollection("cards").HotFootprint.Snapshot();</code></example>
public sealed class CacheHotFootprint
{
    private long binary;
    private long decoded;
    private long pinned;

    /// <summary>Accounts a binary representation attach or actual final release.</summary>
    /// <param name="bytes">The retained capacity delta, negative only after actual release.</param>
    public void ChangeBinary(long bytes) => Change(ref this.binary, bytes);

    /// <summary>Accounts a decoded graph attach or actual final release using its declared estimate.</summary>
    /// <param name="bytes">The retained conservative capacity delta.</param>
    public void ChangeDecoded(long bytes) => Change(ref this.decoded, bytes);

    /// <summary>Accounts the first or last external pin on a generation, independent of index retirement.</summary>
    /// <param name="bytes">The protected representation-capacity delta, counted once per pinned generation.</param>
    public void ChangePinned(long bytes) => Change(ref this.pinned, bytes);

    /// <summary>Reads current node-local retained capacities without acquiring provider, index or entry locks.</summary>
    /// <returns>Current binary, decoded and uniquely pinned representation capacities.</returns>
    public CacheHotFootprintSnapshot Snapshot() => new(Interlocked.Read(ref this.binary), Interlocked.Read(ref this.decoded), Interlocked.Read(ref this.pinned));

    private static void Change(ref long location, long delta)
    {
        while (true)
        {
            var before = Interlocked.Read(ref location);
            var after = delta >= 0 ? before > long.MaxValue - delta ? long.MaxValue : before + delta :
                delta == long.MinValue || before < -delta ? 0 : before + delta;
            if (Interlocked.CompareExchange(ref location, after, before) == before) { return; }
        }
    }
}

/// <summary>Reports representation capacities retained by a single declared collection in this process.</summary>
/// <param name="BinaryBytes">Exact retained binary capacity, including retired generations until their last pin ends.</param>
/// <param name="DecodedBytes">Retained decoded graph capacity from the registered conservative estimator.</param>
/// <param name="PinnedBytes">Binary capacity plus decoded estimates protected by at least one external pin, counted once per generation.</param>
/// <remarks>These are attribution views within combined hot-resident usage and must not be added again to total resident bytes.</remarks>
/// <example><code>ShowCapacity(snapshot.BinaryBytes, snapshot.DecodedBytes, snapshot.PinnedBytes);</code></example>
public sealed record CacheHotFootprintSnapshot(long BinaryBytes, long DecodedBytes, long PinnedBytes);