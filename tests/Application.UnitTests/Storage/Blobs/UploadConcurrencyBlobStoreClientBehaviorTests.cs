// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

[UnitTest("Application.Storage")]
public class UploadConcurrencyBlobStoreClientBehaviorTests
{
    [Fact]
    public async Task UploadAsync_WhenQueueIsFull_DoesNotInvokeInnerClient()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = Substitute.For<IBlobStoreClient>();
        inner.UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                entered.TrySetResult();
                await release.Task;
                return Result<BlobInfo>.Success(new BlobInfo());
            });
        using var coordinator = new BlobUploadAdmissionCoordinator();
        var sut = new UploadConcurrencyBlobStoreClientBehavior(
            inner,
            coordinator,
            new UploadConcurrencyBlobStoreClientBehaviorOptions
            {
                MaxConcurrentUploads = 1,
                MaxQueuedUploads = 0
            },
            storeName: "reports");
        using var firstContent = new MemoryStream([1]);
        using var secondContent = new MemoryStream([2]);
        var first = sut.UploadAsync(CreateUpload("first", firstContent));
        await entered.Task;

        var rejected = await sut.UploadAsync(CreateUpload("second", secondContent));
        release.TrySetResult();
        await first;

        rejected.HasError<BlobStoreUploadOverloadedError>().ShouldBeTrue();
        await inner.Received(1).UploadAsync(
            Arg.Any<BlobUpload>(),
            Arg.Any<CancellationToken>());
        secondContent.Position.ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_WhenInnerThrows_ReleasesPermit()
    {
        var inner = Substitute.For<IBlobStoreClient>();
        inner.UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new InvalidOperationException("boom"),
                _ => Result<BlobInfo>.Success(new BlobInfo()));
        using var coordinator = new BlobUploadAdmissionCoordinator();
        var sut = new UploadConcurrencyBlobStoreClientBehavior(
            inner,
            coordinator,
            new UploadConcurrencyBlobStoreClientBehaviorOptions
            {
                MaxConcurrentUploads = 1,
                MaxQueuedUploads = 0
            },
            storeName: "reports");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.UploadAsync(CreateUpload("first", new MemoryStream([1]))));
        var result = await sut.UploadAsync(CreateUpload("second", new MemoryStream([2])));

        result.IsSuccess.ShouldBeTrue();
        coordinator.GetSnapshots().Single().ActiveUploads.ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_WhenCallerIsAlreadyCanceled_DoesNotInvokeInnerClient()
    {
        var inner = Substitute.For<IBlobStoreClient>();
        using var coordinator = new BlobUploadAdmissionCoordinator();
        var sut = new UploadConcurrencyBlobStoreClientBehavior(
            inner,
            coordinator,
            new UploadConcurrencyBlobStoreClientBehaviorOptions(),
            storeName: "reports");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => sut.UploadAsync(
            CreateUpload("canceled", new MemoryStream([1])),
            cancellation.Token);

        await action.ShouldThrowAsync<OperationCanceledException>();
        await inner.DidNotReceive().UploadAsync(
            Arg.Any<BlobUpload>(),
            Arg.Any<CancellationToken>());
        coordinator.GetSnapshots().Single().ActiveUploads.ShouldBe(0);
    }

    [Fact]
    public async Task ExistsAsync_BypassesUploadAdmission()
    {
        var inner = Substitute.For<IBlobStoreClient>();
        inner.ExistsAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        using var coordinator = new BlobUploadAdmissionCoordinator();
        await using var active = await coordinator.AcquireAsync(
            "reports",
            new UploadConcurrencyBlobStoreClientBehaviorOptions
            {
                MaxConcurrentUploads = 1,
                MaxQueuedUploads = 0
            },
            default);
        var sut = new UploadConcurrencyBlobStoreClientBehavior(
            inner,
            coordinator,
            new UploadConcurrencyBlobStoreClientBehaviorOptions
            {
                MaxConcurrentUploads = 1,
                MaxQueuedUploads = 0
            },
            storeName: "reports");

        var result = await sut.ExistsAsync(new BlobKey("reports", "probe"));

        result.Value.ShouldBeTrue();
        await inner.Received(1).ExistsAsync(
            Arg.Any<BlobKey>(),
            Arg.Any<CancellationToken>());
    }

    private static BlobUpload CreateUpload(string name, Stream content) =>
        new()
        {
            Key = new BlobKey("reports", $"{name}.bin"),
            Content = content
        };
}
