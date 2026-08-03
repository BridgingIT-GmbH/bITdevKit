// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Application")]
public sealed class InMemoryBlobStorageRegistrationTests
{
    [Fact]
    public async Task WithInMemoryClient_WithConfiguredOptions_UsesSameOptionsForProviderEnforcement()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithInMemoryClient("reports", options => options.MaxBlobSize = 3);
        using var serviceProvider = services.BuildServiceProvider();
        var sut = serviceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        var key = new BlobKey("reports", "large.bin");

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new NonSeekableReadStream([1, 2, 3, 4])
        });
        var exists = await sut.ExistsAsync(key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreSizeLimitExceededError>().ShouldBeTrue();
        exists.Value.ShouldBeFalse();
    }

    private sealed class NonSeekableReadStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}
