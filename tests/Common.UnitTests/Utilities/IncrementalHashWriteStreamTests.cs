// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Security.Cryptography;

public sealed class IncrementalHashWriteStreamTests
{
    [Fact]
    public async Task Write_WithSlicesAndFlushes_AppendsOnlyRequestedBytesAndLeavesHashOwnedByCaller()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = "_report_"u8.ToArray();
        hash.AppendData("inspection:"u8);

        await using (var sut = new IncrementalHashWriteStream(hash))
        {
            sut.Write(buffer, 1, 2);
            sut.Flush();
            sut.Write(buffer.AsSpan(3, 1));
            await sut.WriteAsync(buffer, 4, 1, CancellationToken.None);
            await sut.WriteAsync(buffer.AsMemory(5, 2));
            await sut.FlushAsync();
            sut.WriteByte((byte)'!');
        }

        hash.AppendData("done"u8);
        hash.GetHashAndReset().ShouldBe(SHA256.HashData("inspection:report!done"u8));
    }

    [Fact]
    public async Task Write_WithCanceledStreamToken_LeavesPreviouslyAppendedBytesIntact()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var cancellation = new CancellationTokenSource();
        using var sut = new IncrementalHashWriteStream(hash, cancellation.Token);
        var bytes = "inspection"u8.ToArray();
        sut.Write(bytes);
        cancellation.Cancel();

        Should.Throw<OperationCanceledException>(() => sut.Write(bytes, 0, bytes.Length))
            .CancellationToken.ShouldBe(cancellation.Token);
        var pending = sut.WriteAsync(bytes.AsMemory());
        (await Should.ThrowAsync<OperationCanceledException>(() => pending.AsTask()))
            .CancellationToken.ShouldBe(cancellation.Token);

        hash.GetHashAndReset().ShouldBe(SHA256.HashData(bytes));
    }

    [Fact]
    public async Task WriteAsync_WithCanceledWriteToken_LeavesHashAvailableForFutureWrites()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sut = new IncrementalHashWriteStream(hash);
        var bytes = "inspection"u8.ToArray();
        var canceled = new CancellationToken(true);

        var arrayWrite = sut.WriteAsync(bytes, 0, bytes.Length, canceled);
        var memoryWrite = sut.WriteAsync(bytes.AsMemory(), canceled);

        arrayWrite.IsCanceled.ShouldBeTrue();
        memoryWrite.IsCanceled.ShouldBeTrue();
        (await Should.ThrowAsync<OperationCanceledException>(() => arrayWrite)).CancellationToken.ShouldBe(canceled);
        (await Should.ThrowAsync<OperationCanceledException>(() => memoryWrite.AsTask())).CancellationToken.ShouldBe(canceled);
        await sut.WriteAsync(bytes.AsMemory());
        hash.GetHashAndReset().ShouldBe(SHA256.HashData(bytes));
    }

    [Fact]
    public void Dispose_WithOpenHash_ClosesOnlyTheWriteStream()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sut = new IncrementalHashWriteStream(hash);

        sut.CanRead.ShouldBeFalse();
        sut.CanSeek.ShouldBeFalse();
        sut.CanWrite.ShouldBeTrue();
        Should.Throw<NotSupportedException>(() => sut.ReadByte());
        Should.Throw<NotSupportedException>(() => sut.SetLength(0));
        Should.Throw<NotSupportedException>(() => sut.Seek(0, SeekOrigin.Begin));

        sut.Write("inspection"u8);
        sut.Dispose();

        sut.CanWrite.ShouldBeFalse();
        Should.Throw<ObjectDisposedException>(() => sut.WriteByte(0));
        hash.GetHashAndReset().ShouldBe(SHA256.HashData("inspection"u8));
    }
}
