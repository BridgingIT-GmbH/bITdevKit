// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

/// <summary>Checks hierarchical capacity ownership, rejection and release.</summary>
public sealed class ResourceBudgetTests
{
    [Fact]
    public void RawCharges_InvalidAmounts_LeaveEveryCounterUnchanged()
    {
        var parent = new ResourceBudget("process", 100);
        var sut = new ResourceBudget("child", 50, parent);
        sut.TryCharge(10).ShouldBeTrue();

        Should.Throw<ArgumentOutOfRangeException>(() => sut.TryCharge(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => sut.Return(-1));
        Should.Throw<InvalidOperationException>(() => sut.Return(11));

        sut.Used.ShouldBe(10);
        parent.Used.ShouldBe(10);
        sut.Return(10);
        parent.Used.ShouldBe(0);
    }

    [Fact]
    public void CombinedReservations_LaterRejection_RollsBackEveryEarlierCharge()
    {
        var first = new ResourceBudget("first", 100);
        var second = new ResourceBudget("second", 10);
        second.TryReserve(5, out var retained).ShouldBeTrue();
        using var reservation = retained;

        ResourceCharge.TryReserve(6, out var charge, first, second).ShouldBeFalse();

        charge.ShouldBeNull();
        first.Used.ShouldBe(0);
        second.Used.ShouldBe(5);
    }

    [Fact]
    public void Reservation_MaximumCapacity_RejectsGrowthWithoutOverflow()
    {
        var sut = new ResourceBudget("maximum", long.MaxValue);
        sut.TryReserve(long.MaxValue, out var owned).ShouldBeTrue();
        using var reservation = owned;

        reservation.TryGrow(1).ShouldBeFalse();
        sut.TryReserve(1, out _).ShouldBeFalse();
        reservation.Amount.ShouldBe(long.MaxValue);
        reservation.ReduceTo(1);
        sut.Used.ShouldBe(1);
        reservation.Dispose();
        sut.Used.ShouldBe(0);
    }

    [Fact]
    public void CombinedReservations_ReduceTemporaryCapacityAndRetainOutputAcrossEveryBudget()
    {
        var parent = new ResourceBudget("process", 100);
        var resident = new ResourceBudget("collection", 100, parent);
        var transient = new ResourceBudget("transient", 100);
        ResourceCharge.TryReserve(80, out var charge, resident, transient).ShouldBeTrue();
        using var sut = charge;

        sut.ReduceTo(20);

        resident.Used.ShouldBe(20);
        parent.Used.ShouldBe(20);
        transient.Used.ShouldBe(20);
        Should.Throw<ArgumentOutOfRangeException>(() => sut.ReduceTo(30));
        sut.Dispose();
        resident.Used.ShouldBe(0);
        parent.Used.ShouldBe(0);
        transient.Used.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => sut.ReduceTo(0));
    }

    [Fact]
    public void Reservations_ParentAndChildExhaustion_RollBackWithoutReleasingExistingCapacity()
    {
        var parent = new ResourceBudget("process", 10);
        var first = new ResourceBudget("first", 8, parent);
        var second = new ResourceBudget("second", 8, parent);
        first.TryReserve(7, out var owned).ShouldBeTrue();
        using var reservation = owned;

        second.TryReserve(4, out _).ShouldBeFalse();
        parent.Used.ShouldBe(7);
        second.Used.ShouldBe(0);
        reservation.TryGrow(2).ShouldBeFalse();
        parent.Used.ShouldBe(7);
        first.Used.ShouldBe(7);
        reservation.TryGrow(1).ShouldBeTrue();
        parent.Used.ShouldBe(8);
        reservation.Amount.ShouldBe(8);
        reservation.Dispose();
        reservation.Dispose();
        parent.Used.ShouldBe(0);
        first.Used.ShouldBe(0);
    }

    [Fact]
    public void Reservations_ConcurrentAdmission_NeverExceedsParentCapacity()
    {
        var parent = new ResourceBudget("process", 64);
        var children = Enumerable.Range(0, 4).Select(i => new ResourceBudget("collection-" + i, 32, parent)).ToArray();
        Parallel.For(0, 10000, i =>
        {
            if (children[i % children.Length].TryReserve(4, out var reservation))
            {
                using (reservation)
                {
                    parent.Used.ShouldBeLessThanOrEqualTo(64);
                    reservation.Amount.ShouldBe(4);
                }
            }
        });

        parent.Used.ShouldBe(0);
        children.All(c => c.Used == 0).ShouldBeTrue();
    }

}