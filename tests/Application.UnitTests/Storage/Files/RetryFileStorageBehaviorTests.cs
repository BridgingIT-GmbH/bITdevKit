// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Diagnostics;
using Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging.Abstractions;

[UnitTest("Application")]
public sealed class RetryFileStorageBehaviorTests
{
    [Fact]
    public async Task FileExistsAsync_WhenRetryableFailureIsFollowedBySuccess_RetriesWithBackoff()
    {
        // Arrange
        var provider = new CountingFileStorageProvider(
            [Result.Failure().WithError(new FileSystemError("transient", "a.txt")), Result.Success()]);
        var sut = new RetryFileStorageBehavior(provider, NullLoggerFactory.Instance, new Application.Storage.RetryOptions { MaxRetries = 1 });
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await sut.FileExistsAsync("a.txt");

        // Assert (first retry waits 2 seconds)
        result.IsSuccess.ShouldBeTrue();
        provider.Calls.ShouldBe(2);
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(1900);
    }

    [Fact]
    public async Task FileExistsAsync_WhenMaxRetriesIsZero_DoesNotRetry()
    {
        // Arrange
        var provider = new CountingFileStorageProvider([Result.Failure().WithError(new FileSystemError("transient", "a.txt"))]);
        var sut = new RetryFileStorageBehavior(provider, NullLoggerFactory.Instance, new Application.Storage.RetryOptions { MaxRetries = 0 });

        // Act
        var result = await sut.FileExistsAsync("a.txt");

        // Assert
        result.IsFailure.ShouldBeTrue();
        provider.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task FileExistsAsync_WhenFailureIsNotRetryable_DoesNotRetryAndReturnsFailure()
    {
        // Arrange
        var provider = new CountingFileStorageProvider([Result.Failure().WithError(new NotFoundError("missing"))]);
        var sut = new RetryFileStorageBehavior(provider, NullLoggerFactory.Instance, new Application.Storage.RetryOptions { MaxRetries = 2 });

        // Act
        var result = await sut.FileExistsAsync("a.txt");

        // Assert
        result.IsFailure.ShouldBeTrue();
        provider.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task ReadFileAsync_WhenRetryableFailureIsFollowedBySuccess_RetriesAndReturnsSuccessfulResult()
    {
        // Arrange
        var provider = new CountingFileStorageProvider(
            [],
            [Result<Stream>.Failure().WithError(new FileSystemError("transient", "a.txt")), Result<Stream>.Success(new MemoryStream([1, 2, 3]))]);
        var sut = new RetryFileStorageBehavior(provider, NullLoggerFactory.Instance, new Application.Storage.RetryOptions { MaxRetries = 1 });

        // Act
        var result = await sut.ReadFileAsync("a.txt");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Length.ShouldBe(3);
        provider.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task ReadFileAsync_WhenRetryableFailurePersists_ReturnsFailureAfterAllRetries()
    {
        // Arrange
        var provider = new CountingFileStorageProvider(
            [],
            [Result<Stream>.Failure().WithError(new FileSystemError("transient", "a.txt"))]);
        var sut = new RetryFileStorageBehavior(provider, NullLoggerFactory.Instance, new Application.Storage.RetryOptions { MaxRetries = 1 });

        // Act
        var result = await sut.ReadFileAsync("a.txt");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e is FileSystemError);
        provider.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task ReadFileAsync_WhenFailureIsNotRetryable_DoesNotRetryAndReturnsFailure()
    {
        // Arrange
        var provider = new CountingFileStorageProvider([], [Result<Stream>.Failure().WithError(new NotFoundError("missing"))]);
        var sut = new RetryFileStorageBehavior(provider, NullLoggerFactory.Instance, new Application.Storage.RetryOptions { MaxRetries = 2 });

        // Act
        var result = await sut.ReadFileAsync("a.txt");

        // Assert
        result.IsFailure.ShouldBeTrue();
        provider.Calls.ShouldBe(1);
    }

    private sealed class CountingFileStorageProvider(Result[] results, Result<Stream>[] readResults = null) : BaseFileStorageProvider("test")
    {
        public int Calls { get; private set; }

        public override Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(readResults[Math.Min(this.Calls++, readResults.Length - 1)]);
        }

        public override Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(results[Math.Min(this.Calls++, results.Length - 1)]);
        }
    }
}
