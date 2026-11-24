// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.Data.Common;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class CacheStagingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(8192)]
    [InlineData(65536)]
    [InlineData(131077)]
    public async Task Staging_PublishesOneVersionWithBoundedInlineOrChunks(int length)
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var bytes = new byte[length];
        new Random(42).NextBytes(bytes);

        var published = await fixture.WriteAsync("key", bytes);

        published.IsSuccess.ShouldBeTrue(string.Join(", ", published.Errors.Select(e => e.Message)));
        published.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        var read = await fixture.Provider.LookupAsync(fixture.Identity("KEY"), new(true));
        read.IsSuccess.ShouldBeTrue(string.Join(", ", read.Errors.Select(e => e.Message)));
        read.Value.Metadata.Version.ShouldBe(published.Value.Version.Value);
        await using (var handle = read.Value.Handle)
        {
            using var copy = new MemoryStream();
            await handle.Stream.CopyToAsync(copy);
            copy.ToArray().ShouldBe(bytes);
        }

        await using var context = fixture.Context();
        var store = await context.Set<CacheStoreEntity>().SingleAsync();
        store.StagingBytes.ShouldBe(0);
        store.CurrentBytes.ShouldBe(length);
        store.CurrentEntries.ShouldBe(1);
        (await context.Set<CacheChunkEntity>().CountAsync()).ShouldBe(length <= 65536 ? 0 : 3);
    }

    [Fact]
    public async Task FailedReplacement_LeavesPreviousValueAndReleasesStagingCredits()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var original = await fixture.WriteAsync("key", "old"u8.ToArray());
        var identity = fixture.Identity("key");
        var fence = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        await using (var session = (await fixture.Provider.BeginWriteAsync(new(identity, fence, default, 65536))).Value)
        {
            await session.Stream.WriteAsync("bad"u8.ToArray());
            var invalid = fixture.Metadata(session, "key", "bad"u8.ToArray()) with { StoredHash = new string('0', 64) };

            (await session.PublishAsync(invalid)).IsFailure.ShouldBeTrue();
        }

        await fixture.Provider.ReleaseFenceAsync(identity, fence);
        var read = await fixture.Provider.LookupAsync(identity, new(true));
        read.Value.Metadata.Version.ShouldBe(original.Value.Version.Value);
        await using var handle = read.Value.Handle;
        using var copy = new MemoryStream();
        await handle.Stream.CopyToAsync(copy);
        copy.ToArray().ShouldBe("old"u8.ToArray());
        await using var context = fixture.Context();
        (await context.Set<CacheStoreEntity>().SingleAsync()).StagingBytes.ShouldBe(0);
    }

    [Fact]
    public async Task ExpectedVersion_ConflictsAfterAConcurrentPublicationWithoutChangingIt()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var original = await fixture.WriteAsync("key", "old"u8.ToArray());
        var identity = fixture.Identity("key");
        var fence = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        await using var session = (await fixture.Provider.BeginWriteAsync(new(identity, fence, new(original.Value.Version), 65536))).Value;
        await session.Stream.WriteAsync("late"u8.ToArray());
        var current = await fixture.WriteAsync("key", "current"u8.ToArray());

        var rejected = await session.PublishAsync(fixture.Metadata(session, "key", "late"u8.ToArray()));

        rejected.IsFailure.ShouldBeTrue();
        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
        (await fixture.Provider.LookupAsync(identity, new())).Value.Metadata.Version.ShouldBe(current.Value.Version.Value);
        await fixture.Provider.ReleaseFenceAsync(identity, fence);
    }

    [Fact]
    public async Task OldChunkReader_RemainsStableAfterReplacementAndRetiredBytesStayCharged()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        var old = new byte[100_000];
        new Random(42).NextBytes(old);
        var first = await fixture.WriteAsync("key", old);
        var acquired = await fixture.Provider.LookupAsync(fixture.Identity("key"), new(true));
        await using var reader = acquired.Value.Handle;

        (await fixture.WriteAsync("key", "new"u8.ToArray())).IsSuccess.ShouldBeTrue();

        using var copy = new MemoryStream();
        await reader.Stream.CopyToAsync(copy);
        copy.ToArray().ShouldBe(old);
        reader.Metadata.Version.ShouldBe(first.Value.Version.Value);
        await using var context = fixture.Context();
        (await context.Set<CacheStoreEntity>().SingleAsync()).RetiredBytes.ShouldBe(old.Length);
    }

    [Fact]
    public async Task LostCommitAcknowledgement_ReconcilesItsExactVersionWithoutReplayingTheWrite()
    {
        var fault = new LostAcknowledgement();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(interceptor: fault);
        var identity = fixture.Identity("key");
        var fence = (await fixture.Provider.CaptureFenceAsync(identity)).Value;
        await using var session = (await fixture.Provider.BeginWriteAsync(new(identity, fence, default, 65536))).Value;
        var bytes = "value"u8.ToArray();
        await session.Stream.WriteAsync(bytes);
        await using (var staged = (await session.OpenStagedReadAsync()).Value) { await staged.CopyToAsync(Stream.Null); }

        fault.Armed = true;
        var unknown = await session.PublishAsync(fixture.Metadata(session, "key", bytes));

        unknown.IsSuccess.ShouldBeTrue();
        unknown.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Indeterminate);
        var reconciled = await fixture.Provider.ReconcileAsync(session.OperationId);
        reconciled.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        reconciled.Value.Version.ShouldBe(session.Version);
        fault.Failures.ShouldBe(1);
        await using var context = fixture.Context();
        (await context.Set<CachePayloadEntity>().CountAsync()).ShouldBe(1);
        (await context.Set<CacheEntryEntity>().CountAsync()).ShouldBe(1);
        await fixture.Provider.ReleaseFenceAsync(identity, fence);
    }

    private sealed class LostAcknowledgement : DbTransactionInterceptor
    {
        internal bool Armed { get; set; }

        internal int Failures { get; private set; }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (this.Armed)
            {
                this.Armed = false;
                this.Failures++;
                throw new LostAcknowledgementException();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class LostAcknowledgementException : DbException;
}