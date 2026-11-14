// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies bounded shared resource ownership independently of feature-specific error mapping.</summary>
public sealed class ResourceAdmissionTests
{
    /// <summary>A child waiting on its own occupied limit leaves parent capacity available to unrelated groups.</summary>
    [Fact]
    public async Task AcquireAsync_BlockedChild_AllowsUnrelatedChildToProceed()
    {
        var parent = new ResourceAdmission("process", 2, 8);
        var sut = new ResourceAdmission("reports", 1, 4, parent);
        var other = new ResourceAdmission("images", 1, 4, parent);
        using var owner = (await sut.AcquireAsync(TimeSpan.FromSeconds(10))).Value;
        var waiting = sut.AcquireAsync(TimeSpan.FromSeconds(10)).AsTask();

        using var unrelated = (await other.AcquireAsync(TimeSpan.FromSeconds(10))).Value;

        waiting.IsCompleted.ShouldBeFalse();
        parent.Active.ShouldBe(2);
        parent.Queued.ShouldBe(1);
        owner.Dispose();
        using var follower = (await waiting).Value;
        sut.Active.ShouldBe(1);
        parent.Queued.ShouldBe(0);
    }

    /// <summary>Exhaustion and elapsed waiting use neutral error types without releasing admitted work.</summary>
    [Fact]
    public async Task AcquireAsync_QueueFullAndExpiredWait_ReturnsQuotaAndTimeoutErrors()
    {
        var time = new FakeTimeProvider();
        var sut = new ResourceAdmission("uploads", 1, 1, timeProvider: time);
        using var owner = (await sut.AcquireAsync(TimeSpan.FromSeconds(1))).Value;
        var waiting = sut.AcquireAsync(TimeSpan.FromSeconds(1)).AsTask();

        var full = await sut.AcquireAsync(TimeSpan.FromSeconds(1));
        time.Advance(TimeSpan.FromSeconds(1));
        var expired = await waiting;

        full.Errors.Single().ShouldBeOfType<QuotaExceededError>();
        expired.Errors.Single().ShouldBeOfType<TimeoutError>();
        sut.Active.ShouldBe(1);
        sut.Queued.ShouldBe(0);
        owner.Dispose();
        owner.Dispose();
        sut.Active.ShouldBe(0);
    }

    /// <summary>A cancelled caller cannot occupy otherwise available capacity.</summary>
    [Fact]
    public async Task AcquireAsync_AlreadyCancelled_DoesNotAcquireOrQueue()
    {
        var sut = new ResourceAdmission("uploads", 1, 1);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            sut.AcquireAsync(TimeSpan.FromSeconds(1), new CancellationToken(true)).AsTask());

        sut.Active.ShouldBe(0);
        sut.Queued.ShouldBe(0);
    }

    /// <summary>A long positive budget may acquire immediately but cannot create an unschedulable queued ticket.</summary>
    [Fact]
    public async Task AcquireAsync_QueueWaitExceedsTimerLimit_RejectsWithoutChangingOwnership()
    {
        var sut = new ResourceAdmission("uploads", 1, 1);
        var longBudget = TimeSpan.FromDays(365);
        using var owner = (await sut.AcquireAsync(longBudget)).Value;

        var rejected = await sut.AcquireAsync(longBudget);

        rejected.Errors.Single().ShouldBeOfType<TimeoutError>();
        sut.Active.ShouldBe(1);
        sut.Queued.ShouldBe(0);
        owner.Dispose();
        sut.Active.ShouldBe(0);
    }

    /// <summary>Cancellation withdraws only its pending ticket and preserves the current owner's charge.</summary>
    [Fact]
    public async Task AcquireAsync_CancelledWait_PreservesActiveOwner()
    {
        var sut = new ResourceAdmission("uploads", 1, 1);
        using var owner = (await sut.AcquireAsync(TimeSpan.FromSeconds(10))).Value;
        using var cancellation = new CancellationTokenSource();
        var waiting = sut.AcquireAsync(TimeSpan.FromSeconds(10), cancellation.Token).AsTask();

        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => waiting);

        sut.Active.ShouldBe(1);
        sut.Queued.ShouldBe(0);
    }

    /// <summary>Immediate rejection never adds a queued ticket and successful lease disposal returns its unit once.</summary>
    [Fact]
    public void TryAcquire_OccupiedGroup_RejectsWithoutQueueing()
    {
        var sut = new ResourceAdmission("uploads", 1, 0);
        sut.TryAcquire(out var owner).ShouldBeTrue();

        sut.TryAcquire(out var rejected).ShouldBeFalse();

        rejected.ShouldBeNull();
        sut.Queued.ShouldBe(0);
        owner.Dispose();
        owner.Dispose();
        sut.Active.ShouldBe(0);
    }

    /// <summary>An unmatched raw release fails before changing group or parent accounting.</summary>
    [Fact]
    public void Release_WithoutOwnedUnit_PreservesCounters()
    {
        var parent = new ResourceAdmission("process", 1, 1);
        var sut = new ResourceAdmission("uploads", 1, 1, parent);

        Should.Throw<InvalidOperationException>(sut.Release);

        sut.Active.ShouldBe(0);
        parent.Active.ShouldBe(0);
        sut.TryAcquire(out var owner).ShouldBeTrue();
        owner.Dispose();
    }
}