namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

public partial class InMemoryCacheStoreTests
{
    [Fact]
    public async Task ConditionalReplacement_ConflictsWithoutChangingCurrentOrAcquiredReader()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        var first = Require(await Store(sut, time, "Product:42", [1]));
        var acquired = Require(await sut.LookupAsync(Identity("PRODUCT:42"), new(OpenPayload: true)));
        await using var oldReader = acquired.Handle;
        var replacement = Require(await Store(sut, time, "product:42", [2]));
        var conflict = await Store(sut, time, "product:42", [3], new(first.Version));

        conflict.IsFailure.ShouldBeTrue();
        conflict.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        replacement.Version.ShouldNotBe(first.Version);
        oldReader.Stream.ReadByte().ShouldBe(1);
        oldReader.Metadata.Version.ShouldBe(first.Version.Value);
        var current = Require(await sut.LookupAsync(Identity("product:42"), new(OpenPayload: true)));
        await using var currentReader = current.Handle;
        currentReader.Stream.ReadByte().ShouldBe(2);
        current.Metadata.Version.ShouldBe(replacement.Version.Value);
    }

    [Fact]
    public async Task AbsentRemoval_FencesWorkThatAlreadyCapturedSourceAuthority()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        var identity = Identity("late");
        var fence = Require(await sut.CaptureFenceAsync(identity));
        await using var staging = Require(await sut.BeginWriteAsync(new(identity, fence, default, 1024)));
        await staging.Stream.WriteAsync(new byte[] { 1, 2 });

        var removal = Require(await sut.RemoveAsync(identity));
        var publication = await staging.PublishAsync(Metadata(staging, time, identity, [1, 2]));
        await sut.ReleaseFenceAsync(identity, fence);

        removal.Completed.ShouldBeTrue();
        removal.AffectedCount.ShouldBe(0);
        publication.IsFailure.ShouldBeTrue();
        publication.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        Require(await sut.LookupAsync(identity, new())).State.ShouldBe(CacheReadValidationState.Missing);
    }

    [Fact]
    public async Task ReleasedFence_CannotAuthorizeALaterWrite()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        var identity = Identity("late");
        var fence = Require(await sut.CaptureFenceAsync(identity));
        await sut.ReleaseFenceAsync(identity, fence);
        await sut.ReleaseFenceAsync(identity, fence);

        var result = await sut.BeginWriteAsync(new(identity, fence, default, 1024));

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
    }

    [Fact]
    public async Task OversizedReplacement_PreservesOldVersionAndReleasesStaging()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time, new() { Limits = new() { MaxEncodedBytes = 256, MaxLogicalBytes = 256 } });
        var original = Require(await Store(sut, time, "key", [1]));
        var identity = Identity("key");
        var fence = Require(await sut.CaptureFenceAsync(identity));
        await using (var staging = Require(await sut.BeginWriteAsync(new(identity, fence, default, 256))))
        {
            await Should.ThrowAsync<IOException>(async () => await staging.Stream.WriteAsync(new byte[257]));
            var failed = await staging.PublishAsync(Metadata(staging, time, identity, []));
            failed.IsFailure.ShouldBeTrue();
        }

        await sut.ReleaseFenceAsync(identity, fence);
        var current = Require(await sut.LookupAsync(identity, new()));
        current.Metadata.Version.ShouldBe(original.Version.Value);
        Require(await sut.GetQuotaAsync()).StagingBytes.ShouldBe(0);
    }

    [Fact]
    public async Task StagedReaders_AreIndependentAndPublicationLeavesOldSnapshotValid()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        var identity = Identity("staged");
        var fence = Require(await sut.CaptureFenceAsync(identity));
        await using var staging = Require(await sut.BeginWriteAsync(new(identity, fence, default, 1024)));
        await staging.Stream.WriteAsync(new byte[] { 1, 2, 3 });
        await using var first = Require(await staging.OpenStagedReadAsync());
        await using var second = Require(await staging.OpenStagedReadAsync());

        first.ReadByte().ShouldBe(1);
        first.ReadByte().ShouldBe(2);
        second.ReadByte().ShouldBe(1);
        Require(await staging.PublishAsync(Metadata(staging, time, identity, [1, 2, 3]))).StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        await sut.ReleaseFenceAsync(identity, fence);
        first.ReadByte().ShouldBe(3);
    }

    [Fact]
    public async Task Expiration_NewCallersMissWhileAnAcquiredGenerationRemainsReadable()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        Require(await Store(sut, time, "expires", [1, 2]));
        var snapshot = Require(await sut.LookupAsync(Identity("expires"), new(OpenPayload: true)));
        await using var reader = snapshot.Handle;

        time.Advance(TimeSpan.FromMinutes(16));
        var current = Require(await sut.LookupAsync(Identity("expires"), new()));

        current.State.ShouldBe(CacheReadValidationState.Missing);
        current.Metadata.ShouldBeNull();
        reader.Stream.ReadByte().ShouldBe(1);
        reader.Stream.ReadByte().ShouldBe(2);
    }

    [Fact]
    public async Task ConditionalRemoval_StaleToken_DoesNotFenceCurrentWork()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time);
        var old = Require(await Store(sut, time, "key", [1]));
        Require(await Store(sut, time, "key", [2]));
        var identity = Identity("key");
        var fence = Require(await sut.CaptureFenceAsync(identity));

        var removal = await sut.RemoveAsync(identity, new(old.Version));
        await using var staging = Require(await sut.BeginWriteAsync(new(identity, fence, default, 1024)));
        await staging.Stream.WriteAsync(new byte[] { 3 });
        var published = Require(await staging.PublishAsync(Metadata(staging, time, identity, [3])));
        await sut.ReleaseFenceAsync(identity, fence);

        removal.IsFailure.ShouldBeTrue();
        published.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
    }

    private static async Task<InMemoryCacheStoreProvider> Create(FakeTimeProvider time, CacheCollectionOptions policies = null)
    {
        var provider = new InMemoryCacheStoreProvider(timeProvider: time);
        var ready = await provider.InitializeAsync([new("app", "cards", "v1", "card", policies?.Dimensions ?? [], policies ?? new())]);
        ready.IsSuccess.ShouldBeTrue();
        return provider;
    }

    private static CacheEntryIdentity Identity(string key) => new("app", "cards", "v1", key);

    private static async Task<Result<CacheWriteOutcome>> Store(InMemoryCacheStoreProvider provider, FakeTimeProvider time,
        string key, byte[] bytes, CacheWriteCondition condition = default, params CacheDimension[] dimensions)
    {
        var identity = Identity(key);
        var fence = Require(await provider.CaptureFenceAsync(identity));
        try
        {
            await using var staging = Require(await provider.BeginWriteAsync(new(identity, fence, condition, 1024)));
            await staging.Stream.WriteAsync(bytes);
            return await staging.PublishAsync(Metadata(staging, time, identity, bytes) with { Dimensions = dimensions });
        }
        finally
        {
            await provider.ReleaseFenceAsync(identity, fence);
        }
    }

    private static CacheEntryMetadata Metadata(ICacheProviderWriteSession staging, FakeTimeProvider time,
        CacheEntryIdentity identity, byte[] bytes) => new()
    {
        Identity = identity, Version = staging.Version, ContractId = "card", DisplayKey = identity.Key,
        PublishedAt = time.GetUtcNow(), LastAccessAt = time.GetUtcNow(),
        ExpiresAt = time.GetUtcNow().AddMinutes(15), AbsoluteExpiresAt = time.GetUtcNow().AddMinutes(15),
        LogicalSizeBytes = bytes.Length, EncodedSizeBytes = bytes.Length, StoredSizeBytes = bytes.Length,
        StoredHash = Convert.ToHexStringLower(SHA256.HashData(bytes)), PayloadHash = Convert.ToHexStringLower(SHA256.HashData(bytes)),
        CodecId = "raw"
    };

    private static T Require<T>(Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}