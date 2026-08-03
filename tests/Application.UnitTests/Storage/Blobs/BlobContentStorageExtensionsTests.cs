// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text;
using Application.Storage;
using BridgingIT.DevKit.Common;

[UnitTest("Application")]
public sealed class BlobContentStorageExtensionsTests
{
    [Fact]
    public async Task UploadTextAsync_WithDefaultOptions_UploadsUtf8TextAndMetadata()
    {
        // Arrange
        var blobs = CreateClient();
        var key = new BlobKey("notes", "readme.txt");

        // Act
        var result = await blobs.UploadTextAsync(
            key,
            "hello text",
            new BlobTextUploadOptions
            {
                Properties = new PropertyBag
                {
                    ["kind"] = "note"
                }
            });
        var download = await blobs.DownloadAsync(key);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Key.ShouldBe(key);
        result.Value.ContentType.ShouldBe(ContentType.TXT);
        result.Value.Length.ShouldBe(10);
        result.Value.Properties.Get<string>("kind").ShouldBe("note");

        download.IsSuccess.ShouldBeTrue();
        await using var blobDownload = download.Value;
        using var reader = new StreamReader(blobDownload.Content, Encoding.UTF8);
        (await reader.ReadToEndAsync()).ShouldBe("hello text");
    }

    [Fact]
    public async Task UploadTextAsync_WithBinaryContentType_ReturnsValidationFailureBeforeUpload()
    {
        // Arrange
        var blobs = new ScriptedBlobStoreClient();

        // Act
        var result = await blobs.UploadTextAsync(
            new BlobKey("notes", "binary.pdf"),
            "not a pdf",
            new BlobTextUploadOptions
            {
                ContentType = ContentType.PDF
            });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
        blobs.UploadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task DownloadTextAsync_WithExistingBlob_ReturnsTextAndInfoAndDisposesDownload()
    {
        // Arrange
        var key = new BlobKey("notes", "readme.txt");
        var stream = new TrackingStream(Encoding.UTF8.GetBytes("downloaded"));
        var blobs = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = stream,
                Info = new BlobInfo
                {
                    Key = key,
                    Length = 10,
                    ContentType = ContentType.TXT
                }
            })
        };

        // Act
        var result = await blobs.DownloadTextAsync(key);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Text.ShouldBe("downloaded");
        result.Value.Info.Key.ShouldBe(key);
        stream.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadTextAsync_WithBinaryContentType_ReturnsValidationFailure()
    {
        // Arrange
        var key = new BlobKey("notes", "manual.pdf");
        var blobs = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream([1, 2, 3]),
                Info = new BlobInfo
                {
                    Key = key,
                    Length = 3,
                    ContentType = ContentType.PDF
                }
            })
        };

        // Act
        var result = await blobs.DownloadTextAsync(key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Fact]
    public async Task UploadObjectAsync_WithDefaultSerializer_UploadsJsonBlob()
    {
        // Arrange
        var blobs = CreateClient();
        var key = new BlobKey("profiles", "user.json");
        var profile = new TestProfile("Ada", 37);

        // Act
        var result = await blobs.UploadObjectAsync(key, profile);
        var text = await blobs.DownloadTextAsync(key);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.ContentType.ShouldBe(ContentType.JSON);

        text.IsSuccess.ShouldBeTrue();
        text.Value.Text.ShouldContain("\"name\"");
        text.Value.Text.ShouldContain("Ada");
    }

    [Fact]
    public async Task DownloadObjectAsync_WithDefaultSerializer_ReturnsValueAndInfo()
    {
        // Arrange
        var blobs = CreateClient();
        var key = new BlobKey("profiles", "user.json");
        await blobs.UploadObjectAsync(key, new TestProfile("Grace", 44));

        // Act
        var result = await blobs.DownloadObjectAsync<TestProfile>(key);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Info.Key.ShouldBe(key);
        result.Value.Value.Name.ShouldBe("Grace");
        result.Value.Value.Age.ShouldBe(44);
    }

    [Fact]
    public async Task UploadObjectAsync_WithCustomSerializer_UsesSerializer()
    {
        // Arrange
        var blobs = CreateClient();
        var key = new BlobKey("profiles", "custom.txt");
        var serializer = new PipeDelimitedProfileSerializer();

        // Act
        var result = await blobs.UploadObjectAsync(
            key,
            new TestProfile("Linus", 55),
            new BlobObjectUploadOptions
            {
                Serializer = serializer,
                ContentType = ContentType.TXT
            });
        var text = await blobs.DownloadTextAsync(key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        serializer.SerializeCalls.ShouldBe(1);
        text.IsSuccess.ShouldBeTrue();
        text.Value.Text.ShouldBe("Linus|55");
    }

    [Fact]
    public async Task DownloadObjectAsync_WithCustomSerializer_ReturnsCustomValue()
    {
        // Arrange
        var key = new BlobKey("profiles", "custom.txt");
        var serializer = new PipeDelimitedProfileSerializer();
        var blobs = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("Margaret|61")),
                Info = new BlobInfo
                {
                    Key = key,
                    Length = 11,
                    ContentType = ContentType.TXT
                }
            })
        };

        // Act
        var result = await blobs.DownloadObjectAsync<TestProfile>(
            key,
            new BlobObjectDownloadOptions
            {
                Serializer = serializer
            });

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        serializer.DeserializeCalls.ShouldBe(1);
        result.Value.Info.Key.ShouldBe(key);
        result.Value.Value.Name.ShouldBe("Margaret");
        result.Value.Value.Age.ShouldBe(61);
    }

    [Fact]
    public async Task UploadObjectAsync_WhenSerializerThrows_ReturnsSerializationFailureBeforeUpload()
    {
        // Arrange
        var blobs = new ScriptedBlobStoreClient();

        // Act
        var result = await blobs.UploadObjectAsync(
            new BlobKey("profiles", "broken.json"),
            new TestProfile("Broken", 1),
            new BlobObjectUploadOptions
            {
                Serializer = ThrowingSerializer.Instance
            });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreSerializationError>().ShouldBeTrue();
        blobs.UploadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task DownloadObjectAsync_WhenBlobMissing_PreservesBlobFailure()
    {
        // Arrange
        var key = new BlobKey("profiles", "missing.json");
        var blobs = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key))
        };

        // Act
        var result = await blobs.DownloadObjectAsync<TestProfile>(key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
    }

    [Fact]
    public void PublicBlobStoreClientContract_WithContentHelpers_DoesNotExposeConvenienceMethods()
    {
        // Arrange & Act
        var methods = typeof(IBlobStoreClient).GetMethods().Select(method => method.Name).ToArray();

        // Assert
        methods.ShouldNotContain(nameof(BlobContentStorageExtensions.UploadTextAsync));
        methods.ShouldNotContain(nameof(BlobContentStorageExtensions.DownloadTextAsync));
        methods.ShouldNotContain(nameof(BlobContentStorageExtensions.UploadObjectAsync));
        methods.ShouldNotContain(nameof(BlobContentStorageExtensions.DownloadObjectAsync));
    }

    private static IBlobStoreClient CreateClient()
    {
        var provider = new InMemoryBlobStoreProvider();

        return new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
    }

    private sealed record TestProfile(string Name, int Age);

    private sealed class PipeDelimitedProfileSerializer : ISerializer
    {
        public int SerializeCalls { get; private set; }

        public int DeserializeCalls { get; private set; }

        public void Serialize(object value, Stream output)
        {
            this.SerializeCalls++;
            var profile = (TestProfile)value;
            var bytes = Encoding.UTF8.GetBytes($"{profile.Name}|{profile.Age}");
            output.Write(bytes, 0, bytes.Length);
        }

        public object Deserialize(Stream input, Type type)
        {
            this.DeserializeCalls++;
            using var reader = new StreamReader(input, Encoding.UTF8);
            var parts = reader.ReadToEnd().Split('|');

            return new TestProfile(parts[0], int.Parse(parts[1]));
        }

        public T Deserialize<T>(Stream input)
        {
            return (T)this.Deserialize(input, typeof(T));
        }
    }

    private sealed class ThrowingSerializer : ISerializer
    {
        public static readonly ThrowingSerializer Instance = new();

        public void Serialize(object value, Stream output)
        {
            throw new InvalidOperationException("serializer failed");
        }

        public object Deserialize(Stream input, Type type)
        {
            throw new InvalidOperationException("serializer failed");
        }

        public T Deserialize<T>(Stream input)
        {
            throw new InvalidOperationException("serializer failed");
        }
    }

    private sealed class ScriptedBlobStoreClient : IBlobStoreClient
    {
        public Func<BlobKey, Result<BlobDownload>> DownloadHandler { get; init; }

        public int UploadCalls { get; private set; }

        public Task<Result<BlobInfo>> UploadAsync(
            BlobUpload upload,
            CancellationToken cancellationToken = default)
        {
            this.UploadCalls++;

            return Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = upload.Key }));
        }

        public Task<Result<BlobDownload>> DownloadAsync(
            BlobKey key,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.DownloadHandler?.Invoke(key) ?? Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key)));
        }

        public Task<Result<BlobInfo>> GetPropertiesAsync(
            BlobKey key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = key }));

        public Task<Result<BlobInfo>> UpdatePropertiesAsync(
            BlobPropertiesUpdate update,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = update.Key }));

        public Task<Result<bool>> ExistsAsync(
            BlobKey key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<BlobPage>> ListPageAsync(
            BlobQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobPage>.Success(new BlobPage()));

        public Task<Result> DeleteAsync(
            BlobKey key,
            BlobDeleteOptions options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class TrackingStream(byte[] buffer) : MemoryStream(buffer)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            this.IsDisposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            this.IsDisposed = true;
            await base.DisposeAsync();
        }
    }
}
