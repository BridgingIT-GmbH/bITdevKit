// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Application")]
public sealed class BlobStoreCacheBehaviorTests
{
    [Fact]
    public async Task DownloadAsync_WithCacheMiss_CachesContentAndReturnsDownload()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var cache = new RecordingCacheProvider();
        var sut = new CacheBlobStoreClientBehavior(null, inner, cache, storeName: "reports");
        var key = new BlobKey("reports", "content.txt");

        // Act
        var first = await sut.DownloadAsync(key);
        var second = await sut.DownloadAsync(key);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        inner.DownloadCalls.ShouldBe(1);
        cache.SetCalls.ShouldBe(1);

        await using (first.Value)
        await using (second.Value)
        {
            first.Value.Content.ShouldNotBeSameAs(second.Value.Content);
            (await ReadStringAsync(first.Value.Content)).ShouldBe("content-1");
            (await ReadStringAsync(second.Value.Content)).ShouldBe("content-1");
        }
    }

    [Fact]
    public async Task DownloadAsync_WithCachedContent_ReturnsNewReadOnlyStreamPerHit()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var cache = new RecordingCacheProvider();
        var sut = new CacheBlobStoreClientBehavior(null, inner, cache, storeName: "reports");
        var key = new BlobKey("reports", "content.txt");
        var first = await sut.DownloadAsync(key);
        await first.Value.DisposeAsync();

        // Act
        var second = await sut.DownloadAsync(key);
        var third = await sut.DownloadAsync(key);

        // Assert
        second.IsSuccess.ShouldBeTrue();
        third.IsSuccess.ShouldBeTrue();
        second.Value.Content.ShouldNotBeSameAs(third.Value.Content);
        second.Value.Content.CanWrite.ShouldBeFalse();
        third.Value.Content.CanWrite.ShouldBeFalse();

        await second.Value.DisposeAsync();
        await third.Value.DisposeAsync();
    }

    [Fact]
    public async Task UploadAsync_WhenSuccessful_InvalidatesCachedDownload()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var cache = new RecordingCacheProvider();
        var sut = new CacheBlobStoreClientBehavior(null, inner, cache, storeName: "reports");
        var key = new BlobKey("reports", "content.txt");
        var cached = await sut.DownloadAsync(key);
        await cached.Value.DisposeAsync();

        // Act
        var upload = await sut.UploadAsync(CreateUpload(key));
        var afterUpload = await sut.DownloadAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue();
        afterUpload.IsSuccess.ShouldBeTrue();
        inner.DownloadCalls.ShouldBe(2);
        cache.RemoveCalls.ShouldBe(1);
        cache.RemoveStartsWithCalls.ShouldBe(1);

        await afterUpload.Value.DisposeAsync();
    }

    [Fact]
    public async Task UpdatePropertiesAsync_WhenSuccessful_InvalidatesCachedDownload()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var cache = new RecordingCacheProvider();
        var sut = new CacheBlobStoreClientBehavior(null, inner, cache, storeName: "reports");
        var key = new BlobKey("reports", "content.txt");
        var cached = await sut.DownloadAsync(key);
        await cached.Value.DisposeAsync();

        // Act
        var update = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = key,
            ContentType = ContentType.TXT
        });
        var afterUpdate = await sut.DownloadAsync(key);

        // Assert
        update.IsSuccess.ShouldBeTrue();
        afterUpdate.IsSuccess.ShouldBeTrue();
        inner.DownloadCalls.ShouldBe(2);

        await afterUpdate.Value.DisposeAsync();
    }

    [Fact]
    public async Task DeleteAsync_WhenSuccessful_InvalidatesCachedDownload()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var cache = new RecordingCacheProvider();
        var sut = new CacheBlobStoreClientBehavior(null, inner, cache, storeName: "reports");
        var key = new BlobKey("reports", "content.txt");
        var cached = await sut.DownloadAsync(key);
        await cached.Value.DisposeAsync();

        // Act
        var delete = await sut.DeleteAsync(key);
        var afterDelete = await sut.DownloadAsync(key);

        // Assert
        delete.IsSuccess.ShouldBeTrue();
        afterDelete.IsSuccess.ShouldBeTrue();
        inner.DownloadCalls.ShouldBe(2);

        await afterDelete.Value.DisposeAsync();
    }

    [Fact]
    public async Task DownloadAsync_WhenBlobExceedsCacheLimit_DoesNotCache()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var cache = new RecordingCacheProvider();
        var sut = new CacheBlobStoreClientBehavior(
            null,
            inner,
            cache,
            new CacheBlobStoreClientBehaviorOptions { MaxCachedBlobSize = 1 },
            "reports");
        var key = new BlobKey("reports", "content.txt");

        // Act
        var first = await sut.DownloadAsync(key);
        var second = await sut.DownloadAsync(key);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        inner.DownloadCalls.ShouldBe(2);
        cache.SetCalls.ShouldBe(0);

        await first.Value.DisposeAsync();
        await second.Value.DisposeAsync();
    }

    [Fact]
    public async Task DownloadAsync_WithInvalidOptions_ReturnsValidationFailureWithoutCallingInner()
    {
        // Arrange
        var inner = new CountingBlobStoreClient();
        var cache = new RecordingCacheProvider();
        var sut = new CacheBlobStoreClientBehavior(
            null,
            inner,
            cache,
            new CacheBlobStoreClientBehaviorOptions { BufferSize = 0 },
            "reports");

        // Act
        var result = await sut.DownloadAsync(new BlobKey("reports", "content.txt"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
        inner.DownloadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AddBlobStorage_WithCacheBehavior_RegistersNamedClient()
    {
        // Arrange
        var cache = new RecordingCacheProvider();
        var services = new ServiceCollection();
        services.AddSingleton<ICacheProvider>(cache);
        services.AddBlobStorage()
            .WithCacheBehavior()
            .WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        var key = new BlobKey("reports", "content.txt");
        var upload = await client.UploadAsync(CreateUpload(key));

        // Act
        var first = await client.DownloadAsync(key);
        var second = await client.DownloadAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue();
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        cache.SetCalls.ShouldBe(1);

        await first.Value.DisposeAsync();
        await second.Value.DisposeAsync();
    }

    private static BlobUpload CreateUpload(BlobKey key) => new()
    {
        Key = key,
        Content = new MemoryStream(Encoding.UTF8.GetBytes("content")),
        ContentType = ContentType.TXT
    };

    private static async Task<string> ReadStringAsync(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }

    private sealed class CountingBlobStoreClient : IBlobStoreClient
    {
        public int DownloadCalls { get; private set; }

        public Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo
            {
                Key = upload.Key,
                Length = upload.Content?.Length ?? 0,
                ContentType = upload.ContentType
            }));

        public Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default)
        {
            this.DownloadCalls++;
            var content = Encoding.UTF8.GetBytes($"content-{this.DownloadCalls}");

            return Task.FromResult(Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(content),
                Info = new BlobInfo
                {
                    Key = key,
                    Length = content.Length,
                    ContentType = ContentType.TXT
                }
            }));
        }

        public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = key }));

        public Task<Result<BlobInfo>> UpdatePropertiesAsync(BlobPropertiesUpdate update, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo
            {
                Key = update.Key,
                ContentType = update.ContentType
            }));

        public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobPage>.Success(new BlobPage()));

        public Task<Result> DeleteAsync(
            BlobKey key,
            BlobDeleteOptions options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class RecordingCacheProvider : ICacheProvider
    {
        private readonly Dictionary<string, object> entries = new();

        public int RemoveCalls { get; private set; }

        public int RemoveStartsWithCalls { get; private set; }

        public int SetCalls { get; private set; }

        public T Get<T>(string key) =>
            this.TryGet(key, out T value) ? value : default;

        public Task<T> GetAsync<T>(string key, CancellationToken token = default) =>
            Task.FromResult(this.Get<T>(key));

        public bool TryGet<T>(string key, out T value)
        {
            if (this.entries.TryGetValue(key, out var entry) && entry is T typed)
            {
                value = typed;

                return true;
            }

            value = default;

            return false;
        }

        public Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default) =>
            Task.FromResult(this.TryGet(key, out value));

        public IEnumerable<string> GetKeys() => this.entries.Keys;

        public Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default) =>
            Task.FromResult<IEnumerable<string>>(this.entries.Keys);

        public void Remove(string key)
        {
            this.RemoveCalls++;
            this.entries.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            this.Remove(key);

            return Task.CompletedTask;
        }

        public void RemoveStartsWith(string key)
        {
            this.RemoveStartsWithCalls++;

            foreach (var entryKey in this.entries.Keys.Where(entryKey => entryKey.StartsWith(key, StringComparison.Ordinal)).ToList())
            {
                this.entries.Remove(entryKey);
            }
        }

        public Task RemoveStartsWithAsync(string key, CancellationToken token = default)
        {
            this.RemoveStartsWith(key);

            return Task.CompletedTask;
        }

        public void Set<T>(
            string key,
            T value,
            TimeSpan? slidingExpiration = null,
            DateTimeOffset? absoluteExpiration = null)
        {
            this.SetCalls++;
            this.entries[key] = value;
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? slidingExpiration = null,
            DateTimeOffset? absoluteExpiration = null,
            CancellationToken cancellationToken = default)
        {
            this.Set(key, value, slidingExpiration, absoluteExpiration);

            return Task.CompletedTask;
        }
    }
}
