// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using CachePayloadReader = BridgingIT.DevKit.Infrastructure.FileSystem.Caching.CachePayloadReader;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class CachePayloadTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65536)]
    [InlineData(65537)]
    [InlineData(2097159)]
    public async Task FramedPayload_RoundTripsWithBoundedOwnedBuffersAndPhysicalCredits(int length)
    {
        await using var fixture = new Fixture();
        var bytes = Bytes(length);
        var payload = await fixture.WriteAsync(bytes);
        await using (var reader = await fixture.OpenAsync(payload))
        {
            var received = new MemoryStream();
            await reader.CopyToAsync(received);
            received.ToArray().ShouldBe(bytes);
            reader.Position.ShouldBe(length);
            fixture.Transient.Used.ShouldBeLessThan(256 * CacheDefaults.KiB);
            (await fixture.IsPinnedAsync("payload", payload.Version)).ShouldBeTrue();
            new FileInfo(CachePayloadReference.PathFor(fixture.Values, payload.Version)).Length.ShouldBe(payload.PhysicalBytes);
            fixture.Reserved.ShouldBeGreaterThanOrEqualTo(payload.PhysicalBytes + 4096);
        }

        fixture.PinBytes.ShouldBe(0);
        (await fixture.IsPinnedAsync("payload", payload.Version)).ShouldBeFalse();
    }

    [Fact]
    public async Task IndependentReaders_SeekAcrossFramesWithoutChangingEachOthersPosition()
    {
        await using var fixture = new Fixture();
        var bytes = Bytes(3 * CachePayloadReference.BlockBytes + 123);
        var payload = await fixture.WriteAsync(bytes);
        await using var first = await fixture.OpenAsync(payload);
        await using var second = await fixture.OpenAsync(payload);
        var a = new byte[71];
        var b = new byte[123];

        first.Position = 65520;
        await first.ReadExactlyAsync(a);
        second.Seek(-123, SeekOrigin.End).ShouldBe(bytes.Length - 123);
        await second.ReadExactlyAsync(b);

        a.ShouldBe(bytes[65520..65591]);
        b.ShouldBe(bytes[^123..]);
        first.Position.ShouldBe(65591);
        second.Position.ShouldBe(bytes.Length);
        Should.Throw<IOException>(() => first.Position = -1);
        Should.Throw<IOException>(() => first.Position = bytes.Length + 1L);
    }

    [Fact]
    public async Task CorruptChunk_FailsBeforeCopyingAnyOfThatChunkIntoCallerMemory()
    {
        await using var fixture = new Fixture();
        var payload = await fixture.WriteAsync(Bytes(65537));
        await using (var file = new FileStream(CachePayloadReference.PathFor(fixture.Values, payload.Version), FileMode.Open, FileAccess.Write))
        {
            file.Position = CachePayloadReference.FrameBytes + 123;
            file.WriteByte(250);
        }

        await using var sut = await fixture.OpenAsync(payload);
        var output = Enumerable.Repeat((byte)17, 1000).ToArray();
        var error = await Should.ThrowAsync<CacheTransferException>(async () => await sut.ReadExactlyAsync(output));

        error.Code.ShouldBe(CacheErrorCode.Corruption);
        output.All(b => b == 17).ShouldBeTrue();
    }

    [Fact]
    public async Task WrongCompleteHash_FailsBeforeDeliveringTheLastBytes()
    {
        await using var fixture = new Fixture();
        var payload = await fixture.WriteAsync(Bytes(100));
        await using var sut = await fixture.OpenAsync(payload with { Hash = new string('0', 64) });
        var output = Enumerable.Repeat((byte)17, 100).ToArray();

        (await Should.ThrowAsync<CacheTransferException>(async () => await sut.ReadExactlyAsync(output))).Code.ShouldBe(CacheErrorCode.Corruption);
        output.All(b => b == 17).ShouldBeTrue();
    }

    [Fact]
    public async Task RejectedPhysicalGrowth_PreservesTheAlreadyReservedBoundaryAndPoisonsTheWriter()
    {
        await using var fixture = new Fixture();
        fixture.RejectAbove = 4096;
        var version = Guid.NewGuid().ToString("N");
        await using var sut = await fixture.WriterAsync(version);

        (await Should.ThrowAsync<CacheTransferException>(async () => await sut.WriteAsync(Bytes(65536)))).Code.ShouldBe(CacheErrorCode.Capacity);
        new FileInfo(CachePayloadReference.PathFor(fixture.Values, version, true)).Length.ShouldBe(0);
        fixture.Reserved.ShouldBe(4096);
        (await Should.ThrowAsync<CacheTransferException>(async () => await sut.SealAsync(default))).Code.ShouldBe(CacheErrorCode.Contract);
        File.Exists(CachePayloadReference.PathFor(fixture.Values, version)).ShouldBeFalse();
    }

    [Fact]
    public async Task SourceBuffer_CanBeReusedAsSoonAsWriteCompletes()
    {
        await using var fixture = new Fixture();
        var source = Bytes(100);
        var expected = source.ToArray();
        await using var writer = await fixture.WriterAsync(Guid.NewGuid().ToString("N"));
        await writer.WriteAsync(source);
        Array.Fill(source, (byte)255);
        var payload = await writer.SealAsync(default);
        await using var reader = await fixture.OpenAsync(payload);
        var output = new byte[100];
        await reader.ReadExactlyAsync(output);

        output.ShouldBe(expected);
    }

    [Fact]
    public async Task MissingOrTruncatedCommittedPayload_IsCorruptionAndReleasesTheFailedOpenPin()
    {
        await using var fixture = new Fixture();
        var payload = await fixture.WriteAsync(Bytes(100));
        await using (var file = new FileStream(CachePayloadReference.PathFor(fixture.Values, payload.Version), FileMode.Open, FileAccess.Write)) { file.SetLength(10); }

        (await Should.ThrowAsync<CacheTransferException>(async () => await fixture.OpenAsync(payload))).Code.ShouldBe(CacheErrorCode.Corruption);
        fixture.PinBytes.ShouldBe(0);
        File.Delete(CachePayloadReference.PathFor(fixture.Values, payload.Version));
        (await Should.ThrowAsync<CacheTransferException>(async () => await fixture.OpenAsync(payload))).Code.ShouldBe(CacheErrorCode.Corruption);
        fixture.PinBytes.ShouldBe(0);
    }

    [Fact]
    public async Task LivePins_ProtectOnlyTheirOwnKindAndVersionAndHaveBoundedAdmission()
    {
        await using var fixture = new Fixture(maximumPins: 1);
        var version = Guid.NewGuid().ToString("N");
        var pin = await fixture.PinAsync(version);
        try
        {
            (await fixture.IsPinnedAsync("payload", version)).ShouldBeTrue();
            (await fixture.IsPinnedAsync("index", version)).ShouldBeFalse();
            (await fixture.IsPinnedAsync("payload", Guid.NewGuid().ToString("N"))).ShouldBeFalse();
            (await Should.ThrowAsync<CacheTransferException>(async () => await fixture.PinAsync(Guid.NewGuid().ToString("N")))).Code.ShouldBe(CacheErrorCode.Capacity);
            fixture.PinBytes.ShouldBe(CacheReaderRegistry.PinCreditBytes);
            var id = Path.GetFileNameWithoutExtension(Directory.EnumerateFiles(Path.Combine(fixture.Shard, "pins"), "*.pin").Single());
            (await fixture.ReclaimAsync(id)).ShouldBeFalse();
        }
        finally { await pin.DisposeAsync(); await pin.DisposeAsync(); }

        fixture.PinBytes.ShouldBe(0);
        (await fixture.IsPinnedAsync("payload", version)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbandonedPin_LocalHandleTerminationIsProofButSharedOrphansNeverExpire(bool shared)
    {
        await using var fixture = new Fixture(shared: shared);
        var version = Guid.NewGuid().ToString("N");
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(fixture.Shard, "pins");
        Directory.CreateDirectory(directory);
        using (var owner = FileSystemAuthority.TryAcquire(fixture.Profile.AuthorityOptions, directory, "stage-reader-" + id))
        {
            await CacheCheckedMetadata.WriteAsync(directory, id + ".pin",
                new { Format = 1, fixture.StoreId, CollectionIdentity = "test-collection", Shard = 0, RecoveryGeneration = 0L, Id = id, Kind = "payload", References = new[] { version } },
                owner, true, fixture.Transient, fixture.Resident, default);
            owner.RetainIfShared();
            fixture.PinBytes = CacheReaderRegistry.PinCreditBytes;
        }

        (await fixture.ReclaimAsync(id)).ShouldBe(!shared);
        (await fixture.IsPinnedAsync("payload", version)).ShouldBe(shared);
        fixture.PinBytes.ShouldBe(shared ? CacheReaderRegistry.PinCreditBytes : 0);
        File.Exists(Path.Combine(directory, id + ".pin")).ShouldBe(shared);
    }

    private static byte[] Bytes(int length) => Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "cache-payload-" + Guid.NewGuid().ToString("N"));
        private readonly CacheReaderRegistry registry;

        internal Fixture(bool shared = false, int maximumPins = 4096)
        {
            this.Profile = new(new() { RootPath = this.path, AccessKind = shared ? "Smb" : "Local" });
            Directory.CreateDirectory(this.Shard);
            this.registry = new(this.Shard, this.StoreId, "test-collection", 0, 0, this.Profile, this.Transient, this.Resident,
                (_, bytes, _) => { this.PinBytes += bytes; return ValueTask.FromResult(Result.Success()); },
                (_, bytes, _) => { this.PinBytes -= bytes; return ValueTask.CompletedTask; }, maximumPins);
        }

        internal ResourceBudget Transient { get; } = new("transient", 16 * CacheDefaults.MiB);

        internal ResourceBudget Resident { get; } = new("resident", 16 * CacheDefaults.MiB);

        internal string StoreId { get; } = Guid.NewGuid().ToString("N");

        internal string Shard => Path.Combine(this.path, "shard");

        internal string Values => Path.Combine(this.Shard, "values");

        internal FileSystemCacheOptions Profile { get; }

        internal long Reserved { get; private set; }

        internal long PinBytes { get; set; }

        internal long RejectAbove { get; set; } = long.MaxValue;

        internal ValueTask<CachePayloadWriter> WriterAsync(string version) => CachePayloadWriter.CreateAsync(this.Values, this.StoreId, version,
            4 * CacheDefaults.MiB, this.Profile, (bytes, _) =>
            {
                if (bytes > this.RejectAbove) { return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Capacity, "Fixture admission rejected."))); }

                this.Reserved = Math.Max(this.Reserved, bytes);
                return ValueTask.FromResult(Result.Success());
            }, this.Transient, this.Resident, default);

        internal async Task<CachePayloadReference> WriteAsync(byte[] bytes)
        {
            await using var writer = await this.WriterAsync(Guid.NewGuid().ToString("N"));
            await writer.WriteAsync(bytes);
            return await writer.SealAsync(default);
        }

        internal async ValueTask<IAsyncDisposable> PinAsync(string version)
        {
            using var authority = await FileSystemAuthority.AcquireAsync(this.Profile.AuthorityOptions, this.Shard, "authority", false, default);
            return await this.registry.RegisterAsync(authority, "payload", [version], default);
        }

        internal async ValueTask<bool> IsPinnedAsync(string kind, string version)
        {
            using var authority = await FileSystemAuthority.AcquireAsync(this.Profile.AuthorityOptions, this.Shard, "authority", false, default);
            return await this.registry.IsPinnedAsync(authority, kind, version, default);
        }

        internal async ValueTask<bool> ReclaimAsync(string id)
        {
            using var authority = await FileSystemAuthority.AcquireAsync(this.Profile.AuthorityOptions, this.Shard, "authority", false, default);
            return await this.registry.ReclaimLocalOrphanAsync(authority, id, default);
        }

        internal async ValueTask<CachePayloadReader> OpenAsync(CachePayloadReference payload) => await CachePayloadReader.OpenAsync(this.Values,
            this.StoreId, payload, await this.PinAsync(payload.Version), this.Transient, this.Resident, default);

        public ValueTask DisposeAsync()
        {
            Directory.Delete(this.path, recursive: true);
            this.Transient.Used.ShouldBe(0);
            this.Resident.Used.ShouldBe(0);
            return ValueTask.CompletedTask;
        }
    }
}