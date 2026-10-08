// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public sealed class EntityFrameworkProfilingCoordinationTests
{
    [Fact]
    public async Task SeparateContexts_RestoreOriginalLeaseAndSettlement_WithoutCreatingNewIdentities()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProfilingTestDbContext>().UseSqlite(connection).Options;
        await using var first = new ProfilingTestDbContext(options);
        await first.Database.EnsureCreatedAsync();
        var sut = new EntityFrameworkProfilingCoordination<ProfilingTestDbContext>(new(), "fixture");
        var node = new ProfilingNodeIdentityProvider().GetNode();
        var attempt = Guid.NewGuid();
        var utc = DateTimeOffset.UtcNow;
        ProfilingWriterLease original;
        Guid epoch;
        await using (var transaction = await first.Database.BeginTransactionAsync())
        {
            var frame = await sut.AcquireAsync(first, default);
            original = frame.Writers.Open(new() { AttemptId = attempt, Node = node }, utc, TimeSpan.FromSeconds(30)).Value;
            frame.Writers.Synchronize(new() { Lease = original, SettledThrough = 10 }, utc, []);
            first.Set<ProfilingNodeEntity>().Add(ProfilingEntityMapper.ToEntity(null, node));
            await EntityFrameworkProfilingCoordination<ProfilingTestDbContext>.SaveAsync(first, frame, default);
            epoch = frame.State.StoreEpoch;
            await transaction.CommitAsync();
        }

        // Act
        await using var second = new ProfilingTestDbContext(options);
        await using var nextTransaction = await second.Database.BeginTransactionAsync();
        var restored = await sut.AcquireAsync(second, default);
        var retry = restored.Writers.Open(new() { AttemptId = attempt, Node = node }, utc.AddSeconds(1), TimeSpan.FromSeconds(30));

        // Assert
        restored.State.StoreEpoch.ShouldBe(epoch);
        retry.Value.WriterId.ShouldBe(original.WriterId);
        retry.Value.Token.ShouldBe(original.Token);
        retry.Value.SettledThrough.ShouldBe(10);
        (await second.Set<ProfilingWriterEntity>().CountAsync()).ShouldBe(1);
        await nextTransaction.CommitAsync();
    }

    [Fact]
    public async Task SeparateContexts_KeepSealedCutoffFixed_AcrossLostAcknowledgementResponse()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProfilingTestDbContext>().UseSqlite(connection).Options;
        await using var first = new ProfilingTestDbContext(options);
        await first.Database.EnsureCreatedAsync();
        var sut = new EntityFrameworkProfilingCoordination<ProfilingTestDbContext>(new(), "fixture");
        var node = new ProfilingNodeIdentityProvider().GetNode();
        var utc = DateTimeOffset.UtcNow;
        ProfilingWriterLease lease;
        Guid id;
        await using (var transaction = await first.Database.BeginTransactionAsync())
        {
            var frame = await sut.AcquireAsync(first, default);
            lease = frame.Writers.Open(new() { AttemptId = Guid.NewGuid(), Node = node }, utc, TimeSpan.FromSeconds(30)).Value;
            first.Set<ProfilingNodeEntity>().Add(ProfilingEntityMapper.ToEntity(null, node));
            var clear = frame.Clears.Prepare(new() { DataSet = ProfilingDataSet.Operations }, frame.Writers, utc, false).Value;
            id = clear.Id;
            frame.Clears.Acknowledge(new() { ClearId = id, Lease = lease, CompletionCutoff = 40 }, frame.Writers, utc);
            frame.Clears.Seal(clear, utc);
            await EntityFrameworkProfilingCoordination<ProfilingTestDbContext>.SaveAsync(first, frame, default);
            await transaction.CommitAsync();
        }

        // Act
        await using var second = new ProfilingTestDbContext(options);
        await using var nextTransaction = await second.Database.BeginTransactionAsync();
        var restored = await sut.AcquireAsync(second, default);
        var repeated = restored.Clears.Acknowledge(new() { ClearId = id, Lease = lease, CompletionCutoff = 100 }, restored.Writers, utc.AddSeconds(1));

        // Assert
        repeated.IsSuccess.ShouldBeTrue();
        repeated.Value.CompletionCutoff.ShouldBe(40);
        restored.Clears.Current.State.ShouldBe(ProfilingClearState.Applying);
        restored.Clears.Current.SealedUtc.ShouldBe(utc);
        restored.Clears.Current.Generation.ShouldBe(lease.Generation);
        await nextTransaction.CommitAsync();
    }

    [Fact]
    public async Task Coordination_WithoutOwnedTransaction_RejectsBeforePersistentMutation()
    {
        // Arrange
        await using var context = new ProfilingTestDbContext(new DbContextOptionsBuilder<ProfilingTestDbContext>().UseSqlite("Data Source=:memory:").Options);
        var sut = new EntityFrameworkProfilingCoordination<ProfilingTestDbContext>(new(), "fixture");

        // Act/Assert
        await Should.ThrowAsync<InvalidOperationException>(() => sut.AcquireAsync(context, default));
    }
}
