// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

/// <summary>Verifies bounded stopping and actual resource ownership without granting task admission.</summary>
public sealed class OwnedTaskTrackerTests
{
    /// <summary>An expired stop wait retains admitted capacity until actual work completes, even when an observer fails.</summary>
    [Fact]
    public async Task DisposeAsync_StopWaitExpires_RetainsAdmissionUntilActualCompletion()
    {
        var timedOut = 0;
        var sut = new OwnedTaskTracker(TimeSpan.Zero, stopTimedOut: count =>
        {
            timedOut = count;
            throw new InvalidOperationException("Observer failure.");
        });
        var admission = new ResourceAdmission("uploads", 1, 0);
        admission.TryAcquire(out var owner).ShouldBeTrue();
        var scope = ResourceLeaseContext.Begin(owner);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Track(pending.Task, "native-upload");
        scope.Dispose();

        await sut.DisposeAsync();

        sut.Stopping.IsCancellationRequested.ShouldBeTrue();
        timedOut.ShouldBe(1);
        sut.Count.ShouldBe(1);
        admission.Active.ShouldBe(1);
        pending.SetResult();
        await WaitForDrainAsync(sut);
        admission.Active.ShouldBe(0);
        await sut.DisposeAsync();
    }

    /// <summary>Task failures are observed without leaking the failure observer's own exception.</summary>
    [Fact]
    public void Track_FaultedTask_ContainsObserverFaultAndCompletesObservation()
    {
        Exception observed = null;
        string category = null;
        var sut = new OwnedTaskTracker(TimeSpan.Zero, (exception, kind) =>
        {
            observed = exception;
            category = kind;
            throw new InvalidOperationException("Observer failure.");
        });
        var failure = new IOException("Native failure.");

        sut.Track(Task.FromException(failure), "upload");

        observed.ShouldBeSameAs(failure);
        category.ShouldBe("upload");
        sut.Count.ShouldBe(0);
        sut.DisposeAsync().IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>Resource cleanup failures cannot strand tracked occupancy or prevent their diagnostic observation.</summary>
    [Fact]
    public async Task Track_ResourceDisposalThrows_ObservesFailureAndRemovesCompletedTask()
    {
        var observed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = new OwnedTaskTracker(TimeSpan.FromSeconds(1), (exception, _) => observed.TrySetResult(exception));
        var resource = new ThrowingResource();
        var scope = ResourceLeaseContext.Begin(resource);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Track(pending.Task, "upload");
        scope.Dispose();

        pending.SetResult();
        var failure = await observed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitForDrainAsync(sut);

        failure.ShouldBeOfType<IOException>();
        resource.Disposals.ShouldBe(1);
        await sut.DisposeAsync();
    }

    /// <summary>A parent stopping signal and throwing notification callback do not complete unfinished work.</summary>
    [Fact]
    public async Task BeginStop_ParentSignalAndCallbackFailure_PreservesTrackedTask()
    {
        using var stopping = new CancellationTokenSource();
        var sut = new OwnedTaskTracker(TimeSpan.Zero, stoppingToken: stopping.Token);
        using var registration = sut.Stopping.Register(() => throw new InvalidOperationException("Notification failure."));
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Track(pending.Task, "upload");

        stopping.Cancel();

        sut.Stopping.IsCancellationRequested.ShouldBeTrue();
        sut.Count.ShouldBe(1);
        pending.SetResult();
        await WaitForDrainAsync(sut);
        await sut.DisposeAsync();
    }

    /// <summary>An already admitted tail can register after stopping while its ambient resource remains owned.</summary>
    [Fact]
    public async Task Track_AfterStop_PreservesAlreadyAdmittedTailOwnership()
    {
        var sut = new OwnedTaskTracker(TimeSpan.Zero);
        await sut.DisposeAsync();
        var admission = new ResourceAdmission("uploads", 1, 0);
        admission.TryAcquire(out var owner).ShouldBeTrue();
        var scope = ResourceLeaseContext.Begin(owner);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        sut.Track(pending.Task, "native-tail");
        scope.Dispose();

        sut.Stopping.IsCancellationRequested.ShouldBeTrue();
        sut.Count.ShouldBe(1);
        admission.Active.ShouldBe(1);
        pending.SetResult();
        await WaitForDrainAsync(sut);
        admission.Active.ShouldBe(0);
        await sut.DisposeAsync();
    }

    private static async Task WaitForDrainAsync(OwnedTaskTracker tracker)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (tracker.Count != 0) { await Task.Delay(1, timeout.Token); }
    }

    private sealed class ThrowingResource : IDisposable
    {
        public int Disposals;

        public void Dispose()
        {
            Interlocked.Increment(ref this.Disposals);
            throw new IOException("Cleanup failure.");
        }
    }
}