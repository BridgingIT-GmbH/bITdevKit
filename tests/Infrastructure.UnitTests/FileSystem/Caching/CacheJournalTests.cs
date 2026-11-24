// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class CacheJournalTests
{
    [Fact]
    public async Task CommittedPrefix_IsChecksummedAndIgnoresUncommittedTail()
    {
        await using var fixture = new Fixture();
        var end = await CacheJournal.AppendAsync(fixture.File, 0, 1, "{\"commit\":1}"u8.ToArray(), 32 * CacheDefaults.MiB, true, default);
        await fixture.File.WriteAsync(new byte[] { 1, 2, 3 });
        var count = 0;

        await foreach (var frame in CacheJournal.ReadCommittedAsync(fixture.File, end, 1, fixture.Transient, fixture.Resident))
        {
            frame.Sequence.ShouldBe(1);
            frame.Payload.ToArray().ShouldBe("{\"commit\":1}"u8.ToArray());
            fixture.Transient.Used.ShouldBeGreaterThan(0);
            count++;
        }

        count.ShouldBe(1);
        fixture.Transient.Used.ShouldBe(0);
        fixture.Resident.Used.ShouldBe(0);
        var next = await CacheJournal.AppendAsync(fixture.File, end, 2, "{}"u8.ToArray(), 32 * CacheDefaults.MiB, true, default);
        fixture.File.Length.ShouldBe(next);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(26)]
    public async Task CorruptedCommittedFrame_FailsWithoutReturningUnverifiedMetadata(int position)
    {
        await using var fixture = new Fixture();
        var end = await CacheJournal.AppendAsync(fixture.File, 0, 1, "{}"u8.ToArray(), 32 * CacheDefaults.MiB, true, default);
        fixture.File.Position = position;
        fixture.File.WriteByte(0x7f);
        var yielded = false;

        var error = await Should.ThrowAsync<CacheTransferException>(async () =>
        {
            await foreach (var frame in CacheJournal.ReadCommittedAsync(fixture.File, end, 1, fixture.Transient, fixture.Resident)) { yielded = true; }
        });

        error.Code.ShouldBe(CacheErrorCode.Corruption);
        yielded.ShouldBeFalse();
        fixture.Transient.Used.ShouldBe(0);
    }

    [Fact]
    public async Task TailLimit_RejectsBeforeChangingTheFile()
    {
        await using var fixture = new Fixture();
        var error = await Should.ThrowAsync<CacheTransferException>(async () =>
            await CacheJournal.AppendAsync(fixture.File, 0, 1, new byte[1024], 1024, false, default));

        error.Code.ShouldBe(CacheErrorCode.Capacity);
        fixture.File.Length.ShouldBe(0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "cache-journal-" + Guid.NewGuid().ToString("N"));

        internal Fixture() => this.File = new(this.path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.Asynchronous);

        internal FileStream File { get; }

        internal ResourceBudget Transient { get; } = new("transient", 4 * CacheDefaults.MiB);

        internal ResourceBudget Resident { get; } = new("resident", 4 * CacheDefaults.MiB);

        public async ValueTask DisposeAsync()
        {
            await this.File.DisposeAsync();
            System.IO.File.Delete(this.path);
        }
    }
}