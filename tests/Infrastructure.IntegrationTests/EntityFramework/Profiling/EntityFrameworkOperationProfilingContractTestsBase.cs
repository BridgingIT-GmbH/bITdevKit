// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Profiling;

using System.Collections.Concurrent;
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
using Microsoft.Extensions.Time.Testing;

public abstract class EntityFrameworkOperationProfilingContractTestsBase : ProfilingStorageContractTestsBase, IAsyncLifetime
{
    private readonly List<ServiceProvider> services = [];
    private readonly LostCommitAcknowledgement commitFault = new();
    private readonly QueryTimeoutObserver queryTimeouts = new();
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

    /// <summary>Independent DI writers use database lease time despite skewed recording clocks and preserve post-cutoff work.</summary>
    [Fact]
    public async Task IndependentWriterHosts_SkewedClocks_ClearOldWorkAndInvalidateConcurrentPaging()
    {
        var h = await this.CreateAsync();
        var early = new FakeTimeProvider(h.Clock.GetUtcNow().AddHours(-3));
        var late = new FakeTimeProvider(h.Clock.GetUtcNow().AddHours(3));
        await using var firstHost = CreateWriterHost((EntityFrameworkProfilingStorageProvider<ProfilingProviderDbContext>)h.Provider, early);
        await using var secondHost = CreateWriterHost(this.peer, late);
        var firstWriter = firstHost.GetRequiredService<OperationProfilingWriterService>();
        var secondWriter = secondHost.GetRequiredService<OperationProfilingWriterService>();
        await firstWriter.TickAsync();
        await secondWriter.TickAsync();
        var firstId = Capture(firstHost, early);
        var secondId = Capture(secondHost, late);
        await firstWriter.TickAsync();
        await secondWriter.TickAsync();
        var firstRecord = (await this.peer.FindAsync(firstId)).Value;
        var secondRecord = (await h.Store.FindAsync(secondId)).Value;
        firstRecord.ShouldNotBeNull();
        secondRecord.ShouldNotBeNull();
        firstRecord.NodeId.ShouldNotBe(secondRecord.NodeId);
        firstRecord.Duration.ShouldBe(TimeSpan.FromMilliseconds(1));
        secondRecord.Duration.ShouldBe(TimeSpan.FromMilliseconds(1));
        var query = h.Query() with { FromUtc = h.Clock.GetUtcNow().AddHours(-4), ToUtc = h.Clock.GetUtcNow().AddHours(4), PageSize = 1 };
        var simultaneousPages = await Task.WhenAll(h.Store.QueryAsync(query), this.peer.QueryAsync(query));
        simultaneousPages.All(page => page.IsSuccess && page.Value.TotalCount == 2).ShouldBeTrue();
        var cursor = simultaneousPages[0].Value.NextCursor;

        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = await WaitForClearAsync(h);
        await firstWriter.TickAsync();
        await secondWriter.TickAsync();
        var retainedId = Capture(firstHost, early);
        await firstWriter.TickAsync();
        (await this.peer.AcknowledgeClearAsync(new() { ClearId = pending.Id, Lease = h.Lease, CompletionCutoff = 0 })).IsSuccess.ShouldBeTrue();
        var cleared = await clearing.WaitAsync(TimeSpan.FromSeconds(10));

        cleared.IsSuccess.ShouldBeTrue();
        cleared.Value.RemovedOperationCount.ShouldBe(2);
        (await h.Store.FindAsync(firstId)).Value.ShouldBeNull();
        (await this.peer.FindAsync(secondId)).Value.ShouldBeNull();
        (await this.peer.FindAsync(retainedId)).Value.ShouldNotBeNull();
        var continued = await this.peer.QueryAsync(query with { Cursor = cursor });
        continued.IsFailure.ShouldBeTrue();
        continued.Errors.ShouldContain(error => error is ProfilingQueryBoundaryError);
        firstHost.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().PersistedOperations.ShouldBe(2);
        secondHost.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().PersistedOperations.ShouldBe(1);
    }

    /// <summary>A fresh provider resumes sealed partial deletion and rejects delayed retry after coordinator loss.</summary>
    [Fact]
    public async Task Restart_AfterSeal_RecoversAcrossIndependentProviderAndClockSkew()
    {
        var options = new ProfilingOptions();
        options.Storage.MaximumMaintenanceRoots = 1;
        var h = await this.CreateAsync(options);
        var firstEnvelope = h.Envelope(h.Record(), 1);
        var secondEnvelope = h.Envelope(h.Record(), 2);
        (await h.Store.AppendAsync([firstEnvelope, secondEnvelope])).IsSuccess.ShouldBeTrue();
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = await WaitForClearAsync(h);
        (await this.peer.AcknowledgeClearAsync(new() { ClearId = pending.Id, Lease = h.Lease, CompletionCutoff = 2 })).IsSuccess.ShouldBeTrue();
        var sealedResult = await clearing.WaitAsync(TimeSpan.FromSeconds(10));
        sealedResult.Value.State.ShouldBe(ProfilingClearState.Applying);
        var restartedServices = this.CreateServices(h.Clock);
        var restarted = new EntityFrameworkProfilingStorageProvider<ProfilingProviderDbContext>(restartedServices.GetRequiredService<IServiceScopeFactory>(),
            options, new FakeTimeProvider(h.Clock.GetUtcNow().AddDays(-2)));

        var recovered = await restarted.ResumeMaintenanceAsync(new() { MaximumRoots = 1 });
        recovered.IsSuccess.ShouldBeTrue();
        recovered.Value.RemainingClears.ShouldBe(0);
        (await restarted.Operations.AppendAsync([firstEnvelope])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Cleared);
        (await this.peer.FindAsync(secondEnvelope.Record.Id)).Value.ShouldBeNull();
    }

    /// <summary>Loss before sealing expires preparation using database time and cannot delete or accept a late acknowledgement.</summary>
    [Fact]
    public async Task Restart_BeforeSeal_ExpiresPreparationWithoutDeletingRetainedData()
    {
        var options = new ProfilingOptions();
        options.Storage.ClearPreparationTimeout = TimeSpan.FromSeconds(1);
        var h = await this.CreateAsync(options);
        var envelope = h.Envelope(h.Record(), 1);
        (await h.Store.AppendAsync([envelope])).IsSuccess.ShouldBeTrue();
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = await WaitForClearAsync(h);
        h.Clock.Advance(TimeSpan.FromSeconds(2));
        var restartedServices = this.CreateServices(h.Clock);
        var restarted = new EntityFrameworkProfilingStorageProvider<ProfilingProviderDbContext>(restartedServices.GetRequiredService<IServiceScopeFactory>(),
            options, new FakeTimeProvider(h.Clock.GetUtcNow().AddDays(7)));

        (await restarted.ResumeMaintenanceAsync(new() { MaximumRoots = 1 })).IsSuccess.ShouldBeTrue();
        var result = await clearing.WaitAsync(TimeSpan.FromSeconds(10));
        result.IsFailure.ShouldBeTrue();
        if (result.Value is not null) { result.Value.SealedUtc.ShouldBeNull(); }

        (await restarted.Operations.AcknowledgeClearAsync(new() { ClearId = pending.Id, Lease = h.Lease, CompletionCutoff = 1 })).IsFailure.ShouldBeTrue();
        (await restarted.Operations.FindAsync(envelope.Record.Id)).Value.ShouldNotBeNull();
        (await restarted.Operations.AppendAsync([envelope])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadyStored);
    }

    private static ServiceProvider CreateWriterHost(EntityFrameworkProfilingStorageProvider<ProfilingProviderDbContext> provider, TimeProvider clock)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddSingleton(clock);
        collection.AddProfiling(options => options.Enabled()).WithOperationProfiling().WithProvider(_ => provider);
        return collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static Guid Capture(IServiceProvider provider, FakeTimeProvider clock)
    {
        using var operation = provider.GetRequiredService<IOperationProfiler>().BeginOperation("writer-host", OperationProfilingKind.Service);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        operation.Complete();
        return operation.Id;
    }

    private static async Task<ProfilingPendingClear> WaitForClearAsync(Harness h)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var state = await h.Store.SynchronizeWriterAsync(new() { Lease = h.Lease }, deadline.Token);
            state.IsSuccess.ShouldBeTrue();
            if (state.Value.PendingClears.Count > 0) { return state.Value.PendingClears.Single(); }

            await Task.Delay(10, deadline.Token);
        }
    }

    /// <summary>Records actual engine plans for the indexed publication-window selection without forcing a preferred plan on tiny fixtures.</summary>
    [Fact]
    public async Task PublicationWindow_ProducesActualEngineQueryPlan()
    {
        var h = await this.CreateAsync();
        (await h.Store.AppendAsync([h.Envelope(h.Record(), 1)])).IsSuccess.ShouldBeTrue();
        await using var scope = this.first.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>();
        await context.Database.OpenConnectionAsync();
        var connection = context.Database.GetDbConnection();
        var engine = context.Database.ProviderName;
        var sqlServer = engine.Contains("SqlServer", StringComparison.Ordinal);
        var quoteStart = sqlServer ? "[" : "\"";
        var quoteEnd = sqlServer ? "]" : "\"";
        string Quote(string name) => quoteStart + name + quoteEnd;
        var statement = $"SELECT {Quote("Id")} FROM {Quote("__Profiling_Operations")} WHERE {Quote("CompletedUtcTicks")} >= 0 AND {Quote("CompletedUtcTicks")} < 9223372036854775807 ORDER BY {Quote("CompletedUtcTicks")}, {Quote("CanonicalIdBytes")}";
        await using var command = connection.CreateCommand();
        if (sqlServer)
        {
            command.CommandText = "SET SHOWPLAN_XML ON";
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            command.CommandText = (sqlServer ? "" : engine.Contains("Sqlite", StringComparison.Ordinal) ? "EXPLAIN QUERY PLAN " : "EXPLAIN ") + statement;
            await using var reader = await command.ExecuteReaderAsync();
            var plan = new List<string>();
            while (await reader.ReadAsync()) { plan.Add(reader.GetString(sqlServer || !engine.Contains("Sqlite", StringComparison.Ordinal) ? 0 : 3)); }

            plan.ShouldNotBeEmpty();
            await File.WriteAllLinesAsync(Path.Combine(Path.GetTempPath(), "bitdevkit-phase9-query-plan-" + engine + ".txt"), plan);
        }
        finally
        {
            if (sqlServer) { command.CommandText = "SET SHOWPLAN_XML OFF"; await command.ExecuteNonQueryAsync(); }
        }

        command.CommandText = sqlServer ? "SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(128))"
            : engine.Contains("Sqlite", StringComparison.Ordinal) ? "SELECT sqlite_version()" : "SHOW server_version";
        var version = (await command.ExecuteScalarAsync())?.ToString();
        version.ShouldNotBeNullOrWhiteSpace();
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "bitdevkit-phase12-engine-version-" + engine + ".txt"), version);
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
        var found = await this.peer.FindAsync(record.Id);
        found.Value.Measurements.ShouldHaveSingleItem().Value.ShouldBe(value);
        found.Value.Segments.ShouldHaveSingleItem().Measurements.ShouldHaveSingleItem().Value.ShouldBe(value);
        await using (var readScope = this.first.CreateAsyncScope())
        {
            var readContext = readScope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>();
            var stored = await readContext.Set<OperationProfilingEntity>().AsNoTracking().SingleAsync();
            var summary = await readContext.Set<OperationProfilingSegmentEntity>().AsNoTracking().SingleAsync();
            stored.RecordJson.ShouldContain("items");
            summary.SummaryJson.ShouldContain("items");
            readContext.Model.GetEntityTypes().ShouldNotContain(entity => entity.GetTableName() == "__Profiling_OperationMeasurements");
        }

        await h.Store.CloseWriterAsync(h.Lease);

        var cleared = await this.peer.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });

        cleared.IsSuccess.ShouldBeTrue();
        cleared.Value.RemovedOperationCount.ShouldBe(1);
        cleared.Value.RemovedSegmentSummaryCount.ShouldBe(1);
        await using var scope = this.first.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>();
        (await context.Set<OperationProfilingEntity>().CountAsync()).ShouldBe(0);
        (await context.Set<OperationProfilingSegmentEntity>().CountAsync()).ShouldBe(0);
        (await context.Set<OperationProfilingDimensionEntity>().CountAsync()).ShouldBe(0);
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
        script.ShouldNotContain("__Profiling_OperationMeasurements");
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

    /// <summary>All query commands, including generated aggregate SQL, use the configured budget with whole-second rounding.</summary>
    /// <example>Run with the Query_ConfiguredTimeout filter on each actual database engine.</example>
    [Theory]
    [InlineData(5, 5)]
    [InlineData(30, 30)]
    [InlineData(1.2, 2)]
    [InlineData(0.1, 1)]
    public async Task Query_ConfiguredTimeout_AppliesToLookupPagingGroupingAndAnalysis(double seconds, int expectedSeconds)
    {
        var options = new ProfilingOptions();
        options.Queries.Timeout = TimeSpan.FromSeconds(seconds);
        var h = await this.CreateAsync(options);
        var record = h.Record();
        (await h.Store.AppendAsync([h.Envelope(record, 1)])).IsSuccess.ShouldBeTrue();
        this.queryTimeouts.CommandTimeouts.Clear();

        (await h.Store.FindAsync(record.Id)).IsSuccess.ShouldBeTrue();
        (await h.Store.QueryAsync(h.Query())).IsSuccess.ShouldBeTrue();
        foreach (var view in Enum.GetValues<OperationProfilingView>())
        {
            (await h.Store.GroupAsync(h.Query() with { View = view })).IsSuccess.ShouldBeTrue();
        }

        (await h.Store.SelectAnalysisAsync(h.Query())).IsSuccess.ShouldBeTrue();
        this.queryTimeouts.CommandTimeouts.ShouldNotBeEmpty();
        // EF execution interceptors cover normal reads; the raw-command factory must also preserve the same timeout.
        this.queryTimeouts.CommandTimeouts.ShouldAllBe(timeout => timeout == expectedSeconds);
        await using (var scope = this.first.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>();
            context.Database.SetCommandTimeout(expectedSeconds);
            await using var command = EntityFrameworkProfilingQuerySql<ProfilingProviderDbContext>.CreateCommand(context, context.Set<OperationProfilingEntity>());
            command.CommandTimeout.ShouldBe(expectedSeconds);
        }

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => h.Store.QueryAsync(h.Query(), canceled.Token));
    }

    private ServiceProvider CreateServices(TimeProvider clock)
    {
        var collection = new ServiceCollection();
        collection.AddDbContext<ProfilingProviderDbContext>(builder =>
        {
            this.ConfigureDatabase(builder);
            builder.AddInterceptors(new DatabaseUtcFixture(clock), this.commitFault, this.queryTimeouts);
        });
        var provider = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        this.services.Add(provider);
        return provider;
    }

    /// <summary>Database latency consumes the next-batch budget before either another root batch or node pruning can begin.</summary>
    /// <example>Run with the Maintenance_ElapsedBudget filter on each actual database engine.</example>
    [Fact]
    public async Task Maintenance_ElapsedBudget_DefersNodePruningAndRetentionToNextCall()
    {
        var options = new ProfilingOptions();
        options.Storage.MaintenanceTimeBudget = TimeSpan.FromSeconds(2);
        var h = await this.CreateAsync(options);
        await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddDays(-30));
        this.queryTimeouts.Statements.Clear();
        this.queryTimeouts.RuntimeCountDelay = TimeSpan.FromMilliseconds(100);

        var deferred = await h.Provider.ResumeMaintenanceAsync(new() { Budget = TimeSpan.FromMilliseconds(50) });

        deferred.IsSuccess.ShouldBeTrue();
        this.queryTimeouts.RuntimeCountDelay.ShouldBe(TimeSpan.Zero);
        deferred.Value.RemovedRuntimeSessions.ShouldBe(0);
        this.queryTimeouts.Statements.ShouldNotContain(sql => sql.Contains("__Profiling_Nodes", StringComparison.Ordinal)
            && sql.Contains("NOT EXISTS", StringComparison.Ordinal));
        (await h.Provider.Runtime.ListSessionsAsync()).Value.Count.ShouldBe(1);
        this.queryTimeouts.Statements.Clear();
        var completed = await h.Provider.ResumeMaintenanceAsync(new() { Budget = options.Storage.MaintenanceTimeBudget });
        completed.IsSuccess.ShouldBeTrue();
        completed.Value.RemovedRuntimeSessions.ShouldBe(1);
        this.queryTimeouts.Statements.ShouldContain(sql => sql.Contains("__Profiling_Nodes", StringComparison.Ordinal)
            && sql.Contains("NOT EXISTS", StringComparison.Ordinal));
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

    private sealed class QueryTimeoutObserver : DbCommandInterceptor
    {
        /// <summary>Gets initialized command timeout values before provider disposal or pooling resets the commands.</summary>
        /// <example><code>observer.CommandTimeouts.ShouldAllBe(timeout => timeout == 30);</code></example>
        public ConcurrentQueue<int> CommandTimeouts { get; } = new();

        /// <summary>Gets generated SQL structure for bounded scheduling assertions without retaining command objects.</summary>
        /// <example><code>observer.Statements.Clear();</code></example>
        public ConcurrentQueue<string> Statements { get; } = new();

        /// <summary>Gets or sets a one-shot database delay for the next Runtime retention count.</summary>
        /// <example><code>observer.RuntimeCountDelay = TimeSpan.FromMilliseconds(100);</code></example>
        public TimeSpan RuntimeCountDelay { get; set; }

        /// <inheritdoc />
        public override DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand result)
        {
            this.CommandTimeouts.Enqueue(result.CommandTimeout);
            this.Statements.Enqueue(result.CommandText);
            return result;
        }

        /// <inheritdoc />
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (this.RuntimeCountDelay > TimeSpan.Zero && command.CommandText.Contains("__Profiling_RuntimeSessions", StringComparison.Ordinal)
                && (command.CommandText.Contains("COUNT(", StringComparison.OrdinalIgnoreCase) || command.CommandText.Contains("COUNT_BIG(", StringComparison.OrdinalIgnoreCase)))
            {
                var delay = this.RuntimeCountDelay;
                this.RuntimeCountDelay = TimeSpan.Zero;
                await Task.Delay(delay, cancellationToken);
            }

            return result;
        }
    }

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
