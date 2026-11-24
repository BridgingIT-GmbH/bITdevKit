// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using System.Buffers.Binary;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class CacheIndexTests
{
    [Fact]
    public async Task MetadataCheckpoint_DropsUncapturedAbsentKeyStateAndPreservesOutstandingPublicationCaptures()
    {
        await using var fixture = new Fixture();
        var absent = new CacheEntryIdentity("site", "items", "1", new string('é', 2048));
        var held = new CacheEntryIdentity("site", "items", "1", "held");
        var rows = new[] {
            new CacheSegmentRow(CacheNativeKeyState.Key(absent), 1, JsonSerializer.SerializeToUtf8Bytes(new CacheNativeKeyState(1, CacheNativeKeyState.LogicalIdentity(absent), 4, 0))),
            new CacheSegmentRow(CacheNativeEntryState.Key(absent), 1, JsonSerializer.SerializeToUtf8Bytes(new CacheNativeEntryState(1, absent, 3, 0))),
            new CacheSegmentRow(CacheNativeKeyState.Key(held), 1, JsonSerializer.SerializeToUtf8Bytes(new CacheNativeKeyState(1, CacheNativeKeyState.LogicalIdentity(held), 5, 1))),
            new CacheSegmentRow(CacheNativeEntryState.Key(held), 1, JsonSerializer.SerializeToUtf8Bytes(new CacheNativeEntryState(1, held, 2, 1)))
        };
        var segment = await fixture.WriteAsync(Ordered());

        var merged = await CacheIndexCompactor.CompactAsync(fixture.Root, [segment], 2, (_, _) => ValueTask.FromResult(Result.Success()),
            true, fixture.Transient, fixture.Resident, default, dropTombstones: true, pruneSecondaryIndexes: true);

        await using var reader = await CacheIndexReader.OpenAsync(fixture.Root, merged, fixture.Transient, fixture.Resident, default);
        reader.Count.ShouldBe(2);
        (await reader.FindAsync(CacheNativeKeyState.Key(absent), default)).ShouldBeNull();
        (await reader.FindAsync(CacheNativeEntryState.Key(absent), default)).ShouldBeNull();
        (await reader.FindAsync(CacheNativeKeyState.Key(held), default)).ShouldNotBeNull();
        (await reader.FindAsync(CacheNativeEntryState.Key(held), default)).ShouldNotBeNull();

        async IAsyncEnumerable<CacheSegmentRow> Ordered()
        {
            foreach (var row in rows.OrderBy(value => value.Key)) { yield return row; }

            await Task.CompletedTask;
        }
    }

    [Fact]
    public async Task MetadataBlockCache_SharesValidatedImmutableBlocksAndRetainsPinnedChargesDuringDisposal()
    {
        await using var fixture = new Fixture();
        var segment = await fixture.WriteAsync(Rows(10000));
        using var cache = new CacheIndexBlockCache(4 * CacheDefaults.MiB);
        await using (var first = await CacheIndexReader.OpenAsync(fixture.Root, segment, fixture.Transient, fixture.Resident, default, cache))
        {
            (await first.FindAsync(Key(9000), default)).ShouldNotBeNull();
            first.BlockReads.ShouldBeGreaterThan(0);
            var retained = cache.Used;
            await using (var second = await CacheIndexReader.OpenAsync(fixture.Root, segment, fixture.Transient, fixture.Resident, default, cache))
            {
                (await second.FindAsync(Key(9000), default)).ShouldNotBeNull();
                second.BlockReads.ShouldBe(0);
                cache.Used.ShouldBe(retained);
            }

            cache.Dispose();
            cache.Used.ShouldBeGreaterThan(0);
            (await first.FindAsync(Key(9000), default)).ShouldNotBeNull();
        }

        cache.Used.ShouldBe(0);
        fixture.Resident.Used.ShouldBe(0);
        fixture.Transient.Used.ShouldBe(0);
    }

    [Fact]
    public async Task IncrementalMerge_ChoosesEachKeysNewestSequenceWithoutRetainingAResidentKeyCatalog()
    {
        await using var fixture = new Fixture();
        var first = await fixture.WriteAsync(Rows(10000));
        var second = await CacheSortedSegment.WriteAsync(fixture.Root, Guid.NewGuid().ToString("N"), 2, Changes(),
            (_, _) => ValueTask.FromResult(Result.Success()), true, fixture.Transient, fixture.Resident, default);

        var merged = await CacheIndexCompactor.CompactAsync(fixture.Root, [second, first], 3, (_, _) => ValueTask.FromResult(Result.Success()),
            true, fixture.Transient, fixture.Resident, default, dropTombstones: true);

        await using var reader = await CacheIndexReader.OpenAsync(fixture.Root, merged, fixture.Transient, fixture.Resident, default);
        reader.Count.ShouldBe(9998); // The existing key 5000 and newly deleted key 1 both disappear.
        (await reader.FindAsync(Key(1), default)).ShouldBeNull();
        var replaced = await reader.FindAsync(Key(9000), default);
        replaced.Sequence.ShouldBe(2);
        using var data = await reader.ReadAsync(replaced, default);
        BinaryPrimitives.ReadInt32LittleEndian(data.Value).ShouldBe(42);
        fixture.Transient.Used.ShouldBeLessThan(256 * CacheDefaults.KiB);

        static async IAsyncEnumerable<CacheSegmentRow> Changes()
        {
            yield return new(Key(1), 2, ReadOnlyMemory<byte>.Empty, true);
            var bytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 42);
            yield return new(Key(9000), 2, bytes);
            await Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExactLookup_BinarySearchesBoundedChecksummedBlocksAndReadsOnlyTheSelectedRecord()
    {
        await using var fixture = new Fixture();
        var segment = await fixture.WriteAsync(Rows(10000));
        await using var reader = await CacheIndexReader.OpenAsync(fixture.Root, segment, fixture.Transient, fixture.Resident, default);

        var position = await reader.FindAsync(Key(9000), default);

        position.ShouldNotBeNull();
        using var record = await reader.ReadAsync(position, default);
        BinaryPrimitives.ReadInt32LittleEndian(record.Value).ShouldBe(9000);
        reader.BlockReads.ShouldBeLessThanOrEqualTo(15);
        (await reader.FindAsync(Key(10000), default)).ShouldBeNull();
        var deletion = await reader.FindAsync(Key(5000), default);
        deletion.Deleted.ShouldBeTrue();
        deletion.Length.ShouldBe(0);
    }

    [Fact]
    public async Task RecordAndIndexCorruption_FailBeforeReturningUnverifiedRecordBytes()
    {
        await using var fixture = new Fixture();
        var segment = await fixture.WriteAsync(Rows(10));
        await using (var recordFile = new FileStream(Path.Combine(fixture.Root, segment.Id + ".records"), FileMode.Open, FileAccess.Write))
        {
            recordFile.WriteByte(0xff);
        }

        await using (var reader = await CacheIndexReader.OpenAsync(fixture.Root, segment, fixture.Transient, fixture.Resident, default))
        {
            var position = await reader.FindAsync(Key(0), default);
            (await Should.ThrowAsync<CacheTransferException>(async () => await reader.ReadAsync(position, default))).Code.ShouldBe(CacheErrorCode.Corruption);
        }

        await using (var indexFile = new FileStream(Path.Combine(fixture.Root, segment.Id + ".idx"), FileMode.Open, FileAccess.Write))
        {
            indexFile.Position = CacheSortedSegment.HeaderBytes + 5;
            indexFile.WriteByte(0xff);
        }

        await using var damaged = await CacheIndexReader.OpenAsync(fixture.Root, segment, fixture.Transient, fixture.Resident, default);
        (await Should.ThrowAsync<CacheTransferException>(async () => await damaged.FindAsync(Key(0), default))).Code.ShouldBe(CacheErrorCode.Corruption);
    }

    [Fact]
    public async Task FailedStaging_DoesNotOverwriteOrDeleteAnExistingImmutableSegment()
    {
        await using var fixture = new Fixture();
        var segment = await fixture.WriteAsync(Rows(10));
        var original = await File.ReadAllBytesAsync(Path.Combine(fixture.Root, segment.Id + ".idx"));

        await Should.ThrowAsync<IOException>(async () => await CacheSortedSegment.WriteAsync(fixture.Root, segment.Id, 1, Rows(10),
            (_, _) => ValueTask.FromResult(Result.Success()), true, fixture.Transient, fixture.Resident, default));

        (await File.ReadAllBytesAsync(Path.Combine(fixture.Root, segment.Id + ".idx"))).ShouldBe(original);
        await using var reader = await CacheIndexReader.OpenAsync(fixture.Root, segment, fixture.Transient, fixture.Resident, default);
        (await reader.FindAsync(Key(9), default)).ShouldNotBeNull();
    }

    [Fact]
    public async Task QuotaRejection_IsCheckedBeforeGrowingStagedFilesAndReleasesActualBuffers()
    {
        await using var fixture = new Fixture();

        var error = await Should.ThrowAsync<CacheTransferException>(async () => await CacheSortedSegment.WriteAsync(fixture.Root, Guid.NewGuid().ToString("N"), 1,
            Rows(10), (bytes, _) => ValueTask.FromResult(bytes <= 128 ? Result.Success() : Result.Failure(new CacheError(CacheErrorCode.Capacity, "Test quota rejected growth."))),
            true, fixture.Transient, fixture.Resident, default));

        error.Code.ShouldBe(CacheErrorCode.Capacity);
        Directory.EnumerateFiles(fixture.Root).ShouldBeEmpty();
        fixture.Transient.Used.ShouldBe(0);
        fixture.Resident.Used.ShouldBe(0);
    }

    private static CacheIndexKey Key(int value)
    {
        var bytes = new byte[5];
        bytes[0] = 1;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(1), value);
        return new(bytes);
    }

    private static async IAsyncEnumerable<CacheSegmentRow> Rows(int count)
    {
        for (var value = 0; value < count; value++)
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            yield return new(Key(value), 1, value == 5000 ? ReadOnlyMemory<byte>.Empty : bytes, value == 5000);
        }

        await Task.CompletedTask;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal Fixture() => Directory.CreateDirectory(this.Root);

        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "cache-index-" + Guid.NewGuid().ToString("N"));

        internal ResourceBudget Transient { get; } = new("transient", 16 * CacheDefaults.MiB);

        internal ResourceBudget Resident { get; } = new("resident", 16 * CacheDefaults.MiB);

        internal ValueTask<CacheSegmentReference> WriteAsync(IAsyncEnumerable<CacheSegmentRow> rows) => CacheSortedSegment.WriteAsync(this.Root, Guid.NewGuid().ToString("N"), 1,
            rows, (_, _) => ValueTask.FromResult(Result.Success()), true, this.Transient, this.Resident, default);

        public ValueTask DisposeAsync()
        {
            Directory.Delete(this.Root, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}