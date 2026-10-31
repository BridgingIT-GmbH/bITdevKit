// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

[UnitTest("Application")]
public sealed class DocumentStoreClientGetManyTests
{
    [Fact]
    public async Task GetManyAsync_WithoutBatchProvider_ReturnsOneResultPerKeyInInputOrder()
    {
        var sut = new DocumentStoreClient<GetManyDocument>(new InMemoryDocumentStoreProvider());
        await sut.UpsertAsync(new("p", "a"), new() { Value = "A" });
        await sut.UpsertAsync(new("p", "c"), new() { Value = "C" });

        var result = await sut.GetManyAsync([new("p", "c"), new("p", "missing"), new("p", "a")]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(3);
        result.Value[0].Value.Value.Value.ShouldBe("C");
        result.Value[1].Errors.ShouldContain(x => x is DocumentStoreNotFoundError);
        result.Value[2].Value.Value.Value.ShouldBe("A");
    }

    [Fact]
    public async Task GetManyAsync_WithDuplicateKeys_ReadsOnceAndReturnsEveryPosition()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBatchGetProvider>();
        var batch = (IDocumentStoreBatchGetProvider)provider;
        batch.GetManyAsync(default, default, default, default).ReturnsForAnyArgs(Result<IReadOnlyList<StoredDocument>>.Success([]));
        var sut = new DocumentStoreClient<GetManyDocument>(provider);

        var result = await sut.GetManyAsync([new("p", "a"), new("p", "a")]);

        result.Value.Count.ShouldBe(2);
        await batch.Received(1).GetManyAsync(
            Arg.Any<DocumentTypeIdentity>(),
            Arg.Is<IReadOnlyList<DocumentKey>>(x => x.Count == 1),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetManyAsync_WithBatchProvider_UsesOneBatchCallAndNoSingleReads()
    {
        var inMemory = new InMemoryDocumentStoreProvider();
        var seed = new DocumentStoreClient<GetManyDocument>(inMemory);
        var stored = new List<StoredDocument>();
        foreach (var row in new[] { "a", "b" })
        {
            await seed.UpsertAsync(new("p", row), new() { Value = row });
            stored.Add((await inMemory.GetAsync(DocumentTypeIdentity.For<GetManyDocument>(), new("p", row), DateTimeOffset.UtcNow)).Value);
        }

        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBatchGetProvider>();
        var batch = (IDocumentStoreBatchGetProvider)provider;
        batch.GetManyAsync(default, default, default, default).ReturnsForAnyArgs(Result<IReadOnlyList<StoredDocument>>.Success(stored));
        var sut = new DocumentStoreClient<GetManyDocument>(provider);

        var result = await sut.GetManyAsync([new("p", "b"), new("p", "a"), new("p", "x")]);

        result.Value[0].Value.Value.Value.ShouldBe("b");
        result.Value[1].Value.Value.Value.ShouldBe("a");
        result.Value[2].Errors.ShouldContain(x => x is DocumentStoreNotFoundError);
        await provider.DidNotReceiveWithAnyArgs().GetAsync(default, default, default, default);
    }

    [Fact]
    public async Task GetManyAsync_WhenBatchProviderFails_ReturnsOuterFailure()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBatchGetProvider>();
        ((IDocumentStoreBatchGetProvider)provider).GetManyAsync(default, default, default, default)
            .ReturnsForAnyArgs(Result<IReadOnlyList<StoredDocument>>.Failure(new DocumentStoreProviderError("down")));
        var sut = new DocumentStoreClient<GetManyDocument>(provider);

        var result = await sut.GetManyAsync([new("p", "a")]);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(x => x is DocumentStoreProviderError);
    }

    [Fact]
    public async Task GetManyAsync_WithEmptyKeys_ReturnsEmptyWithoutTouchingProvider()
    {
        var provider = Substitute.For<IDocumentStoreProvider, IDocumentStoreBatchGetProvider>();
        var sut = new DocumentStoreClient<GetManyDocument>(provider);

        var result = await sut.GetManyAsync([]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        provider.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetManyAsync_WithNullInvalidOrTooManyKeys_ReturnsOuterFailure()
    {
        var sut = new DocumentStoreClient<GetManyDocument>(new InMemoryDocumentStoreProvider(), options: new() { MaxTake = 2 });

        (await sut.GetManyAsync(null)).IsFailure.ShouldBeTrue();
        (await sut.GetManyAsync([new("p", "a"), new("", "b")])).IsFailure.ShouldBeTrue();
        (await sut.GetManyAsync([new("p", "a"), new("p", "b"), new("p", "c")])).Errors.ShouldContain(x => x is DocumentStoreInvalidQueryError);
    }

    [Fact]
    public async Task GetManyAsync_WithExpiredDocument_ReturnsNotFound()
    {
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new DocumentStoreClient<GetManyDocument>(new InMemoryDocumentStoreProvider(), timeProvider: time);
        await sut.UpsertAsync(new("p", "a"), new() { Value = "A" }, new() { Expiration = ExpirationChange.After(TimeSpan.FromMinutes(1)) });
        time.Advance(TimeSpan.FromMinutes(2));

        var result = await sut.GetManyAsync([new("p", "a")]);

        result.Value[0].Errors.ShouldContain(x => x is DocumentStoreNotFoundError);
    }

    [Fact]
    public async Task CacheBehavior_GetManyAsync_FetchesOnlyMissesAndReturnsInputOrder()
    {
        var cache = new DictionaryCache();
        var inner = Substitute.For<IDocumentStoreClient<GetManyDocument>>();
        var sut = new CacheDocumentStoreClientBehavior<GetManyDocument>(null, inner, cache);
        var cachedKey = new DocumentKey("p", "cached");
        var missKey = new DocumentKey("p", "miss");
        inner.GetAsync(cachedKey, Arg.Any<CancellationToken>()).Returns(Result<DocumentEntry<GetManyDocument>>.Success(Entry(cachedKey, "cached")));
        await sut.GetAsync(cachedKey);
        inner.GetManyAsync(default, default).ReturnsForAnyArgs(Result<IReadOnlyList<Result<DocumentEntry<GetManyDocument>>>>.Success(
            [Result<DocumentEntry<GetManyDocument>>.Success(Entry(missKey, "miss"))]));

        var result = await sut.GetManyAsync([missKey, cachedKey, missKey]);

        result.Value.Select(x => x.Value.Value.Value).ShouldBe(["miss", "cached", "miss"]);
        await inner.Received(1).GetManyAsync(
            Arg.Is<IReadOnlyList<DocumentKey>>(x => x.Count == 1 && x[0] == missKey),
            Arg.Any<CancellationToken>());

        await sut.GetManyAsync([missKey]);

        await inner.Received(1).GetManyAsync(Arg.Any<IReadOnlyList<DocumentKey>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validate_WithBatchSizeOutOfRange_Fails()
    {
        new DocumentStoreOptions { GetManyBatchSize = 0 }.Validate().IsFailure.ShouldBeTrue();
        new DocumentStoreOptions { GetManyBatchSize = 1001 }.Validate().IsFailure.ShouldBeTrue();
        new DocumentStoreOptions { GetManyBatchSize = 1000 }.Validate().IsSuccess.ShouldBeTrue();
        new DocumentStoreOptions().GetManyBatchSize.ShouldBe(25);
    }

    [Fact]
    public void Validate_WithMaxParallelismOutOfRange_Fails()
    {
        new DocumentStoreOptions { GetManyMaxParallelism = 0 }.Validate().IsFailure.ShouldBeTrue();
        new DocumentStoreOptions { GetManyMaxParallelism = 33 }.Validate().IsFailure.ShouldBeTrue();
        new DocumentStoreOptions { GetManyMaxParallelism = 32 }.Validate().IsSuccess.ShouldBeTrue();
        new DocumentStoreOptions().GetManyMaxParallelism.ShouldBe(4);
    }

    private static DocumentEntry<GetManyDocument> Entry(DocumentKey key, string value) => new()
    {
        Key = key,
        Value = new() { Value = value },
        ContentHash = "sha256:x",
        ETag = "e",
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow
    };

    public sealed class GetManyDocument { public string Value { get; set; } }

    private sealed class DictionaryCache : ICacheProvider
    {
        private readonly Dictionary<string, object> entries = [];

        public T Get<T>(string key) => this.TryGet(key, out T value) ? value : default;
        public Task<T> GetAsync<T>(string key, CancellationToken token = default) => Task.FromResult(this.Get<T>(key));
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

        public Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default) => Task.FromResult(this.TryGet(key, out value));
        public IEnumerable<string> GetKeys() => this.entries.Keys;
        public Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default) => Task.FromResult<IEnumerable<string>>(this.entries.Keys);
        public void Remove(string key) => this.entries.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) { this.Remove(key); return Task.CompletedTask; }
        public void RemoveStartsWith(string key) { foreach (var k in this.entries.Keys.Where(x => x.StartsWith(key, StringComparison.Ordinal)).ToList()) this.entries.Remove(k); }
        public Task RemoveStartsWithAsync(string key, CancellationToken token = default) { this.RemoveStartsWith(key); return Task.CompletedTask; }
        public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null) => this.entries[key] = value;
        public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CancellationToken cancellationToken = default) { this.Set(key, value, slidingExpiration, absoluteExpiration); return Task.CompletedTask; }
    }
}
