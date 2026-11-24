// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks bounded local retention ownership independently of provider publication.</summary>
public class CacheHotEntryOwnershipTests
{
    /// <summary>Admission evidence owns complete identity strings rather than charging only the short key.</summary>
    [Fact]
    public async Task AdmissionEvidence_LongIdentity_ChargesNamespaceCollectionContractAndVersion()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Store.Admission;
        var metadata = fixture.Metadata("key") with
        {
            Identity = new(new string('n', 256), new string('c', 128), new string('v', 128), "key")
        };
        var before = fixture.Resources.HotResident.Used;

        sut.Observe(metadata);

        (fixture.Resources.HotResident.Used - before).ShouldBeGreaterThanOrEqualTo(2L * (256 + 128 + 128 + 3 + 7));
    }

    /// <summary>The per-entry cap includes retained metadata even when the payload itself would fit.</summary>
    [Fact]
    public async Task Retention_RepresentationFitsButCombinedMetadataDoesNot_DeclinesWithoutCharging()
    {
        await using var fixture = new Fixture(new() { MaxEntryBytes = 2048 });
        var sut = fixture.Store;
        var metadata = fixture.Metadata("key");
        sut.Admission.Observe(metadata);
        sut.Admission.Observe(metadata);
        var before = fixture.Resources.HotResident.Used;

        sut.TryRetainBinary(metadata, new byte[1200], null).ShouldBeFalse();

        sut.Count.ShouldBe(0);
        fixture.Resources.HotResident.Used.ShouldBe(before);
    }

    /// <summary>A second representation cannot bypass the shared entry cap by excluding existing metadata ownership.</summary>
    [Fact]
    public async Task CombinedRetention_MetadataAndExistingRepresentation_DeclinesTheOversizedSecondRepresentation()
    {
        await using var fixture = new Fixture(new() { Representation = CacheHotEntryRepresentation.BinaryAndDecoded, MaxEntryBytes = 4096 });
        var sut = fixture.Store;
        var metadata = fixture.Metadata("key");
        sut.Admission.Observe(metadata);
        sut.Admission.Observe(metadata);
        sut.TryRetainBinary(metadata, new byte[2048], null).ShouldBeTrue();
        var before = fixture.Resources.HotResident.Used;

        sut.TryRetainDecoded(metadata, fixture.Contract, "decoded").ShouldBeFalse();

        sut.TryAcquire(metadata.Identity, true).ShouldBeNull();
        fixture.Resources.HotResident.Used.ShouldBe(before);
        using var binary = sut.TryAcquire(metadata.Identity, false);
        binary.ShouldNotBeNull();
    }

    /// <summary>Retained dimension strings consume both the hot and ordinary resident budgets.</summary>
    [Fact]
    public async Task Retention_LargeValidDimensionMetadata_ChargesItsRetainedStringsAndRecords()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Store;
        var values = Enumerable.Range(0, 8).Select(index => (object)(index.ToString("D2") + new string('x', 2998))).ToArray();
        var metadata = fixture.Metadata("key") with { Dimensions = [CacheDimension.Set("tags", CacheDimensionKind.String, values)] };
        sut.Admission.Observe(metadata);
        sut.Admission.Observe(metadata);
        var hotBefore = fixture.Resources.HotResident.Used;
        var residentBefore = fixture.Resources.ForCollection("items").Resident.Used;

        sut.TryRetainBinary(metadata, [1], null).ShouldBeTrue();

        (fixture.Resources.HotResident.Used - hotBefore).ShouldBeGreaterThanOrEqualTo(48000);
        (fixture.Resources.ForCollection("items").Resident.Used - residentBefore).ShouldBeGreaterThanOrEqualTo(48000);
    }

    /// <summary>Large metadata alone can make an otherwise tiny payload ineligible for local retention.</summary>
    [Fact]
    public async Task Retention_MetadataAloneExceedsEntryCap_DeclinesTheTinyPayload()
    {
        await using var fixture = new Fixture(new() { MaxEntryBytes = 32 * CacheDefaults.KiB });
        var sut = fixture.Store;
        var values = Enumerable.Range(0, 16).Select(index => (object)(index.ToString("D2") + new string('x', 2998))).ToArray();
        var metadata = fixture.Metadata("key") with { Dimensions = [CacheDimension.Set("tags", CacheDimensionKind.String, values)] };
        sut.Admission.Observe(metadata);
        sut.Admission.Observe(metadata);
        var before = fixture.Resources.HotResident.Used;

        sut.TryRetainBinary(metadata, [1], null).ShouldBeFalse();

        sut.Count.ShouldBe(0);
        fixture.Resources.HotResident.Used.ShouldBe(before);
    }

    /// <summary>Retiring an index does not release bytes while a consumer still owns a snapshot.</summary>
    [Fact]
    public async Task RetiredPinnedEntry_RemainsChargedUntilItsFinalSnapshotEnds()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Store;
        var metadata = fixture.Metadata("key");
        sut.Admission.Observe(metadata);
        sut.Admission.Observe(metadata);
        sut.TryRetainBinary(metadata, [1, 2, 3], null).ShouldBeTrue();
        var before = fixture.Resources.HotResident.Used;
        var pin = sut.TryAcquire(metadata.Identity, false);
        await using var handle = new CacheHotProviderReadHandle(pin, metadata);

        sut.Retire(metadata.Identity, metadata.Version);

        sut.Count.ShouldBe(0);
        fixture.Resources.HotResident.Used.ShouldBeGreaterThan(0);
        fixture.Resources.HotResident.Used.ShouldBeLessThan(before); // Only evidence was released.
        handle.Stream.ReadByte().ShouldBe(1);
        await handle.DisposeAsync();
        fixture.Resources.HotResident.Used.ShouldBe(0);
    }

    /// <summary>Two consumers use independent cursors over one owned immutable buffer.</summary>
    [Fact]
    public async Task IndependentHotReaders_DoNotMoveAnotherReadersCursor()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Store;
        var metadata = fixture.Metadata("key");
        sut.Admission.Observe(metadata);
        sut.Admission.Observe(metadata);
        sut.TryRetainBinary(metadata, [1, 2, 3], null).ShouldBeTrue();
        await using var first = new CacheHotProviderReadHandle(sut.TryAcquire(metadata.Identity, false), metadata);
        await using var second = new CacheHotProviderReadHandle(sut.TryAcquire(metadata.Identity, false), metadata);

        first.Stream.ReadByte().ShouldBe(1);
        first.Stream.ReadByte().ShouldBe(2);
        second.Stream.ReadByte().ShouldBe(1);
        first.Stream.Position.ShouldBe(2);
        second.Stream.Position.ShouldBe(1);
    }

    /// <summary>Admission evidence has both a hard count and a finite exact-version observation window.</summary>
    [Fact]
    public async Task Admission_EnforcesItsCountWindowAndVersionBoundary()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Store.Admission;
        var metadata = fixture.Metadata("key");
        sut.Observe(metadata);
        sut.IsEligible(metadata).ShouldBeFalse();
        sut.Observe(metadata);
        sut.IsEligible(metadata).ShouldBeTrue();
        sut.IsEligible(metadata with { Version = new("different") }).ShouldBeFalse();

        fixture.Time.Advance(TimeSpan.FromMinutes(2));
        sut.IsEligible(metadata).ShouldBeFalse();
        for (var index = 0; index < 20; index++) { sut.Observe(fixture.Metadata("key" + index)); }

        sut.Count.ShouldBe(4);
    }

    /// <summary>A late promotion or retirement cannot displace a different current local version.</summary>
    [Fact]
    public async Task ExactVersionRetirement_PreservesAConcurrentDifferentCandidate()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Store;
        var metadata = fixture.Metadata("key");
        sut.Admission.Observe(metadata);
        sut.Admission.Observe(metadata);
        sut.TryRetainBinary(metadata, [1], null).ShouldBeTrue();

        sut.Retire(metadata.Identity, new("earlier"));

        using var pin = sut.TryAcquire(metadata.Identity, false);
        pin.ShouldNotBeNull();
        pin.Metadata.Version.ShouldBe(metadata.Version);
    }

    /// <summary>Incremental local idle eviction releases only local retained ownership.</summary>
    [Fact]
    public async Task IdleEviction_ReleasesCandidateWithoutScanningOrDeletingProviderData()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Store;
        var metadata = fixture.Metadata("key");
        sut.Admission.Observe(metadata);
        sut.Admission.Observe(metadata);
        sut.TryRetainBinary(metadata, [1], null).ShouldBeTrue();

        fixture.Time.Advance(TimeSpan.FromMinutes(3));

        sut.TryAcquire(metadata.Identity, false).ShouldBeNull();
        sut.Count.ShouldBe(0);
    }

    /// <summary>Same-version decode work is shared independently of source-loading coordination.</summary>
    [Fact]
    public async Task PromotionCoordinator_CoalescesOnlyTheSameIdentityAndVersion()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Store.Promotions;
        var metadata = fixture.Metadata("key");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.TryRun(metadata.Identity, metadata.Version, () => { entered.SetResult(); return release.Task; }, out var owner, out var owns).ShouldBeTrue();
        await entered.Task;
        sut.TryRun(metadata.Identity, metadata.Version, () => throw new InvalidOperationException("The follower must not execute."),
            out Task<int> follower, out var followerOwns).ShouldBeTrue();

        owns.ShouldBeTrue();
        followerOwns.ShouldBeFalse();
        sut.Count.ShouldBe(1);
        release.SetResult(42);
        (await owner).ShouldBe(42);
        (await follower).ShouldBe(42);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider services;

        internal Fixture(CacheHotEntryOptions options = null)
        {
            var registrations = new ServiceCollection();
            registrations.AddApplicationCaching().WithValueContract<string>("text", c => c.WithImmutableSharing(_ => 1536L))
                .WithCollection<string>("items");
            this.services = registrations.BuildServiceProvider();
            this.Resources = this.services.GetRequiredService<CacheRuntimeResources>();
            this.Store = new(options ?? new() { Enabled = true, Admission = new() { MaxTrackingEntries = 4 } }, this.Resources.ForCollection("items"), this.Time);
        }

        internal Clock Time { get; } = new();

        internal CacheHotStore Store { get; }

        internal CacheRuntimeResources Resources { get; }

        /// <summary>Gets the immutable decoded contract used by combined-retention checks.</summary>
        public CacheValueContract<string> Contract => (CacheValueContract<string>)this.services.GetRequiredService<CacheCatalog>().Collections["items"].Contract;

        internal CacheEntryMetadata Metadata(string key) => new()
        {
            Identity = new("default", "items", "1", key), Version = new("version"), StoredSizeBytes = 3
        };

        public async ValueTask DisposeAsync()
        {
            this.Store.Dispose();
            await this.services.DisposeAsync();
        }
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => this.now;

        internal void Advance(TimeSpan duration) => this.now += duration;
    }
}