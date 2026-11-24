// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public sealed class BlobDimensionsRetryTests
{
    [Fact]
    public async Task Upload_WithDimensions_DoesNotReplayConsumedStream()
    {
        var inner = Substitute.For<IBlobStoreClient>();
        inner.UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await call.Arg<BlobUpload>().Content.CopyToAsync(Stream.Null);
            return Result<BlobInfo>.Failure(new BlobStoreProviderError("Final publication failed."));
        });
        var sut = new RetryBlobStoreClientBehavior(inner, new() { Attempts = 3 });
        using var stream = new MemoryStream([1, 2, 3]);
        var result = await sut.UploadAsync(new() { Key = new("archive", "owner"), Content = stream,
            Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }).Value });
        result.IsFailure.ShouldBeTrue();
        await inner.Received(1).UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>());
        stream.Position.ShouldBe(3);
    }

    [Fact]
    public async Task DimensionsUpdate_RegistrationBudgetExhausted_IsNotRetried()
    {
        var inner = Substitute.For<IBlobStoreClient>();
        inner.UpdateDimensionsAsync(Arg.Any<BlobDimensionsUpdate>(), Arg.Any<CancellationToken>())
            .Returns(Result<BlobInfo>.Failure(new BlobDimensionRegistrationContentionError()));
        var sut = new RetryBlobStoreClientBehavior(inner, new() { Attempts = 3 });
        var result = await sut.UpdateDimensionsAsync(new() { Key = new("archive", "owner"), Dimensions = BlobDimensions.Empty });
        result.HasError<BlobDimensionRegistrationContentionError>().ShouldBeTrue();
        await inner.Received(1).UpdateDimensionsAsync(Arg.Any<BlobDimensionsUpdate>(), Arg.Any<CancellationToken>());
    }
}
