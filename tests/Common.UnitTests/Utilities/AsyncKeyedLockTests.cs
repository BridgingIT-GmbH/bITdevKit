// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Collections;
using System.Reflection;

/// <summary>Verifies exclusion and lease cleanup for asynchronous keyed locks.</summary>
/// <example><code>using var locks = new AsyncKeyedLock&lt;string&gt;(StringComparer.OrdinalIgnoreCase);</code></example>
[UnitTest("Common")]
public sealed class AsyncKeyedLockTests
{
    /// <summary>Exercises direct acquisition, timeout, comparer equality, and release.</summary>
    /// <example><code>using var lease = await locks.TryAcquireAsync("report-42", TimeSpan.Zero);</code></example>
    [Fact]
    public async Task TryAcquireAsync_AtPool_TransfersOnlyAcquiredLeases()
    {
        using var sut = new AsyncKeyedLock<string>(StringComparer.OrdinalIgnoreCase);
        using var held = await sut.TryAcquireAsync("Report", TimeSpan.Zero);
        held.ShouldNotBeNull();

        (await sut.TryAcquireAsync("report", TimeSpan.Zero)).ShouldBeNull();
        (await sut.TryAcquireAsync("report", TimeSpan.FromMilliseconds(10))).ShouldBeNull();
        using var other = await sut.TryAcquireAsync("other", TimeSpan.Zero);
        other.ShouldNotBeNull();

        using var cancellation = new CancellationTokenSource();
        var canceled = sut.TryAcquireAsync("report", Timeout.InfiniteTimeSpan, cancellation.Token);
        var queued = sut.TryAcquireAsync("report", TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => canceled);
        queued.IsCompleted.ShouldBeFalse();
        held.Dispose();

        using var next = await queued;
        next.ShouldNotBeNull();
        (await sut.TryAcquireAsync("Report", TimeSpan.Zero)).ShouldBeNull();
    }

    /// <summary>Checks cancellation, invalid timeouts, and pool disposal for direct acquisition.</summary>
    /// <example><code>var pending = locks.TryAcquireAsync("report-42", TimeSpan.Zero, cancellationToken);</code></example>
    [Fact]
    public async Task TryAcquireAsync_AtPoolWithFailedWait_ReturnsReservations()
    {
        using var sut = new AsyncKeyedLock<int>();
        var entriesField = typeof(AsyncKeyedLock<int>).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
        var entries = (IDictionary)entriesField.GetValue(sut);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => sut.TryAcquireAsync(1, TimeSpan.Zero, cancellation.Token));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => sut.TryAcquireAsync(2, TimeSpan.FromMilliseconds(-2)));
        entries.Count.ShouldBe(0);

        sut.Dispose();
        await Should.ThrowAsync<ObjectDisposedException>(() => sut.TryAcquireAsync(1, TimeSpan.Zero));
    }

    /// <summary>Verifies that the selected comparer controls exclusion and that other keys proceed independently.</summary>
    /// <example><code>using var lease = locks.Rent("report-42");</code></example>
    [Fact]
    public async Task TryAcquireAsync_EquivalentAndDistinctKeys_ExcludeOnlyEquivalentKeys()
    {
        using var sut = new AsyncKeyedLock<string>(StringComparer.OrdinalIgnoreCase);
        using var holder = sut.Rent("Report-42");
        using var sameKey = sut.Rent("report-42");
        using var otherKey = sut.Rent("report-43");
        (await holder.TryAcquireAsync(TimeSpan.Zero)).ShouldBeTrue();

        var sameResult = await sameKey.TryAcquireAsync(TimeSpan.Zero);
        var otherResult = await otherKey.TryAcquireAsync(TimeSpan.Zero);

        sameResult.ShouldBeFalse();
        otherResult.ShouldBeTrue();
        await Should.ThrowAsync<ObjectDisposedException>(() => sameKey.TryAcquireAsync(TimeSpan.Zero));
    }

    /// <summary>Verifies that canceling one waiter leaves the remaining waiter on the original semaphore.</summary>
    /// <example><code>var pending = lease.TryAcquireAsync(Timeout.InfiniteTimeSpan, cancellationToken);</code></example>
    [Fact]
    public async Task TryAcquireAsync_CanceledWaiter_PreservesExclusionForRemainingWaiters()
    {
        using var sut = new AsyncKeyedLock<int>();
        using var cancellation = new CancellationTokenSource();
        using var holder = sut.Rent(42);
        using var canceled = sut.Rent(42);
        using var queued = sut.Rent(42);
        (await holder.TryAcquireAsync(TimeSpan.Zero)).ShouldBeTrue();
        var canceledWait = canceled.TryAcquireAsync(Timeout.InfiniteTimeSpan, cancellation.Token);
        var queuedWait = queued.TryAcquireAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        var error = await Should.ThrowAsync<OperationCanceledException>(() => canceledWait);
        error.CancellationToken.ShouldBe(cancellation.Token);
        queuedWait.IsCompleted.ShouldBeFalse();
        using var newcomer = sut.Rent(42);
        (await newcomer.TryAcquireAsync(TimeSpan.Zero)).ShouldBeFalse();

        holder.Dispose();
        (await queuedWait).ShouldBeTrue();
    }

    /// <summary>Verifies one-shot acquisition and idempotent disposal.</summary>
    /// <example><code>using var lease = locks.Rent("report-42");</code></example>
    [Fact]
    public async Task TryAcquireAsync_ReusedLease_RejectsAnotherAttempt()
    {
        using var sut = new AsyncKeyedLock<int>();
        using var lease = sut.Rent(42);
        (await lease.TryAcquireAsync(TimeSpan.Zero)).ShouldBeTrue();

        await Should.ThrowAsync<InvalidOperationException>(() => lease.TryAcquireAsync(TimeSpan.Zero));
        lease.Dispose();
        lease.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(() => lease.TryAcquireAsync(TimeSpan.Zero));
        using var replacement = sut.Rent(42);
        (await replacement.TryAcquireAsync(TimeSpan.Zero)).ShouldBeTrue();
    }

    /// <summary>Verifies that pending disposal keeps the entry alive and releases any lock obtained by the wait.</summary>
    /// <example><code>lease.Dispose();</code></example>
    [Fact]
    public async Task Dispose_WhileWaiting_ReleasesTheReservationAfterTheWaitSettles()
    {
        using var sut = new AsyncKeyedLock<int>();
        using var holder = sut.Rent(42);
        using var waiter = sut.Rent(42);
        (await holder.TryAcquireAsync(TimeSpan.Zero)).ShouldBeTrue();
        var pending = waiter.TryAcquireAsync(TimeSpan.FromSeconds(5));
        await Should.ThrowAsync<InvalidOperationException>(() => waiter.TryAcquireAsync(TimeSpan.Zero));

        waiter.Dispose();
        waiter.Dispose();

        pending.IsCompleted.ShouldBeFalse();
        using var newcomer = sut.Rent(42);
        (await newcomer.TryAcquireAsync(TimeSpan.Zero)).ShouldBeFalse();
        holder.Dispose();
        await Should.ThrowAsync<ObjectDisposedException>(() => pending);
        using var replacement = sut.Rent(42);
        (await replacement.TryAcquireAsync(TimeSpan.Zero)).ShouldBeTrue();
    }

    /// <summary>Verifies that existing rentals finish after the pool stops accepting new leases.</summary>
    /// <example><code>locks.Dispose();</code></example>
    [Fact]
    public async Task Dispose_WithActiveLeases_StopsRentalsAndAllowsExistingWaitsToFinish()
    {
        using var sut = new AsyncKeyedLock<int>();
        using var holder = sut.Rent(42);
        using var waiter = sut.Rent(42);
        using var reserved = sut.Rent(43);
        (await holder.TryAcquireAsync(TimeSpan.Zero)).ShouldBeTrue();
        var pending = waiter.TryAcquireAsync(TimeSpan.FromSeconds(5));

        sut.Dispose();

        Should.Throw<ObjectDisposedException>(() => sut.Rent(44));
        pending.IsCompleted.ShouldBeFalse();
        holder.Dispose();
        (await pending).ShouldBeTrue();
        (await reserved.TryAcquireAsync(TimeSpan.Zero)).ShouldBeTrue();
    }

    /// <summary>Checks that retired keys leave the pool after release, cancellation, and invalid waits.</summary>
    /// <example><code>using var lease = locks.Rent("report-42");</code></example>
    [Fact]
    public async Task Rent_WithNoRemainingLeases_RemovesUnusedEntries()
    {
        using var sut = new AsyncKeyedLock<int>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var entriesField = typeof(AsyncKeyedLock<int>).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
        var entries = (IDictionary)entriesField.GetValue(sut);
        using var unused = sut.Rent(41);
        using var holder = sut.Rent(42);
        using var canceled = sut.Rent(42);
        using var invalid = sut.Rent(43);
        (await holder.TryAcquireAsync(TimeSpan.Zero)).ShouldBeTrue();

        entries.Count.ShouldBe(3);
        await Should.ThrowAsync<OperationCanceledException>(() => canceled.TryAcquireAsync(TimeSpan.Zero, cancellation.Token));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => invalid.TryAcquireAsync(TimeSpan.FromMilliseconds(-2)));
        unused.Dispose();

        entries.Count.ShouldBe(1);
        holder.Dispose();
        entries.Count.ShouldBe(0);
    }

    /// <summary>Verifies mutual exclusion while several asynchronous callers churn through the same key.</summary>
    /// <example><code>using var lease = locks.Rent("report-42");</code></example>
    [Fact]
    public async Task TryAcquireAsync_ConcurrentRentals_RunOneCallerAtATime()
    {
        using var sut = new AsyncKeyedLock<int>();
        var active = 0;
        var completed = 0;

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => EnterRepeatedlyAsync()));

        active.ShouldBe(0);
        completed.ShouldBe(128);

        async Task EnterRepeatedlyAsync()
        {
            for (var i = 0; i < 16; i++)
            {
                using var lease = sut.Rent(42);
                (await lease.TryAcquireAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();
                Interlocked.Increment(ref active).ShouldBe(1);
                await Task.Yield();
                Interlocked.Decrement(ref active).ShouldBe(0);
                Interlocked.Increment(ref completed);
            }
        }
    }
}
