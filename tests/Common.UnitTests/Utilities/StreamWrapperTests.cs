// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public sealed class StreamWrapperTests
{
    [Fact]
    public async Task ForwardOnlyReadStream_LeaveOpen_HidesSeekingAndPreservesSource()
    {
        // Arrange
        using var source = new MemoryStream("abc"u8.ToArray());
        var sut = new ForwardOnlyReadStream(source, leaveOpen: true);
        var buffer = new byte[2];

        // Act
        sut.Read(buffer, 0, 1).ShouldBe(1);
        (await sut.ReadAsync(buffer.AsMemory(1))).ShouldBe(1);
        await sut.DisposeAsync();

        // Assert
        buffer.ShouldBe("ab"u8.ToArray());
        sut.CanRead.ShouldBeFalse();
        sut.CanWrite.ShouldBeFalse();
        sut.CanSeek.ShouldBeFalse();
        Should.Throw<NotSupportedException>(() => sut.Seek(0, SeekOrigin.Begin));
        Should.Throw<ObjectDisposedException>(() => sut.ReadByte());
        source.ReadByte().ShouldBe((int)'c');
    }

    [Fact]
    public async Task RangeReadStream_PositionedSource_StopsAtRangeBoundaryWithoutClosingSource()
    {
        // Arrange
        using var source = new MemoryStream("xabcde"u8.ToArray()) { Position = 1 };
        var sut = new RangeReadStream(source, 3);
        var buffer = new byte[8];

        // Act
        sut.Read(buffer, 0, 1).ShouldBe(1);
        (await sut.ReadAsync(buffer.AsMemory(1))).ShouldBe(2);
        (await sut.ReadAsync(buffer)).ShouldBe(0);
        await sut.DisposeAsync();

        // Assert
        buffer[..3].ShouldBe("abc"u8.ToArray());
        sut.Position.ShouldBe(3);
        sut.CanRead.ShouldBeFalse();
        source.Position.ShouldBe(4);
        source.ReadByte().ShouldBe((int)'d');
        Should.Throw<ObjectDisposedException>(() => sut.ReadByte());
    }

    [Fact]
    public void LimitedWriteStream_CaughtLimitException_PermanentlyRejectsAttempt()
    {
        // Arrange
        using var destination = new MemoryStream();
        var sut = new LimitedWriteStream(destination, 3);
        sut.Write("ab"u8);

        // Act
        var exception = Should.Throw<StreamSizeLimitExceededException>(() => sut.Write("cd"u8));

        // Assert
        exception.MaximumBytes.ShouldBe(3);
        exception.ObservedBytes.ShouldBe(4);
        sut.Written.ShouldBe(2);
        sut.HasFailed.ShouldBeTrue();
        sut.CompletedSuccessfully.ShouldBeFalse();
        Should.Throw<StreamSizeLimitExceededException>(() => sut.Write("c"u8));
        destination.ToArray().ShouldBe("ab"u8.ToArray());
        sut.Dispose();
        destination.CanWrite.ShouldBeTrue();
    }

    [Fact]
    public async Task LimitedWriteStream_ExactLimit_CompletesAndLeavesDestinationOpen()
    {
        // Arrange
        using var destination = new MemoryStream();
        var sut = new LimitedWriteStream(destination, 3);

        // Act
        sut.Write("a"u8);
        await sut.WriteAsync("bc"u8.ToArray());
        await sut.FlushAsync();

        // Assert
        sut.CompletedSuccessfully.ShouldBeTrue();
        sut.HasFailed.ShouldBeFalse();
        sut.Written.ShouldBe(3);
        destination.ToArray().ShouldBe("abc"u8.ToArray());
        await sut.DisposeAsync();
        destination.CanWrite.ShouldBeTrue();
        await Should.ThrowAsync<ObjectDisposedException>(() => sut.WriteAsync(new byte[1]).AsTask());
    }

    [Fact]
    public async Task LimitedWriteStream_CancelledDestinationWrite_PermanentlyRejectsAttempt()
    {
        // Arrange
        using var destination = new MemoryStream();
        using var sut = new LimitedWriteStream(destination, 3);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => sut.WriteAsync(new byte[1], cancellation.Token).AsTask());

        // Assert
        sut.Written.ShouldBe(0);
        sut.HasFailed.ShouldBeTrue();
        sut.CompletedSuccessfully.ShouldBeFalse();
        await Should.ThrowAsync<StreamSizeLimitExceededException>(() => sut.WriteAsync(new byte[1]).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LimitedWriteStream_CaughtFlushFailure_PermanentlyRejectsAttempt(bool asynchronous)
    {
        // Arrange
        using var destination = new FailingFlushStream();
        using var sut = new LimitedWriteStream(destination, 3);
        sut.Write("a"u8);

        // Act
        if (asynchronous)
        {
            await Should.ThrowAsync<IOException>(() => sut.FlushAsync());
        }
        else
        {
            Should.Throw<IOException>(sut.Flush);
        }

        // Assert
        sut.Written.ShouldBe(1);
        sut.HasFailed.ShouldBeTrue();
        sut.CompletedSuccessfully.ShouldBeFalse();
        Should.Throw<StreamSizeLimitExceededException>(() => sut.Write("b"u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedStream_UnderlyingDisposalFails_ReleasesResourceExactlyOnce(bool asynchronous)
    {
        // Arrange
        var source = new FailingDisposeStream();
        var releases = 0;
        var sut = new OwnedStream(source, () => { releases++; return ValueTask.CompletedTask; });

        // Act
        if (asynchronous)
        {
            await Should.ThrowAsync<IOException>(() => sut.DisposeAsync().AsTask());
        }
        else
        {
            Should.Throw<IOException>(sut.Dispose);
        }

        sut.Dispose();
        await sut.DisposeAsync();

        // Assert
        releases.ShouldBe(1);
        source.DisposalAttempts.ShouldBe(1);
        sut.CanRead.ShouldBeFalse();
    }

    [Fact]
    public void Constructors_InvalidSourcesOrBounds_RejectInvalidConfiguration()
    {
        // Arrange
        using var readable = new MemoryStream(new byte[1], writable: false);
        using var writeOnly = new WriteOnlyStream();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ForwardOnlyReadStream(null));
        Should.Throw<ArgumentException>(() => new ForwardOnlyReadStream(writeOnly));
        Should.Throw<ArgumentOutOfRangeException>(() => new RangeReadStream(readable, -1));
        Should.Throw<ArgumentException>(() => new RangeReadStream(writeOnly, 1));
        Should.Throw<ArgumentNullException>(() => new LimitedWriteStream(null, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => new LimitedWriteStream(writeOnly, -1));
        Should.Throw<ArgumentException>(() => new LimitedWriteStream(readable, 1));
        Should.Throw<ArgumentNullException>(() => new OwnedStream(readable, (Action)null));
        Should.Throw<ArgumentNullException>(() => new OwnedStream(null, () => { }));
    }

    private sealed class WriteOnlyStream : MemoryStream
    {
        public override bool CanRead => false;
    }

    private sealed class FailingFlushStream : MemoryStream
    {
        public override void Flush() => throw new IOException("Underlying flush failed.");

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.FromException(new IOException("Underlying flush failed."));
    }

    private sealed class FailingDisposeStream : MemoryStream
    {
        public int DisposalAttempts { get; private set; }

        public override ValueTask DisposeAsync()
        {
            this.DisposalAttempts++;
            return ValueTask.FromException(new IOException("Underlying disposal failed."));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposalAttempts++;
                throw new IOException("Underlying disposal failed.");
            }

            base.Dispose(disposing);
        }
    }
}