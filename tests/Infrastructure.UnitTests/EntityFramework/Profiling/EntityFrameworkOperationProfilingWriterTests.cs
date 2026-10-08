// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

public sealed class EntityFrameworkOperationProfilingWriterTests
{
    [Fact]
    public async Task PartialBatch_StoresValidGraph_AndReturnsOriginalPublicationOnRetry()
    {
        await using var fixture = await Fixture.CreateAsync();
        var record = fixture.Record() with { Dimensions = [new() { Key = "Size", Value = new ProfilingValue(ProfilingValueType.Int64, "42") }] };
        var valid = fixture.Envelope(record, 1);

        var first = await fixture.Sut.AppendAsync([valid, fixture.Envelope(record with { Id = Guid.NewGuid(), Key = "" }, 2)]);
        var retry = await fixture.Sut.AppendAsync([valid with { Record = record with { Key = "" } }]);
        var found = await fixture.Sut.FindAsync(record.Id);

        first.IsSuccess.ShouldBeTrue();
        first.Value.Records.Select(result => result.Outcome).ShouldBe(new[] { ProfilingWriteOutcome.Accepted, ProfilingWriteOutcome.PermanentFailure });
        retry.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadyStored);
        retry.Value.Records.Single().CommitWatermark.ShouldBe(first.Value.Records[0].CommitWatermark);
        found.Value.Dimensions.Single().Value.ShouldBe(new ProfilingValue(ProfilingValueType.Int64, "42"));
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProfilingTestDbContext>();
        (await context.ProfilingOperations.CountAsync()).ShouldBe(1);
        (await context.ProfilingOperationDimensions.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Retention_ProtectsUnsettledRoots_AndDoesNotRestoreSettledDeletedRecords()
    {
        var options = new ProfilingOptions();
        options.Operations.MaximumRetainedOperations = 1;
        await using var fixture = await Fixture.CreateAsync(options);
        var original = fixture.Envelope(fixture.Record(), 1);
        (await fixture.Sut.AppendAsync([original])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);

        var blocked = await fixture.Sut.AppendAsync([fixture.Envelope(fixture.Record(), 2)]);
        await fixture.Sut.SynchronizeWriterAsync(new() { Lease = fixture.Lease, SettledThrough = 1 });
        var admitted = await fixture.Sut.AppendAsync([fixture.Envelope(fixture.Record(), 3)]);
        var replay = await fixture.Sut.AppendAsync([original]);

        blocked.Value.Records.Single().SafeCode.ShouldBe("RetentionCapacity");
        admitted.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);
        replay.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadySettled);
        (await fixture.Sut.FindAsync(original.Record.Id)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task RetentionInsideBatch_DoesNotReportDeletedPrefetchedIdentityAsStored()
    {
        var options = new ProfilingOptions();
        options.Operations.MaximumRetainedOperations = 1;
        await using var fixture = await Fixture.CreateAsync(options);
        var original = fixture.Envelope(fixture.Record(), 1);
        await fixture.Sut.AppendAsync([original]);
        await fixture.Sut.SynchronizeWriterAsync(new() { Lease = fixture.Lease, SettledThrough = 1 });

        var result = await fixture.Sut.AppendAsync([fixture.Envelope(fixture.Record(), 2), original]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Records.Select(record => record.Outcome).ShouldBe(new[] { ProfilingWriteOutcome.Accepted, ProfilingWriteOutcome.AlreadySettled });
        (await fixture.Sut.FindAsync(original.Record.Id)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task SeparateProviders_ShareFixedClearCutoff_AndRetainPostCutoffRecords()
    {
        await using var fixture = await Fixture.CreateAsync();
        var second = new EntityFrameworkProfilingStorageProvider<ProfilingTestDbContext>(fixture.Services.GetRequiredService<IServiceScopeFactory>());
        var old = fixture.Envelope(fixture.Record(), 1);
        await fixture.Sut.AppendAsync([old]);
        var clearing = second.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        ProfilingPendingClear pending = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (pending is null)
        {
            var sync = await fixture.Sut.SynchronizeWriterAsync(new() { Lease = fixture.Lease }, deadline.Token);
            pending = sync.Value.PendingClears.SingleOrDefault();
            if (pending is null)
            {
                await Task.Delay(10, deadline.Token);
            }
        }

        await fixture.Sut.AcknowledgeClearAsync(new() { ClearId = pending.Id, Lease = fixture.Lease, CompletionCutoff = 1 });
        var after = fixture.Envelope(fixture.Record(), 2);
        await fixture.Sut.AppendAsync([after]);
        var cleared = await clearing;
        var replay = await fixture.Sut.AppendAsync([old]);

        cleared.IsSuccess.ShouldBeTrue();
        cleared.Value.State.ShouldBe(ProfilingClearState.Completed);
        cleared.Value.RemovedOperationCount.ShouldBe(1);
        replay.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Cleared);
        (await second.FindAsync(old.Record.Id)).Value.ShouldBeNull();
        (await second.FindAsync(after.Record.Id)).Value.Id.ShouldBe(after.Record.Id);
    }

    [Fact]
    public async Task WriterLease_UsesDatabaseUtc_DespiteSkewedHostClock()
    {
        await using var fixture = await Fixture.CreateAsync();
        var skewed = new FakeTimeProvider(new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var sut = new EntityFrameworkProfilingStorageProvider<ProfilingTestDbContext>(fixture.Services.GetRequiredService<IServiceScopeFactory>(), clock: skewed);
        var start = DateTimeOffset.UtcNow;

        var lease = await sut.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = fixture.Node });

        lease.IsSuccess.ShouldBeTrue();
        lease.Value.ExpiresUtc.ShouldBeGreaterThan(start.AddSeconds(25));
        lease.Value.ExpiresUtc.ShouldBeLessThan(DateTimeOffset.UtcNow.AddSeconds(31));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection { get; init; }
        public ServiceProvider Services { get; init; }
        public EntityFrameworkProfilingStorageProvider<ProfilingTestDbContext> Sut { get; init; }
        public ProfilingNode Node { get; init; }
        public ProfilingWriterLease Lease { get; set; }
        public static async Task<Fixture> CreateAsync(ProfilingOptions options = null)
        {
            var connection = new SqliteConnection($"Data Source={Path.Combine(Path.GetTempPath(), $"profiling-{Guid.NewGuid():N}.db")};Pooling=False");
            await connection.OpenAsync();
            var services = new ServiceCollection().AddDbContext<ProfilingTestDbContext>(builder => builder.UseSqlite(connection.ConnectionString)).BuildServiceProvider();
            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ProfilingTestDbContext>().Database.EnsureCreatedAsync();
            }

            var fixture = new Fixture
            {
                Connection = connection, Services = services, Node = new ProfilingNodeIdentityProvider().GetNode(),
                Sut = new(services.GetRequiredService<IServiceScopeFactory>(), options),
            };
            var opened = await fixture.Sut.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = fixture.Node });
            opened.IsSuccess.ShouldBeTrue();
            fixture.Lease = opened.Value;
            return fixture;
        }

        public OperationProfilingRecord Record() => new()
        {
            Id = Guid.NewGuid(), Key = "Report", Kind = "Service", Node = this.Node, StartedUtc = DateTimeOffset.UtcNow.AddMilliseconds(-10),
            CompletedUtc = DateTimeOffset.UtcNow, Duration = TimeSpan.FromMilliseconds(10), Outcome = OperationProfilingOutcome.Completed,
            WallTime = [new() { Kind = ProfilingWallTimeKind.OutsideSegments, Duration = TimeSpan.FromMilliseconds(10) }],
        };
        public ProfilingWriteEnvelope Envelope(OperationProfilingRecord record, long sequence) => new() { Record = record, CompletionSequence = sequence, Lease = this.Lease };
        public async ValueTask DisposeAsync()
        {
            await this.Services.DisposeAsync();
            var path = this.Connection.DataSource;
            await this.Connection.DisposeAsync();
            File.Delete(path);
        }
    }
}
