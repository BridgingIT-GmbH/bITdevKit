// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;

public class CacheMaintenanceTests
{
    [Fact]
    public async Task RetiredPayload_RemainsChargedAndReadableUntilItsHandleActuallyCloses()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var bytes = Enumerable.Range(0, 180000).Select(i => (byte)i).ToArray();
        (await fixture.WriteAsync("key", bytes)).IsSuccess.ShouldBeTrue();
        var acquired = await fixture.Provider.LookupAsync(fixture.Identity("key"), new(OpenPayload: true));
        acquired.IsSuccess.ShouldBeTrue();
        var handle = acquired.Value.Handle;
        await fixture.Provider.RemoveAsync(fixture.Identity("key"));

        for (var i = 0; i < 4; i++) { (await MaintainAsync(fixture, 1)).IsSuccess.ShouldBeTrue(); }

        (await fixture.Provider.GetQuotaAsync()).Value.RetiredBytes.ShouldBe(bytes.Length);
        using var read = new MemoryStream();
        await handle.Stream.CopyToAsync(read);
        read.ToArray().ShouldBe(bytes);
        await handle.DisposeAsync();
        for (var i = 0; i < 12; i++) { (await MaintainAsync(fixture, 1)).IsSuccess.ShouldBeTrue(); }

        var quota = (await fixture.Provider.GetQuotaAsync()).Value;
        quota.CurrentEntries.ShouldBe(0);
        quota.RetiredBytes.ShouldBe(0);
        quota.StagingBytes.ShouldBe(0);
        quota.MetadataBytes.ShouldBe(16384 + 32768 + 16384);
    }

    [Fact]
    public async Task ExpirationGraceAndExpiredStaging_AreRecoveredInBoundedPassesWithoutReadingPayloads()
    {
        var clock = new Clock();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(timeProvider: clock);
        (await fixture.WriteAsync("expired", new byte[90000])).IsSuccess.ShouldBeTrue();
        var identity = fixture.Identity("abandoned");
        var fence = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        await using var session = (await fixture.Provider.BeginWriteAsync(new(identity, fence, default, 200000))).Value;
        await session.Stream.WriteAsync(new byte[90000]);
        (await fixture.Provider.GetQuotaAsync()).Value.StagingBytes.ShouldBeGreaterThan(0);
        clock.Advance(TimeSpan.FromMinutes(2));

        (await MaintainAsync(fixture)).IsSuccess.ShouldBeTrue();
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(1);
        clock.Advance(TimeSpan.FromMinutes(10));
        for (var i = 0; i < 12; i++)
        {
            var result = await fixture.Provider.MaintainAsync(new(2, clock.GetUtcNow(), clock.GetUtcNow().AddSeconds(5)));
            result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Message)));
        }

        var quota = (await fixture.Provider.GetQuotaAsync()).Value;
        quota.CurrentEntries.ShouldBe(0);
        quota.CurrentBytes.ShouldBe(0);
        quota.RetiredBytes.ShouldBe(0);
        quota.StagingBytes.ShouldBe(0);
        (await session.PublishAsync(fixture.Metadata(session, "abandoned", new byte[90000]))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task CleanupDeadlineAndInvalidBounds_DoNotChangeTheStore()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        await fixture.WriteAsync("key", [1]);
        await fixture.Provider.RemoveAsync(fixture.Identity("key"));
        var before = (await fixture.Provider.GetQuotaAsync()).Value;

        (await fixture.Provider.MaintainAsync(new(0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(1)))).IsFailure.ShouldBeTrue();
        var expired = await fixture.Provider.MaintainAsync(new(100, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(-1)));

        expired.Value.HasMore.ShouldBeTrue();
        (await fixture.Provider.GetQuotaAsync()).Value.ShouldBe(before);
    }

    private static ValueTask<Result<CacheMaintenanceOutcome>> MaintainAsync(CacheRelationalTestStore fixture, int batchSize = 100) =>
        fixture.Provider.MaintainAsync(new(batchSize, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(5)));

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => this.now;

        internal void Advance(TimeSpan value) => this.now += value;
    }
}