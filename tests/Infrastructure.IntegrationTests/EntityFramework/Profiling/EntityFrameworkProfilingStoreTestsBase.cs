// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public abstract class EntityFrameworkProfilingStoreTestsBase
{
    protected abstract void ConfigureDatabase(DbContextOptionsBuilder options);

    [Fact]
    public async Task CompetingStarts_AcrossStoreInstances_CreateOneActiveSession()
    {
        // Arrange
        await using var harness = await this.CreateHarnessAsync();
        var startedUtc = DateTimeOffset.UtcNow;

        // Act
        var results = await Task.WhenAll(
            harness.First.GetOrCreateActiveSessionAsync(CreateRequest(startedUtc)),
            harness.Second.GetOrCreateActiveSessionAsync(
                CreateRequest(startedUtc.AddMilliseconds(1))
            )
        );

        // Assert
        results.ShouldAllBe(result => result.IsSuccess);
        results.Select(result => result.Value.Session.Identity.Id).Distinct().Count().ShouldBe(1);
        results.Count(result => result.Value.Created).ShouldBe(1);
        (await harness.First.ListSessionsAsync()).Value.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CompetingFinalization_AcrossStoreInstances_LeavesOneTerminalState()
    {
        // Arrange
        await using var harness = await this.CreateHarnessAsync();
        var startedUtc = DateTimeOffset.UtcNow;
        var session = (await harness.First.GetOrCreateActiveSessionAsync(CreateRequest(startedUtc)))
            .Value
            .Session;

        // Act
        var results = await Task.WhenAll(
            harness.First.TryTransitionSessionAsync(
                session.Identity.Id,
                [ProfilingSessionState.Running],
                ProfilingSessionState.Stopped,
                startedUtc.AddSeconds(1)
            ),
            harness.Second.TryTransitionSessionAsync(
                session.Identity.Id,
                [ProfilingSessionState.Running],
                ProfilingSessionState.Stopped,
                startedUtc.AddSeconds(1)
            )
        );

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        (await harness.First.FindSessionAsync(session.Identity.Key)).Value.State.ShouldBe(
            ProfilingSessionState.Stopped
        );
    }

    [Fact]
    public async Task PhaseMarkerAndStop_Compete_MarkerNeverCommitsAfterTerminalState()
    {
        // Arrange
        await using var harness = await this.CreateHarnessAsync();
        var startedUtc = DateTimeOffset.UtcNow;
        var session = (await harness.First.GetOrCreateActiveSessionAsync(CreateRequest(startedUtc)))
            .Value
            .Session;
        var marker = new ProfilingPhaseMarker(
            Guid.NewGuid(),
            session.Identity.Id,
            session.Identity.Key,
            "load",
            startedUtc.AddSeconds(1)
        );

        // Act
        var markerTask = harness.First.AddPhaseMarkerAsync(marker);
        var stopTask = harness.Second.TryTransitionSessionAsync(
            session.Identity.Id,
            [ProfilingSessionState.Running],
            ProfilingSessionState.Stopped,
            startedUtc.AddSeconds(2)
        );
        await Task.WhenAll(markerTask, stopTask);
        var markerResult = await markerTask;
        var stopResult = await stopTask;

        // Assert
        stopResult.IsSuccess.ShouldBeTrue();
        var data = (await harness.First.GetSessionDataAsync(session.Identity.Key)).Value;
        data.Session.State.ShouldBe(ProfilingSessionState.Stopped);
        data.PhaseMarkers.Count.ShouldBe(markerResult.IsSuccess ? 1 : 0);
    }

    [Fact]
    public async Task CompetingOwnedDocumentWrites_AcrossStoreInstances_PreserveBothChanges()
    {
        // Arrange
        await using var harness = await this.CreateHarnessAsync();
        var startedUtc = DateTimeOffset.UtcNow;
        var session = (await harness.First.GetOrCreateActiveSessionAsync(CreateRequest(startedUtc)))
            .Value
            .Session;
        var firstMarker = new ProfilingPhaseMarker(
            Guid.NewGuid(),
            session.Identity.Id,
            session.Identity.Key,
            "first",
            startedUtc.AddSeconds(1)
        );
        var secondMarker = firstMarker with
        {
            Id = Guid.NewGuid(),
            Name = "second",
            TimestampUtc = startedUtc.AddSeconds(2),
        };

        // Act
        var results = await Task.WhenAll(
            harness.First.AddPhaseMarkerAsync(firstMarker),
            harness.Second.AddPhaseMarkerAsync(secondMarker)
        );

        // Assert
        results.ShouldAllBe(result => result.IsSuccess);
        var markers = (await harness.First.GetSessionDataAsync(session.Identity.Key))
            .Value
            .PhaseMarkers;
        markers.Count.ShouldBe(2);
        markers
            .Select(marker => marker.Id)
            .ShouldBe([firstMarker.Id, secondMarker.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task ClearAndDelayedSnapshot_Compete_ClearRemainsAtomicAndTombstoneRejectsLaterWrite()
    {
        // Arrange
        await using var harness = await this.CreateHarnessAsync();
        var startedUtc = DateTimeOffset.UtcNow;
        var session = (await harness.First.GetOrCreateActiveSessionAsync(CreateRequest(startedUtc)))
            .Value
            .Session;
        var correlation = new ProfilingNodeCorrelation("node-a", startedUtc);
        var node = (
            await harness.First.GetOrCreateNodeAsync(
                correlation,
                new ProfilingNode
                {
                    Identity = ProfilingNodeIdentity.Create(),
                    Correlation = correlation,
                    HostName = "localhost",
                    ProcessId = 1234,
                }
            )
        ).Value;
        await harness.First.TryTransitionSessionAsync(
            session.Identity.Id,
            [ProfilingSessionState.Running],
            ProfilingSessionState.Stopped,
            startedUtc.AddSeconds(2)
        );
        var snapshot = CreateSnapshot(session, node, startedUtc.AddSeconds(1));

        // Act
        var clearTask = harness.First.ClearAsync();
        var appendTask = harness.Second.AddSnapshotAsync(snapshot);
        await Task.WhenAll(clearTask, appendTask);
        var clearResult = await clearTask;
        var delayed = await harness.Second.AddSnapshotAsync(
            snapshot with
            {
                Identity = ProfilingSnapshotIdentity.Create(),
                Sequence = 2,
            }
        );

        // Assert
        clearResult.IsSuccess.ShouldBeTrue();
        delayed.IsFailure.ShouldBeTrue();
        (await harness.First.ListSessionsAsync()).Value.ShouldBeEmpty();
        await harness.AssertSnapshotCountAsync(0);
    }

    private async Task<ProfilingStoreHarness> CreateHarnessAsync()
    {
        var firstProvider = this.CreateProvider();
        var secondProvider = this.CreateProvider();
        await using (var scope = firstProvider.CreateAsyncScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<ProfilingIntegrationDbContext>()
                .Database.EnsureCreatedAsync();
        }

        return new ProfilingStoreHarness(firstProvider, secondProvider);
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ProfilingIntegrationDbContext>(this.ConfigureDatabase);
        services
            .AddProfiling(options => options.Enabled())
            .WithEntityFrameworkStore<ProfilingIntegrationDbContext>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ProfilingSessionCreateRequest CreateRequest(DateTimeOffset startedUtc) =>
        new(
            ProfilingSessionIdentity.Create(),
            "integration",
            startedUtc,
            ProfilingOptions.MinimumSamplingInterval,
            TimeSpan.FromSeconds(10),
            ["integration"]
        );

    private static ProfilingSnapshot CreateSnapshot(
        ProfilingSession session,
        ProfilingNode node,
        DateTimeOffset timestampUtc
    ) =>
        new()
        {
            Identity = ProfilingSnapshotIdentity.Create(),
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            TimestampUtc = timestampUtc,
            HostName = node.HostName,
            ProcessId = node.ProcessId,
            Sequence = 1,
            ScheduledElapsed = timestampUtc - session.StartedUtc,
            CaptureStartedElapsed = timestampUtc - session.StartedUtc,
            CaptureDuration = TimeSpan.FromMilliseconds(1),
        };

    private sealed class ProfilingStoreHarness(
        ServiceProvider firstProvider,
        ServiceProvider secondProvider
    ) : IAsyncDisposable
    {
        public IProfilingStore First { get; } = firstProvider.GetRequiredService<IProfilingStore>();

        public IProfilingStore Second { get; } =
            secondProvider.GetRequiredService<IProfilingStore>();

        public async Task AssertSnapshotCountAsync(int expected)
        {
            await using var scope = firstProvider.CreateAsyncScope();
            (
                await scope
                    .ServiceProvider.GetRequiredService<ProfilingIntegrationDbContext>()
                    .ProfilingSnapshots.CountAsync()
            ).ShouldBe(expected);
        }

        public async ValueTask DisposeAsync()
        {
            await using (var scope = firstProvider.CreateAsyncScope())
            {
                await scope
                    .ServiceProvider.GetRequiredService<ProfilingIntegrationDbContext>()
                    .Database.EnsureDeletedAsync();
            }

            await firstProvider.DisposeAsync();
            await secondProvider.DisposeAsync();
        }
    }

    protected sealed class ProfilingIntegrationDbContext(
        DbContextOptions<ProfilingIntegrationDbContext> options
    ) : DbContext(options), IProfilingContext
    {
        public DbSet<ProfilingSessionEntity> ProfilingSessions { get; set; }

        public DbSet<ProfilingInvalidSessionEntity> ProfilingInvalidSessions { get; set; }

        public DbSet<ProfilingNodeEntity> ProfilingNodes { get; set; }

        public DbSet<ProfilingParticipationEntity> ProfilingParticipations { get; set; }

        public DbSet<ProfilingSnapshotEntity> ProfilingSnapshots { get; set; }

        public DbSet<ProfilingMetricObservationEntity> ProfilingMetricObservations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureProfiling();
        }
    }
}
