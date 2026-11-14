namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

public class CacheResourceTests
{
    [Fact]
    public async Task Admission_BlockedChild_DoesNotOccupyAnIdleParentSlot()
    {
        var parent = new CacheAdmission("process", 2, 8);
        var first = new CacheAdmission("first", 1, 4, parent);
        var second = new CacheAdmission("second", 1, 4, parent);
        using var owner = (await first.AcquireAsync(TimeSpan.FromMinutes(1))).Value;
        var queued = first.AcquireAsync(TimeSpan.FromMinutes(1)).AsTask();
        queued.IsCompleted.ShouldBeFalse();
        parent.Active.ShouldBe(1);
        using var other = (await second.AcquireAsync(TimeSpan.FromMinutes(1))).Value;
        parent.Active.ShouldBe(2);
        first.Queued.ShouldBe(1);

        owner.Dispose();
        using var follower = (await queued).Value;

        first.Active.ShouldBe(1);
        first.Queued.ShouldBe(0);
        parent.Active.ShouldBe(2);
    }

    [Fact]
    public async Task Admission_ExpiredWait_DoesNotReleaseOccupiedExecution()
    {
        var time = new FakeTimeProvider();
        var sut = new CacheAdmission("sources", 1, 1, timeProvider: time);
        using var owner = (await sut.AcquireAsync(TimeSpan.FromSeconds(1))).Value;
        var waiter = sut.AcquireAsync(TimeSpan.FromSeconds(1)).AsTask();
        var overflow = await sut.AcquireAsync(TimeSpan.FromSeconds(1));
        overflow.IsFailure.ShouldBeTrue();
        overflow.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Capacity);

        time.Advance(TimeSpan.FromSeconds(2));
        var result = await waiter;

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.CoordinationTimeout);
        sut.Active.ShouldBe(1);
        sut.Queued.ShouldBe(0);
        owner.Dispose();
        sut.Active.ShouldBe(0);
    }

    [Fact]
    public async Task Admission_CancellationRacingGrant_DoesNotLeakOrDoubleRelease()
    {
        var sut = new CacheAdmission("sources", 1, 2);
        for (var i = 0; i < 100; i++)
        {
            using var owner = (await sut.AcquireAsync(TimeSpan.FromMinutes(1))).Value;
            using var cancellation = new CancellationTokenSource();
            var waiter = sut.AcquireAsync(TimeSpan.FromMinutes(1), cancellation.Token).AsTask();
            await Task.WhenAll(Task.Run(cancellation.Cancel), Task.Run(owner.Dispose));
            try
            {
                using var granted = (await waiter).Value;
            }
            catch (OperationCanceledException)
            {
                // Either a completed grant or cancellation is valid; both must return all occupied capacity.
            }

            sut.Active.ShouldBe(0);
            sut.Queued.ShouldBe(0);
        }
    }
}