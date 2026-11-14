// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Checks real retained and pinned representation accounting through retirement and final release.</summary>
public sealed class CacheHotFootprintTests
{
    [Fact]
    public void Retire_PinnedGeneration_KeepsCapacityUntilActualFinalRelease()
    {
        var budget = new ResourceBudget("test", 4096);
        var footprint = new CacheHotFootprint();
        ResourceCharge.TryReserve(32, out var bookkeeping, budget).ShouldBeTrue();
        ResourceCharge.TryReserve(128, out var binaryCharge, budget).ShouldBeTrue();
        var sut = new CacheHotEntry(new(), DateTimeOffset.UtcNow, bookkeeping, footprint)
        {
            Binary = new(new byte[96], null, null, false, 128, binaryCharge)
        };
        sut.AddReference();
        using var first = new CacheHotEntryLease(sut, sut.Binary);
        sut.AddReference();
        using var second = new CacheHotEntryLease(sut, sut.Binary);
        footprint.Snapshot().PinnedBytes.ShouldBe(128);

        sut.Retire();
        first.Dispose();

        footprint.Snapshot().BinaryBytes.ShouldBe(128);
        footprint.Snapshot().PinnedBytes.ShouldBe(128);
        budget.Used.ShouldBe(160);
        second.Dispose();
        footprint.Snapshot().ShouldBe(new(0, 0, 0));
        budget.Used.ShouldBe(0);
    }

    [Fact]
    public void AttachDecoded_WhileBinaryPinned_AccountsBothRepresentationsOnce()
    {
        var budget = new ResourceBudget("test", 4096);
        var footprint = new CacheHotFootprint();
        ResourceCharge.TryReserve(32, out var bookkeeping, budget).ShouldBeTrue();
        ResourceCharge.TryReserve(128, out var binaryCharge, budget).ShouldBeTrue();
        ResourceCharge.TryReserve(256, out var decodedCharge, budget).ShouldBeTrue();
        var sut = new CacheHotEntry(new(), DateTimeOffset.UtcNow, bookkeeping, footprint)
        {
            Binary = new(new byte[96], null, null, false, 128, binaryCharge)
        };
        sut.AddReference();
        using var pin = new CacheHotEntryLease(sut, sut.Binary);

        sut.Decoded = new(null, null, "immutable", true, 256, decodedCharge);

        footprint.Snapshot().ShouldBe(new(128, 256, 384));
        pin.Dispose();
        footprint.Snapshot().ShouldBe(new(128, 256, 0));
        sut.Retire();
        footprint.Snapshot().ShouldBe(new(0, 0, 0));
        budget.Used.ShouldBe(0);
    }

    [Fact]
    public void Retire_UnpinnedGeneration_ReleasesCapacityOnce()
    {
        var budget = new ResourceBudget("test", 4096);
        var footprint = new CacheHotFootprint();
        ResourceCharge.TryReserve(32, out var bookkeeping, budget).ShouldBeTrue();
        ResourceCharge.TryReserve(256, out var decodedCharge, budget).ShouldBeTrue();
        var sut = new CacheHotEntry(new(), DateTimeOffset.UtcNow, bookkeeping, footprint)
        {
            Decoded = new(null, null, "immutable", true, 256, decodedCharge)
        };

        sut.Retire();
        sut.Retire();

        footprint.Snapshot().ShouldBe(new(0, 0, 0));
        budget.Used.ShouldBe(0);
    }
}