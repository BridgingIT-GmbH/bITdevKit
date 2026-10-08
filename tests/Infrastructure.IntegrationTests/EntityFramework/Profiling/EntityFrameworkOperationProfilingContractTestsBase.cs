// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Profiling;

using System.Data;
using System.Data.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using BridgingIT.DevKit.Tests.Profiling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

public abstract class EntityFrameworkOperationProfilingContractTestsBase : ProfilingStorageContractTestsBase, IAsyncLifetime
{
    private readonly List<ServiceProvider> services = [];
    private readonly LostCommitAcknowledgement commitFault = new();
    private ServiceProvider first;
    private ServiceProvider second;
    private EntityFrameworkProfilingStorageProvider<ProfilingProviderDbContext> peer;
    protected abstract void ConfigureDatabase(DbContextOptionsBuilder builder);

    protected override async Task<IProfilingStorageProvider> CreateProviderAsync(ProfilingOptions options, TimeProvider clock)
    {
        this.first = this.CreateServices(clock);
        this.second = this.CreateServices(clock);
        await using (var scope = this.first.CreateAsyncScope())
        {
            // Only the isolated fixture owns schema creation. Production provider code never does.
            await scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>().Database.EnsureCreatedAsync();
        }

        this.peer = new(this.second.GetRequiredService<IServiceScopeFactory>(), options, clock);
        return new EntityFrameworkProfilingStorageProvider<ProfilingProviderDbContext>(this.first.GetRequiredService<IServiceScopeFactory>(), options, clock);
    }

    [Fact]
    public async Task SeparateProcesses_AppendAndSettle_UseSameDurableEpoch()
    {
        var h = await this.CreateAsync();
        var envelope = h.Envelope(h.Record(), 1);
        var firstWrite = await h.Store.AppendAsync([envelope]);
        var retry = await this.peer.AppendAsync([envelope]);
        await this.peer.SynchronizeWriterAsync(new() { Lease = h.Lease, SettledThrough = 1 });
        var synchronized = await h.Store.SynchronizeWriterAsync(new() { Lease = h.Lease });

        firstWrite.IsSuccess.ShouldBeTrue();
        retry.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadyStored);
        retry.Value.Records.Single().CommitWatermark.ShouldBe(firstWrite.Value.Records.Single().CommitWatermark);
        synchronized.Value.Lease.SettledThrough.ShouldBe(1);
        this.peer.Capabilities.Scope.ShouldBe(h.Provider.Capabilities.Scope);
    }

    [Fact]
    public async Task LostCommitAcknowledgement_ReportsUnknown_AndOriginalEnvelopeRetriesIdempotently()
    {
        var h = await this.CreateAsync();
        var envelope = h.Envelope(h.Record(), 1);
        this.commitFault.Arm();

        var unknown = await h.Store.AppendAsync([envelope]);
        var retry = await this.peer.AppendAsync([envelope]);

        unknown.IsFailure.ShouldBeTrue();
        unknown.Errors.OfType<ProfilingPersistenceError>().Single().MayHaveCommitted.ShouldBeTrue();
        retry.IsSuccess.ShouldBeTrue();
        retry.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadyStored);
        (await h.Store.FindAsync(envelope.Record.Id)).Value.Id.ShouldBe(envelope.Record.Id);
    }

    [Fact]
    public async Task Clear_DeletesEveryOwnedSummaryDimensionAndMeasurement_Atomically()
    {
        var h = await this.CreateAsync();
        var record = h.Record();
        var value = new ProfilingValue(ProfilingValueType.Int64, "42");
        var statistics = new ProfilingDurationStatistics
        {
            Count = 1, TotalDuration = TimeSpan.FromMilliseconds(1), TotalSelfDuration = TimeSpan.FromMilliseconds(1),
            MinimumDuration = TimeSpan.FromMilliseconds(1), MaximumDuration = TimeSpan.FromMilliseconds(1),
        };
        record = record with
        {
            Dimensions = [Dimension("tenant", "sample")],
            Measurements = [new() { Key = "items", Unit = "count", Value = value, Aggregation = MeasurementAggregation.Sum }],
            Segments = [new()
            {
                OperationId = record.Id, NodeId = record.NodeId, Key = "Load", Path = new(["Load"]), Statistics = statistics,
                Outcomes = [new() { Outcome = ProfilingSegmentOutcome.Completed, Statistics = statistics }],
                Dimensions = [new() { Key = "source", Value = new(ProfilingValueType.String, "sql"), SampleCount = 1 }],
                Measurements = [new() { Key = "items", Unit = "count", Aggregation = MeasurementAggregation.Sum, Value = value, SampleCount = 1, LastCompletionSequence = 1, LastCompletedUtc = h.Clock.GetUtcNow() }],
            }],
        };
        var appended = await h.Store.AppendAsync([h.Envelope(record, 1)]);
        appended.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);
        await h.Store.CloseWriterAsync(h.Lease);

        var cleared = await this.peer.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });

        cleared.IsSuccess.ShouldBeTrue();
        cleared.Value.RemovedOperationCount.ShouldBe(1);
        cleared.Value.RemovedSegmentSummaryCount.ShouldBe(1);
        await using var scope = this.first.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>();
        (await context.ProfilingOperations.CountAsync()).ShouldBe(0);
        (await context.ProfilingOperationSegments.CountAsync()).ShouldBe(0);
        (await context.ProfilingOperationDimensions.CountAsync()).ShouldBe(0);
        (await context.ProfilingOperationMeasurements.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Node_ExactProcessIdentityTicks_RoundTripAboveDatabaseTimestampPrecision()
    {
        var h = await this.CreateAsync();
        var node = h.Node with { Identity = ProfilingIdentityFactory.CreateNode(), ProcessStartedUtc = h.Clock.GetUtcNow().AddTicks(1) };
        var lease = (await this.peer.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = node })).Value;
        var record = h.Record() with { Node = node };

        var written = await h.Store.AppendAsync([h.Envelope(record, 1, lease)]);
        var found = await this.peer.FindAsync(record.Id);

        written.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);
        found.Value.Node.ProcessStartedUtc.ShouldBe(node.ProcessStartedUtc);
    }

    [Fact]
    public async Task OperationNode_RuntimeRegistration_AttachesIndependentBroadcastCorrelation()
    {
        var h = await this.CreateAsync();
        var correlation = new RuntimeProfilingNodeCorrelation("runtime-peer", h.Clock.GetUtcNow().AddSeconds(1));

        var attached = await h.Provider.Runtime.GetOrCreateNodeAsync(correlation, h.Node with { Correlation = correlation });
        var found = await this.peer.Runtime.FindNodeAsync(correlation);
        var record = h.Record();
        var written = await this.peer.AppendAsync([h.Envelope(record, 1)]);

        attached.IsSuccess.ShouldBeTrue();
        found.IsSuccess.ShouldBeTrue();
        found.Value.Identity.ShouldBe(h.Node.Identity);
        found.Value.ProcessStartedUtc.ShouldBe(h.Node.ProcessStartedUtc);
        found.Value.Correlation.ShouldBe(correlation);
        written.Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Accepted);
    }

    [Fact]
    public async Task ProviderStartup_DoesNotCreateSchema_AndHostTablesSurviveMaintenance()
    {
        // A fresh fixture database has no tables until the test executes its migration SQL.
        var clock = TimeProvider.System;
        this.first = this.CreateServices(clock);
        var sut = new EntityFrameworkProfilingStorageProvider<ProfilingProviderDbContext>(this.first.GetRequiredService<IServiceScopeFactory>());
        await using var scope = this.first.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>();
        (await context.Database.EnsureCreatedAsync()).ShouldBeTrue();
        context.HostRows.Add(new() { Id = 1, Value = "preserve" });
        await context.SaveChangesAsync();

        var cleared = await sut.ClearAsync(new());
        var maintained = await sut.ResumeMaintenanceAsync(new());

        cleared.IsSuccess.ShouldBeTrue();
        maintained.IsSuccess.ShouldBeTrue();
        (await context.HostRows.SingleAsync()).Value.ShouldBe("preserve");
    }

    [Fact]
    public async Task MigrationSql_ContainsFinalSchemaAndIndexes_WithoutLegacyTables()
    {
        var h = await this.CreateAsync();
        await using var scope = this.first.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>();
        var script = context.Database.GenerateCreateScript();
        script.ShouldContain("__Profiling_Operations");
        script.ShouldContain("__Profiling_RuntimeSessions");
        script.ShouldContain("__Profiling_RuntimeGate");
        script.ShouldContain("__Profiling_OperationSegments");
        script.ShouldNotContain("__Profiling_Sessions");
        var model = context.GetService<IDesignTimeModel>().Model;
        var operations = context.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(operations, model);
        commands.Count.ShouldBeGreaterThan(0);
        string.Join('\n', commands.Select(command => command.CommandText)).ShouldContain("__Profiling_Operations");
        h.Provider.Capabilities.Shared.ShouldBeTrue();
    }

    [Fact]
    public async Task Query_GroupDurationOverflow_RemainsExplicitAndDoesNotUseFloatingPointSum()
    {
        var h = await this.CreateAsync();
        var duration = TimeSpan.MaxValue;
        var record = h.Record() with { Duration = duration, WallTime = [new() { Kind = ProfilingWallTimeKind.OutsideSegments, Duration = duration }] };
        await h.Store.AppendAsync([h.Envelope(record, 1), h.Envelope(record with { Id = Guid.NewGuid() }, 2)]);

        var grouped = await h.Store.GroupAsync(h.Query() with { View = OperationProfilingView.ByCount });

        grouped.IsSuccess.ShouldBeTrue();
        grouped.Value.Groups.Single().Count.ShouldBe(2);
        grouped.Value.Groups.Single().DurationUnavailable.ShouldBeTrue();
        grouped.Value.Groups.Single().TotalDuration.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task Query_GroupDurationTicksAboveDoublePrecision_RemainExact()
    {
        var h = await this.CreateAsync();
        var duration = TimeSpan.FromTicks(9007199254740993L);
        var record = h.Record() with { Duration = duration, WallTime = [new() { Kind = ProfilingWallTimeKind.OutsideSegments, Duration = duration }] };
        await h.Store.AppendAsync([h.Envelope(record, 1), h.Envelope(record with { Id = Guid.NewGuid() }, 2)]);

        var grouped = await h.Store.GroupAsync(h.Query() with { View = OperationProfilingView.ByCount });

        grouped.IsSuccess.ShouldBeTrue();
        grouped.Value.Groups.Single().TotalDuration.Ticks.ShouldBe(18014398509481986L);
    }

    private ServiceProvider CreateServices(TimeProvider clock)
    {
        var collection = new ServiceCollection();
        collection.AddDbContext<ProfilingProviderDbContext>(builder =>
        {
            this.ConfigureDatabase(builder);
            builder.AddInterceptors(new DatabaseUtcFixture(clock), this.commitFault);
        });
        var provider = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        this.services.Add(provider);
        return provider;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        if (this.first is not null)
        {
            await using var scope = this.first.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>().Database.EnsureDeletedAsync();
        }

        foreach (var provider in this.services) { await provider.DisposeAsync(); }
    }

    private sealed class LostCommitAcknowledgement : DbTransactionInterceptor
    {
        private int armed;
        public void Arm() => Interlocked.Exchange(ref this.armed, 1);
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref this.armed, 0) == 1)
            {
                throw new CommitAcknowledgementException();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CommitAcknowledgementException : DbException;

    // Controlled provider-authoritative time is supplied at the database-command boundary.
    // Recorder/host clocks are not used to issue or renew leases in production code.
    private sealed class DatabaseUtcFixture(TimeProvider clock) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Replace(command, result));

        private InterceptionResult<DbDataReader> Replace(DbCommand command, InterceptionResult<DbDataReader> result)
        {
            if (!command.CommandText.Contains("strftime", StringComparison.Ordinal) && !command.CommandText.Contains("SYSUTCDATETIME()", StringComparison.Ordinal)
                && !command.CommandText.Contains("clock_timestamp()", StringComparison.Ordinal)) { return result; }

            var text = command.CommandText.Contains("strftime", StringComparison.Ordinal);
            var table = new DataTable();
            table.Columns.Add("Value", text ? typeof(string) : typeof(DateTime));
            table.Rows.Add(text ? clock.GetUtcNow().UtcDateTime.ToString("O") : clock.GetUtcNow().UtcDateTime);
            return InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader());
        }
    }
}

public sealed class ProfilingProviderDbContext(DbContextOptions<ProfilingProviderDbContext> options) : DbContext(options), IProfilingDbContext
{
    public DbSet<OperationProfilingEntity> ProfilingOperations { get; set; }
    public DbSet<OperationProfilingSegmentEntity> ProfilingOperationSegments { get; set; }
    public DbSet<OperationProfilingDimensionEntity> ProfilingOperationDimensions { get; set; }
    public DbSet<OperationProfilingMeasurementEntity> ProfilingOperationMeasurements { get; set; }
    public DbSet<ProfilingStoreStateEntity> ProfilingStoreStates { get; set; }
    public DbSet<ProfilingRuntimeGateEntity> ProfilingRuntimeGates { get; set; }
    public DbSet<ProfilingWriterEntity> ProfilingWriters { get; set; }
    public DbSet<ProfilingClearEntity> ProfilingClears { get; set; }
    public DbSet<RuntimeProfilingSessionEntity> ProfilingSessions { get; set; }
    public DbSet<RuntimeProfilingInvalidSessionEntity> ProfilingInvalidSessions { get; set; }
    public DbSet<ProfilingNodeEntity> ProfilingNodes { get; set; }
    public DbSet<RuntimeProfilingParticipationEntity> ProfilingParticipations { get; set; }
    public DbSet<RuntimeProfilingSnapshotEntity> ProfilingSnapshots { get; set; }
    public DbSet<RuntimeProfilingMetricObservationEntity> ProfilingMetricObservations { get; set; }
    public DbSet<ProfilingHostRow> HostRows { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureProfiling();
        modelBuilder.Entity<ProfilingHostRow>().ToTable("HostRows");
        modelBuilder.Entity<ProfilingHostRow>().Property(row => row.Id).ValueGeneratedNever();
    }
}

public sealed class ProfilingHostRow
{
    public int Id { get; set; }
    public string Value { get; set; }
}
