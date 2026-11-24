// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

public class CacheExactRemovalTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateOnly_TreatsConfirmedNegativeAsAbsentAndPositiveEmptyAsPresent(bool negative)
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync(collection: new() { NegativeCaching = new() { Enabled = true } });
        var identity = fixture.Identity("key");
        var captured = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        try
        {
            await using var session = (await fixture.Provider.BeginWriteAsync(new(identity, captured, default, 1))).Value;
            var now = DateTimeOffset.UtcNow;
            var metadata = fixture.Metadata(session, "key", []) with
            {
                PublishedAt = now, ExpiresAt = now.AddSeconds(10), AbsoluteExpiresAt = now.AddSeconds(10), IsNotFound = negative,
                PayloadHash = negative ? null : fixture.Metadata(session, "key", []).PayloadHash,
                StoredHash = negative ? null : fixture.Metadata(session, "key", []).StoredHash
            };
            (await session.PublishAsync(metadata)).IsSuccess.ShouldBeTrue();
        }
        finally { (await fixture.Provider.ReleaseFenceAsync(identity, captured)).IsSuccess.ShouldBeTrue(); }

        var result = await fixture.WriteAsync("key", [2], new(CreateOnly: true));

        result.IsSuccess.ShouldBe(negative);
        if (!negative) { result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict); }

        var current = await fixture.Provider.LookupAsync(identity, new());
        current.Value.Metadata.IsNotFound.ShouldBeFalse();
        current.Value.Metadata.StoredSizeBytes.ShouldBe(negative ? 1 : 0);
    }

    [Fact]
    public async Task RemovingAnAbsentKey_FencesAlreadyCapturedPublication()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var identity = fixture.Identity("key");
        var fence = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        await using var session = (await fixture.Provider.BeginWriteAsync(new(identity, fence, new(CreateOnly: true), 65536))).Value;
        var bytes = "late"u8.ToArray();
        await session.Stream.WriteAsync(bytes);

        var removed = await fixture.Provider.RemoveAsync(identity);
        var late = await session.PublishAsync(fixture.Metadata(session, "key", bytes));

        removed.Value.Completed.ShouldBeTrue();
        removed.Value.AffectedCount.ShouldBe(0);
        late.IsFailure.ShouldBeTrue();
        late.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        (await fixture.Provider.LookupAsync(identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        await fixture.Provider.ReleaseFenceAsync(identity, fence);
    }

    [Fact]
    public async Task FailedExpectedRemoval_DoesNotFenceAnotherConditionalPublication()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var old = await fixture.WriteAsync("key", "old"u8.ToArray());
        var identity = fixture.Identity("key");
        var fence = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        await using var session = (await fixture.Provider.BeginWriteAsync(new(identity, fence, new(old.Value.Version), 65536))).Value;
        var bytes = "updated"u8.ToArray();
        await session.Stream.WriteAsync(bytes);

        var rejected = await fixture.Provider.RemoveAsync(identity, new(CacheEntryVersion.New()));
        var published = await session.PublishAsync(fixture.Metadata(session, "key", bytes));

        rejected.IsFailure.ShouldBeTrue();
        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        published.IsSuccess.ShouldBeTrue();
        published.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        await fixture.Provider.ReleaseFenceAsync(identity, fence);
    }

    [Fact]
    public async Task Removal_RetiresPinnedBytesAndDoesNotRevokeAnAcquiredReader()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var bytes = new byte[100_000];
        new Random(42).NextBytes(bytes);
        var stored = await fixture.WriteAsync("key", bytes);
        var acquired = await fixture.Provider.LookupAsync(fixture.Identity("key"), new(true));
        await using var reader = acquired.Value.Handle;

        var removed = await fixture.Provider.RemoveAsync(fixture.Identity("key"), new(stored.Value.Version));

        removed.Value.AffectedCount.ShouldBe(1);
        (await fixture.Provider.LookupAsync(fixture.Identity("key"), new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        var quota = (await fixture.Provider.GetQuotaAsync()).Value;
        quota.CurrentBytes.ShouldBe(0);
        quota.RetiredBytes.ShouldBe(bytes.Length);
        quota.CurrentEntries.ShouldBe(0);
        using var copied = new MemoryStream();
        await reader.Stream.CopyToAsync(copied);
        copied.ToArray().ShouldBe(bytes);
    }

    [Fact]
    public async Task ExactRemoval_CoversRetainedContractVersionsInTheSameStore()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        (await fixture.WriteAsync("key", "v1"u8.ToArray())).IsSuccess.ShouldBeTrue();
        await using var second = new EntityFrameworkCacheStoreProvider("second", new() { ChunkSize = 65536 }, fixture.Context);
        (await second.InitializeAsync([fixture.Definition, fixture.Definition with { ContractVersion = "2" }])).IsSuccess.ShouldBeTrue();
        var identity = new CacheEntryIdentity("test", "items", "2", "key");
        var fence = (await second.CaptureFenceAsync(identity)).Value;
        await using (var session = (await second.BeginWriteAsync(new(identity, fence, default, 65536))).Value)
        {
            var bytes = "v2"u8.ToArray();
            await session.Stream.WriteAsync(bytes);
            var metadata = fixture.Metadata(session, "key", bytes) with { Identity = identity };
            (await session.PublishAsync(metadata)).IsSuccess.ShouldBeTrue();
        }

        await second.ReleaseFenceAsync(identity, fence);

        var removed = await fixture.Provider.RemoveAsync(fixture.Identity("key"));

        removed.IsSuccess.ShouldBeTrue();
        removed.Value.AffectedCount.ShouldBe(2);
        (await second.LookupAsync(identity, new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
        (await fixture.Provider.LookupAsync(fixture.Identity("key"), new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
    }
}