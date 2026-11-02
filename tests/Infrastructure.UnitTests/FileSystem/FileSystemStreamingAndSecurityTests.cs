namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;
using System.Security.Cryptography;

public sealed class FileSystemStreamingAndSecurityTests
{
    [LocalFileSystemTheory]
    [InlineData(4096)]
    [InlineData(65536)]
    [InlineData(1048576)]
    [InlineData(16777216)]
    [InlineData(134217728)]
    public async Task NonSeekableStreaming_SizeMatrix_VerifiesBytesWithoutPayloadSizedFixture(int size)
    {
        using var root = new OwnedFileSystemRoot();
        var sut = new FileSystemBlobStoreProvider(root.Options);
        using var source = new ConstantStream(size);
        var key = new BlobKey("stream", "large");
        var info = await sut.UploadAsync(new() { Key = key, Content = source });
        info.IsSuccess.ShouldBeTrue();
        source.CanRead.ShouldBeTrue();
        using var expected = new ConstantStream(size);
        var expectedHash = await SHA256.HashDataAsync(expected);
        var download = await sut.DownloadAsync(key);
        await using var stream = download.Value.Content;
        var actual = await SHA256.HashDataAsync(stream);
        actual.ShouldBe(expectedHash);
        download.Value.Info.Length.ShouldBe(size);
    }

    [LocalFileSystemTheory]
    [InlineData(1024)]
    [InlineData(16384)]
    [InlineData(65536)]
    [InlineData(262144)]
    [InlineData(1048000)]
    public async Task TypedDocuments_LegalPayloadSizes_RoundTripWithDefaultDurability(int size)
    {
        using var root = new OwnedFileSystemRoot();
        IDocumentStoreClient<Document> sut = new DocumentStoreClient<Document>(new FileSystemDocumentStoreProvider(root.Options));
        var value = new string ('X', size);
        var key = new DocumentKey("matrix", "size-" + size);
        (await sut.UpsertAsync(key, new() { Value = value })).IsSuccess.ShouldBeTrue();
        (await sut.GetAsync(key)).Value.Value.Value.ShouldBe(value);
    }

    [SkippableFact]
    public async Task ReparseTraversal_IsRejectedBeforeWritingOutsideManagedRoot()
    {
        Skip.If(!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux(), "Windows/Linux only.");
        using var root = new OwnedFileSystemRoot();
        using var outside = new OwnedFileSystemRoot();
        var link = Path.Combine(root.Path, "blobs");

        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, outside.Path);
            }
            catch (Exception exception)when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                Skip.If(true, "Creating a test symlink requires unavailable OS privileges or filesystem support.");
            }

            var sut = new FileSystemBlobStoreProvider(root.Options);
            var result = await sut.UploadAsync(new() { Key = new("catalog", "escape"), Content = new MemoryStream([1]) });
            result.HasError<FileSystemStorageCorruptionError>().ShouldBeTrue();
            Directory.EnumerateFileSystemEntries(outside.Path).Select(Path.GetFileName).ShouldBe(new[] { "run.json" });
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
        }
    }

    [LocalFileSystemFact]
    public async Task PermissionAndCapacityClassifications_AreNonRetryableExpectedFailures()
    {
        var permission = await FileSystemResults.BlobAsync<bool>(() => throw new UnauthorizedAccessException());
        permission.HasError<FileSystemStoragePermissionError>().ShouldBeTrue();
        var code = OperatingSystem.IsWindows() ? unchecked((int)0x80070070) : 28;
        var capacity = await FileSystemResults.DocumentAsync<bool>(() => throw new IOException("Injected full disk.", code));
        capacity.HasError<FileSystemStorageCapacityError>().ShouldBeTrue();
        capacity.HasError<DocumentStoreProviderError>().ShouldBeFalse();
    }

    public sealed class Document
    {
        public string Value { get; set; }
    }

    private sealed class ConstantStream(long length) : Stream
    {
        private long position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var take = (int)Math.Min(count, length - this.position);
            buffer.AsSpan(offset, take).Fill(0x5a);
            this.position += take;

            return take;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var take = (int)Math.Min(buffer.Length, length - this.position);
            buffer.Span[..take].Fill(0x5a);
            this.position += take;

            return ValueTask.FromResult(take);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}