// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Security.Cryptography;

[UnitTest("Common")]
public class StreamHelperTests
{
    [Fact]
    public async Task CopyAsync_WithHash_CopiesAndHashesFromCurrentPosition()
    {
        // Arrange
        await using var source = new MemoryStream("xpayload"u8.ToArray()) { Position = 1 };
        await using var destination = new MemoryStream();

        // Act
        var result = await StreamHelper.CopyAsync(source, destination, new StreamCopyOptions
        {
            BufferSize = 2,
            HashAlgorithm = HashAlgorithmName.SHA256
        });

        // Assert
        result.Length.ShouldBe(7);
        result.Hash.ShouldBe(Convert.ToHexStringLower(SHA256.HashData("payload"u8.ToArray())));
        destination.ToArray().ShouldBe("payload"u8.ToArray());
        source.CanRead.ShouldBeTrue();
        destination.CanWrite.ShouldBeTrue();
    }

    [Fact]
    public async Task CopyAsync_WhenLimitExceeded_WritesAtMostMaximum()
    {
        // Arrange
        await using var source = new MemoryStream("123456"u8.ToArray());
        await using var destination = new MemoryStream();

        // Act
        var exception = await Should.ThrowAsync<StreamSizeLimitExceededException>(() =>
            StreamHelper.CopyAsync(source, destination, new StreamCopyOptions { MaximumBytes = 4, BufferSize = 3 }));

        // Assert
        exception.MaximumBytes.ShouldBe(4);
        destination.Length.ShouldBe(4);
    }

    [Fact]
    public async Task CopyAsync_WhenLengthEqualsLimit_CompletesSuccessfully()
    {
        // Arrange
        await using var source = new MemoryStream("1234"u8.ToArray());
        await using var destination = new MemoryStream();

        // Act
        var result = await StreamHelper.CopyAsync(
            source,
            destination,
            new StreamCopyOptions { MaximumBytes = 4, BufferSize = 3 });

        // Assert
        result.Length.ShouldBe(4);
        destination.ToArray().ShouldBe("1234"u8.ToArray());
    }

    [Fact]
    public async Task CopyAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        await using var source = new MemoryStream(new byte[10]);
        await using var destination = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            StreamHelper.CopyAsync(source, destination, cancellationToken: cancellation.Token));
    }
}
