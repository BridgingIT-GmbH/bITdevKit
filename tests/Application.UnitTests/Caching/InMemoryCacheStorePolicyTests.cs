namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

public partial class InMemoryCacheStoreTests
{
    [Fact]
    public async Task CountPressure_EvictsLowerPriorityWithoutRemovingAnAcquiredReader()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time, new() { Limits = new() { MaxEntries = 2 } });
        var low = Require(await Store(sut, time, "low", [1]));
        Require(await Store(sut, time, "protected", [2]));
        var snapshot = Require(await sut.LookupAsync(Identity("protected"), new(OpenPayload: true)));
        await using var protectedReader = snapshot.Handle;

        var stored = Require(await Store(sut, time, "new", [3]));

        stored.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        Require(await sut.LookupAsync(Identity("low"), new())).State.ShouldBe(CacheReadValidationState.Missing);
        Require(await sut.LookupAsync(Identity("protected"), new())).State.ShouldBe(CacheReadValidationState.Valid);
        protectedReader.Stream.ReadByte().ShouldBe(2);
        Require(await sut.GetQuotaAsync()).CurrentEntries.ShouldBe(2);
        low.Version.ShouldNotBe(stored.Version);
    }

    [Fact]
    public async Task RejectPressurePolicy_PreservesExistingEntries()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time, new() { Limits = new() { MaxEntries = 1 }, Eviction = CacheEvictionMode.Reject });
        var original = Require(await Store(sut, time, "first", [1]));

        var rejected = await Store(sut, time, "second", [2]);

        rejected.IsFailure.ShouldBeTrue();
        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Capacity);
        Require(await sut.LookupAsync(Identity("first"), new())).Metadata.Version.ShouldBe(original.Version.Value);
    }

    [Fact]
    public async Task ConditionalConflict_UnderPressure_DoesNotEvictOtherKeys()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time, new() { Limits = new() { MaxEntries = 2 } });
        var old = Require(await Store(sut, time, "key", [1]));
        var other = Require(await Store(sut, time, "other", [2]));
        var replacement = Require(await Store(sut, time, "key", [3]));

        var conflict = await Store(sut, time, "key", [4], new(old.Version));

        conflict.IsFailure.ShouldBeTrue();
        Require(await sut.LookupAsync(Identity("key"), new())).Metadata.Version.ShouldBe(replacement.Version.Value);
        Require(await sut.LookupAsync(Identity("other"), new())).Metadata.Version.ShouldBe(other.Version.Value);
    }

    [Fact]
    public async Task SlidingExpiry_RenewsAtomicallyButStopsAtTheAbsoluteCap()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time, new() { Expiration = new() { SlidingEnabled = true } });
        var identity = Identity("sliding");
        var fence = Require(await sut.CaptureFenceAsync(identity));
        CacheEntryVersion version;
        await using (var staging = Require(await sut.BeginWriteAsync(new(identity, fence, default, 1024))))
        {
            await staging.Stream.WriteAsync(new byte[] { 1 });
            var metadata = Metadata(staging, time, identity, [1]) with
            {
                ExpiresAt = time.GetUtcNow().AddMinutes(5), AbsoluteExpiresAt = time.GetUtcNow().AddHours(1),
                SlidingIdleTimeout = TimeSpan.FromMinutes(5)
            };
            version = Require(await staging.PublishAsync(metadata)).Version.Value;
        }

        await sut.ReleaseFenceAsync(identity, fence);
        for (var i = 0; i < 14; i++)
        {
            time.Advance(TimeSpan.FromMinutes(4));
            var hit = Require(await sut.LookupAsync(identity, new()));
            hit.State.ShouldBe(CacheReadValidationState.Valid);
            hit.Metadata.Version.ShouldBe(version);
            hit.Metadata.ExpiresAt.ShouldBeLessThanOrEqualTo(hit.Metadata.AbsoluteExpiresAt);
        }

        time.Advance(TimeSpan.FromMinutes(4));
        Require(await sut.LookupAsync(identity, new())).State.ShouldBe(CacheReadValidationState.Missing);
        (await sut.TouchAsync(identity)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task BackgroundValidation_DoesNotRenewSlidingExpiryOrCreateForegroundActivity()
    {
        var time = new FakeTimeProvider();
        await using var sut = await Create(time, new() { Expiration = new() { SlidingEnabled = true } });
        var identity = Identity("sliding");
        var fence = Require(await sut.CaptureFenceAsync(identity));
        await using (var staging = Require(await sut.BeginWriteAsync(new(identity, fence, default, 1024))))
        {
            await staging.Stream.WriteAsync(new byte[] { 1 });
            Require(await staging.PublishAsync(Metadata(staging, time, identity, [1]) with
            {
                ExpiresAt = time.GetUtcNow().AddMinutes(5), AbsoluteExpiresAt = time.GetUtcNow().AddHours(1),
                SlidingIdleTimeout = TimeSpan.FromMinutes(5)
            }));
        }

        await sut.ReleaseFenceAsync(identity, fence);
        time.Advance(TimeSpan.FromMinutes(4));
        var background = Require(await sut.LookupAsync(identity, new(ObserveAccess: false)));
        background.Metadata.HasForegroundAccess.ShouldBeFalse();
        time.Advance(TimeSpan.FromMinutes(1));
        Require(await sut.LookupAsync(identity, new())).State.ShouldBe(CacheReadValidationState.Missing);
    }
}