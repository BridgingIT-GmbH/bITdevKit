// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Time.Testing;

[UnitTest("Application.Storage")]
public class BlobUploadAdmissionCoordinatorTests
{
    private static readonly UploadConcurrencyBlobStoreClientBehaviorOptions Options = new()
    {
        MaxConcurrentUploads = 1,
        MaxQueuedUploads = 2,
        QueueWaitTimeout = TimeSpan.FromMinutes(1)
    };

    [Fact]
    public async Task AcquireAsync_WhenPermitReleased_AdmitsOldestWaiterFirst()
    {
        using var sut = new BlobUploadAdmissionCoordinator();
        await using var active = await sut.AcquireAsync("reports", Options, default);
        var firstWaiter = sut.AcquireAsync("reports", Options, default).AsTask();
        var secondWaiter = sut.AcquireAsync("reports", Options, default).AsTask();

        sut.GetSnapshots().Single().QueuedUploads.ShouldBe(2);
        await active.DisposeAsync();

        await using var first = await firstWaiter;
        first.IsAcquired.ShouldBeTrue();
        secondWaiter.IsCompleted.ShouldBeFalse();

        await first.DisposeAsync();
        await using var second = await secondWaiter;
        second.IsAcquired.ShouldBeTrue();
    }

    [Fact]
    public async Task AcquireAsync_WhenQueueFull_ReturnsOverloadedErrorImmediately()
    {
        var options = new UploadConcurrencyBlobStoreClientBehaviorOptions
        {
            MaxConcurrentUploads = 1,
            MaxQueuedUploads = 1,
            QueueWaitTimeout = TimeSpan.FromMinutes(1)
        };
        using var sut = new BlobUploadAdmissionCoordinator();
        await using var active = await sut.AcquireAsync("reports", options, default);
        var queued = sut.AcquireAsync("reports", options, default).AsTask();

        await using var rejected = await sut.AcquireAsync("reports", options, default);

        rejected.IsAcquired.ShouldBeFalse();
        rejected.Error.ShouldBeOfType<BlobStoreUploadOverloadedError>();
        await active.DisposeAsync();
        await (await queued).DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenQueueWaitExpires_ReturnsAdmissionTimeoutError()
    {
        var time = new FakeTimeProvider();
        using var sut = new BlobUploadAdmissionCoordinator(time);
        await using var active = await sut.AcquireAsync("reports", Options, default);
        var queued = sut.AcquireAsync("reports", Options, default).AsTask();

        time.Advance(Options.QueueWaitTimeout);
        await using var result = await queued;

        result.IsAcquired.ShouldBeFalse();
        result.Error.ShouldBeOfType<BlobStoreUploadAdmissionTimeoutError>();
        sut.GetSnapshots().Single().QueuedUploads.ShouldBe(0);
    }

    [Fact]
    public async Task AcquireAsync_WhenCallerCancels_ThrowsAndRemovesWaiter()
    {
        using var sut = new BlobUploadAdmissionCoordinator();
        await using var active = await sut.AcquireAsync("reports", Options, default);
        using var cancellation = new CancellationTokenSource();
        var queued = sut.AcquireAsync("reports", Options, cancellation.Token).AsTask();

        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(queued);
        sut.GetSnapshots().Single().QueuedUploads.ShouldBe(0);
    }

    [Fact]
    public async Task AcquireAsync_WhenCallerIsAlreadyCanceled_ThrowsWithoutAcquiringPermit()
    {
        using var sut = new BlobUploadAdmissionCoordinator();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await sut.AcquireAsync(
            "reports",
            Options,
            cancellation.Token);

        await action.ShouldThrowAsync<OperationCanceledException>();
        sut.GetSnapshots().ShouldBeEmpty();
    }

    [Fact]
    public async Task AcquireAsync_NormalizesCaseAndKeepsDifferentStoresIndependent()
    {
        using var sut = new BlobUploadAdmissionCoordinator();
        await using var first = await sut.AcquireAsync(" Reports ", Options, default);
        var sameStore = sut.AcquireAsync("REPORTS", Options, default).AsTask();
        await using var otherStore = await sut.AcquireAsync("archive", Options, default);

        otherStore.IsAcquired.ShouldBeTrue();
        sameStore.IsCompleted.ShouldBeFalse();
        sut.GetSnapshots().Select(snapshot => snapshot.StoreName)
            .ShouldBe(["archive", "reports"]);

        await first.DisposeAsync();
        await (await sameStore).DisposeAsync();
    }

    [Fact]
    public async Task Dispose_WhenAcquisitionIsQueued_CompletesWaiterAndRejectsNewCalls()
    {
        var sut = new BlobUploadAdmissionCoordinator();
        await using var active = await sut.AcquireAsync("reports", Options, default);
        var queued = sut.AcquireAsync("reports", Options, default).AsTask();

        sut.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(queued);
        await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await sut.AcquireAsync("reports", Options, default));
    }

    [Fact]
    public async Task AcquireAsync_WhenStoreLimitsDiffer_ThrowsInvalidOperationException()
    {
        using var sut = new BlobUploadAdmissionCoordinator();
        await using var active = await sut.AcquireAsync("reports", Options, default);
        var different = new UploadConcurrencyBlobStoreClientBehaviorOptions
        {
            MaxConcurrentUploads = 2,
            MaxQueuedUploads = Options.MaxQueuedUploads,
            QueueWaitTimeout = Options.QueueWaitTimeout
        };

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await sut.AcquireAsync("reports", different, default));
    }

    [Fact]
    public async Task AcquireAsync_WhenMetricListenerThrows_DoesNotLeakQueueOrPermits()
    {
        using var meterFactory = new CoordinatorMeterFactory();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name is "blobstorage_uploads_active" or "blobstorage_uploads_queued")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
            throw new InvalidOperationException("simulated metric listener failure"));
        listener.Start();
        using var sut = new BlobUploadAdmissionCoordinator(meterFactory: meterFactory);

        await using var active = await sut.AcquireAsync("reports", Options, default);
        var queued = sut.AcquireAsync("reports", Options, default).AsTask();

        sut.GetSnapshots().Single().QueuedUploads.ShouldBe(1);
        await active.DisposeAsync();
        await (await queued).DisposeAsync();
        var snapshot = sut.GetSnapshots().Single();
        snapshot.ActiveUploads.ShouldBe(0);
        snapshot.QueuedUploads.ShouldBe(0);
    }

    private sealed class CoordinatorMeterFactory : IMeterFactory
    {
        private readonly Meter meter = new(Metrics.MeterName);

        public Meter Create(MeterOptions options) => this.meter;

        public void Dispose() => this.meter.Dispose();
    }
}
