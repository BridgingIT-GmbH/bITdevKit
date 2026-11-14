// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Checks existing deadline and retained-task behavior independently of cache provider policy.</summary>
public sealed class CacheResourceMechanismTests
{
    /// <summary>The original null and default owner constructors remain unambiguous and dispose as no-ops.</summary>
    [Fact]
    public void AdmissionLease_NullAndDefaultOwners_RemainUnambiguousNoOps()
    {
        using var sut = new CacheAdmissionLease(null);
        using var other = new CacheAdmissionLease(default);

        sut.Dispose();
        other.Dispose();
    }

    /// <summary>Successful progress renews inactivity without moving the original absolute deadline.</summary>
    [Fact]
    public void TransferProgress_RenewsOnlyIdleDeadline_ExpiresAtOriginalLifetime()
    {
        var time = new FakeTimeProvider();
        var expired = 0;
        using var sut = new CacheTransferBudget(time, new()
        {
            IdleTimeout = TimeSpan.FromSeconds(3),
            Lifetime = TimeSpan.FromSeconds(5)
        }, () => expired++);
        sut.Start();

        time.Advance(TimeSpan.FromSeconds(2));
        sut.Progress();
        time.Advance(TimeSpan.FromSeconds(2));
        sut.Progress();
        sut.HasExpired.ShouldBeFalse();
        time.Advance(TimeSpan.FromSeconds(1));

        sut.HasExpired.ShouldBeTrue();
        sut.Token.IsCancellationRequested.ShouldBeTrue();
        expired.ShouldBe(1);
    }

    /// <summary>Constructing a transfer owner fixes the lifetime even if its first timer scheduling occurs later.</summary>
    [Fact]
    public void TransferStart_DelayedScheduling_DoesNotRenewLifetime()
    {
        var time = new FakeTimeProvider();
        using var sut = new CacheTransferBudget(time, new()
        {
            IdleTimeout = TimeSpan.FromSeconds(10),
            Lifetime = TimeSpan.FromSeconds(5)
        }, () => { });

        time.Advance(TimeSpan.FromSeconds(6));
        sut.Start();

        sut.HasExpired.ShouldBeTrue();
    }

    /// <summary>An already cancelled caller cannot occupy an immediately available admission slot.</summary>
    [Fact]
    public async Task Admission_AlreadyCancelledCaller_DoesNotOccupyCapacity()
    {
        var sut = new CacheAdmission("sources", 1, 1);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            sut.AcquireAsync(TimeSpan.FromSeconds(1), new CancellationToken(true)).AsTask());

        sut.Active.ShouldBe(0);
        sut.Queued.ShouldBe(0);
    }

    /// <summary>A bounded stop wait does not release ambient resources retained by actual unfinished work.</summary>
    [Fact]
    public async Task TrackedWork_StopWaitExpires_RetainsResourceUntilActualCompletion()
    {
        var catalog = new ServiceCollection().AddApplicationCaching(options => options
            .BackgroundServices(background => background.StopTimeout(TimeSpan.FromMilliseconds(1)))).BuildCatalog();
        var sut = new CacheRuntimeTasks(catalog);
        var resource = new ResourceProbe();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scope = CacheResourceLeaseContext.Begin(resource);
        sut.Track(pending.Task, "native-work");
        scope.Dispose();

        await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        sut.Stopping.IsCancellationRequested.ShouldBeTrue();
        sut.Count.ShouldBe(1);
        resource.Released.Task.IsCompleted.ShouldBeFalse();
        pending.SetResult();
        await resource.Released.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await sut.DisposeAsync();
        using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (sut.Count != 0) { await Task.Delay(1, drain.Token); }

        resource.Disposals.ShouldBe(1);
        sut.Count.ShouldBe(0);
    }

    private sealed class ResourceProbe : IDisposable
    {
        public int Disposals;

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Dispose()
        {
            Interlocked.Increment(ref this.Disposals);
            this.Released.TrySetResult();
        }
    }
}