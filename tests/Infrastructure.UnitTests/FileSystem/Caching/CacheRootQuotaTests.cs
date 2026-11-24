// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class CacheRootQuotaTests
{
    [Fact]
    public void PreparedPublication_ReservesEntryCountAndStorageUntilItsExactFinalization()
    {
        var state = new CacheRootQuotaState { StoreId = Guid.NewGuid().ToString("N"), MaximumBytes = 1000, MaximumEntries = 1, WorkspaceBytes = 100 };
        var change = new CacheQuotaChange(CurrentBytes: 200, MetadataBytes: 50, Entries: 1);
        var prepared = state.Reserve(300).Prepare(change);

        prepared.Snapshot(10).CurrentEntries.ShouldBe(1);
        prepared.Snapshot(10).IsExact.ShouldBeFalse();
        prepared.ReservedBytes.ShouldBe(300);
        Should.Throw<CacheTransferException>(() => prepared.Prepare(new(Entries: 1))).Code.ShouldBe(CacheErrorCode.Capacity);
        var committed = prepared.Confirm(change, 300);

        committed.ReservedBytes.ShouldBe(0);
        committed.CurrentEntries.ShouldBe(1);
        committed.CurrentBytes.ShouldBe(200);
        committed.PreparedOperations.ShouldBe(0);
        committed.Snapshot(10).IsExact.ShouldBeTrue();
    }

    [Fact]
    public void StagingAndRetiredPayload_ContinueToConsumeSharedCapacityUntilActuallyReleased()
    {
        var state = new CacheRootQuotaState { StoreId = Guid.NewGuid().ToString("N"), MaximumBytes = 1000, MaximumEntries = 2,
            WorkspaceBytes = 100, CurrentBytes = 300, CurrentEntries = 1, MetadataBytes = 50 };
        state.Validate(state.StoreId);
        var staged = state.Reserve(300);
        var replaced = staged.Apply(new(CurrentBytes: -100, RetiredBytes: 300, MetadataBytes: 20), 300);

        replaced.CurrentBytes.ShouldBe(200);
        replaced.RetiredBytes.ShouldBe(300);
        replaced.Available.ShouldBe(330);
        Should.Throw<CacheTransferException>(() => replaced.Reserve(331)).Code.ShouldBe(CacheErrorCode.Capacity);
        var reclaimed = replaced.Apply(new(RetiredBytes: -300), 0);
        reclaimed.Available.ShouldBe(630);
    }

    [Fact]
    public void MetadataWorkspace_ReportsItsActualBytesAndUnusedReservationWithoutDoubleCharging()
    {
        var state = new CacheRootQuotaState { StoreId = Guid.NewGuid().ToString("N"), MaximumBytes = 1000, MaximumEntries = 2,
            WorkspaceBytes = 100, ReservedBytes = 200, MetadataBytes = 50, CurrentBytes = 300 };

        var snapshot = state.Snapshot(70);

        snapshot.StagingBytes.ShouldBe(230);
        snapshot.MetadataBytes.ShouldBe(120);
        (snapshot.CurrentBytes + snapshot.RetiredBytes + snapshot.StagingBytes + snapshot.MetadataBytes).ShouldBe(650);
        Should.Throw<CacheTransferException>(() => state.Snapshot(101)).Code.ShouldBe(CacheErrorCode.Corruption);
    }

    [Fact]
    public void InvalidReleaseNegativeCountersAndOverflow_FailWithoutCreatingFreeCapacity()
    {
        var state = new CacheRootQuotaState { StoreId = Guid.NewGuid().ToString("N"), MaximumBytes = long.MaxValue, MaximumEntries = 1,
            CurrentBytes = long.MaxValue - 100, WorkspaceBytes = 100 };

        state.Available.ShouldBe(0);
        Should.Throw<CacheTransferException>(() => state.Reserve(1)).Code.ShouldBe(CacheErrorCode.Capacity);
        Should.Throw<CacheTransferException>(() => state.Release(1)).Code.ShouldBe(CacheErrorCode.Corruption);
        Should.Throw<CacheTransferException>(() => state.Apply(new(CurrentBytes: 101), 0)).Code.ShouldBe(CacheErrorCode.Corruption);
        Should.Throw<CacheTransferException>(() => state.Apply(new(RetiredBytes: -1), 0)).Code.ShouldBe(CacheErrorCode.Corruption);
        state.Available.ShouldBe(0);
    }

    [Fact]
    public void CollectionQuota_IsIndependentOfProviderHeadroomAndCountsMetadata()
    {
        var state = new CacheCollectionQuotaState { Identity = "scope", MaximumBytes = 500, MaximumEntries = 1, MetadataBytes = 100 };
        state.Validate("scope");
        var pending = state.Reserve(400);
        var committed = pending.Apply(new(CurrentBytes: 300, MetadataBytes: 100, Entries: 1), 400);

        committed.Available.ShouldBe(0);
        Should.Throw<CacheTransferException>(() => committed.Reserve(1)).Code.ShouldBe(CacheErrorCode.Capacity);
        Should.Throw<CacheTransferException>(() => committed.Apply(new(Entries: 1), 0)).Code.ShouldBe(CacheErrorCode.Capacity);
        Should.Throw<CacheTransferException>(() => committed.Validate("another")).Code.ShouldBe(CacheErrorCode.Corruption);
    }
}