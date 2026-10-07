// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
[Collection(nameof(IsolatedSqliteTestEnvironmentCollection))]
public sealed class EntityFrameworkProfilingLifecycleTests
{
    [Fact]
    public async Task ApplyRetentionAsync_DurableStore_PreservesPinnedAndNewestTerminalSessions()
    {
        // Arrange
        await using var harness = await ProfilingLifecycleHarness.CreateAsync();
        var now = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        var oldest = await CreateTerminalSessionAsync(harness.Store, "oldest", now.AddHours(-3));
        var newest = await CreateTerminalSessionAsync(harness.Store, "newest", now.AddHours(-2));
        var pinned = await CreateTerminalSessionAsync(harness.Store, "pinned", now.AddHours(-4));
        await harness.Store.UpdateSessionMetadataAsync(
            pinned.Identity.Key,
            new(pinned.Name, pinned.Tags, pinned.Note, true)
        );

        // Act
        var result = await harness.Store.ApplyRetentionAsync(
            1,
            TimeSpan.FromDays(30),
            now
        );
        var remaining = await harness.Store.ListSessionsAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
        remaining.IsSuccess.ShouldBeTrue();
        remaining.Value.Select(session => session.Identity.Key).ShouldBe(
            [newest.Identity.Key, pinned.Identity.Key],
            ignoreOrder: true
        );
        remaining.Value.ShouldNotContain(session => session.Identity.Key == oldest.Identity.Key);
    }

    [Fact]
    public async Task ReconcileAsync_OverdueDurableSession_FinalizesWithWarningsOnlyOnce()
    {
        // Arrange
        await using var harness = await ProfilingLifecycleHarness.CreateAsync();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        var session = (
            await harness.Store.GetOrCreateActiveSessionAsync(
                new(
                    ProfilingIdentityFactory.CreateRuntimeSession(),
                    "abandoned",
                    startedUtc,
                    RuntimeProfilingOptions.MinimumSamplingInterval,
                    TimeSpan.FromSeconds(1),
                    []
                )
            )
        ).Value.Session;
        var correlation = new RuntimeProfilingNodeCorrelation("node-a", startedUtc);
        var node = (
            await harness.Store.GetOrCreateNodeAsync(
                correlation,
                new()
                {
                    Identity = ProfilingIdentityFactory.CreateNode(),
                    Correlation = correlation,
                    HostName = "localhost",
                    ProcessId = 1234,
                }
            )
        ).Value;
        await harness.Store.UpsertParticipationAsync(
            new()
            {
                SessionId = session.Identity.Id,
                SessionKey = session.Identity.Key,
                NodeId = node.Identity.Id,
                NodeKey = node.Identity.Key,
                Role = RuntimeProfilingNodeRole.ExpectedParticipant,
                State = RuntimeProfilingParticipationState.Accepted,
                JoinedUtc = startedUtc,
            }
        );
        var options = new ProfilingOptions { Enabled = true, Runtime = new() { Enabled = true, FinalizationGracePeriod = TimeSpan.Zero } };
        var timeProvider = new FixedTimeProvider(startedUtc.AddSeconds(2));
        var finalizer = new RuntimeProfilingSessionFinalizer(harness.Store, options, timeProvider);
        var reconciler = new RuntimeProfilingStartupReconciler(
            harness.Store,
            options,
            timeProvider,
            finalizer
        );

        // Act
        var first = await reconciler.ReconcileAsync();
        var second = await reconciler.ReconcileAsync();
        var finalized = await harness.Store.FindSessionAsync(session.Identity.Key);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        first.Value.ShouldBe(1);
        second.IsSuccess.ShouldBeTrue();
        second.Value.ShouldBe(0);
        finalized.IsSuccess.ShouldBeTrue();
        finalized.Value.State.ShouldBe(RuntimeProfilingSessionState.CompletedWithWarnings);
        finalized.Value.CompletedUtc.ShouldBe(timeProvider.GetUtcNow());
    }

    private static async Task<RuntimeProfilingSession> CreateTerminalSessionAsync(
        IRuntimeProfilingStore store,
        string name,
        DateTimeOffset startedUtc
    )
    {
        var session = (
            await store.GetOrCreateActiveSessionAsync(
                new(
                    ProfilingIdentityFactory.CreateRuntimeSession(),
                    name,
                    startedUtc,
                    RuntimeProfilingOptions.MinimumSamplingInterval,
                    TimeSpan.FromSeconds(1),
                    []
                )
            )
        ).Value.Session;
        return (
            await store.TryTransitionSessionAsync(
                session.Identity.Id,
                [RuntimeProfilingSessionState.Running],
                RuntimeProfilingSessionState.Completed,
                startedUtc.AddSeconds(1)
            )
        ).Value;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ProfilingLifecycleHarness(
        ServiceProvider provider,
        string databasePath
    ) : IAsyncDisposable
    {
        public IRuntimeProfilingStore Store { get; } = provider.GetRequiredService<IRuntimeProfilingStore>();

        public static async Task<ProfilingLifecycleHarness> CreateAsync()
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"profiling-lifecycle-{Guid.NewGuid():N}.db"
            );
            var services = new ServiceCollection();
            services.AddDbContext<ProfilingLifecycleDbContext>(options =>
                options.UseSqlite($"Data Source={databasePath}")
            );
            services
                .AddProfiling(options => options.Enabled()).WithRuntimeProfiling()
                .WithEntityFrameworkProvider<ProfilingLifecycleDbContext>();
            var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true }
            );
            await using (var scope = provider.CreateAsyncScope())
            {
                await scope
                    .ServiceProvider.GetRequiredService<ProfilingLifecycleDbContext>()
                    .Database.EnsureCreatedAsync();
            }

            return new(provider, databasePath);
        }

        public async ValueTask DisposeAsync()
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                await scope
                    .ServiceProvider.GetRequiredService<ProfilingLifecycleDbContext>()
                    .Database.EnsureDeletedAsync();
            }

            await provider.DisposeAsync();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private sealed class ProfilingLifecycleDbContext(
        DbContextOptions<ProfilingLifecycleDbContext> options
    ) : DbContext(options), IProfilingDbContext
    {
        public DbSet<RuntimeProfilingSessionEntity> ProfilingSessions { get; set; }

        public DbSet<RuntimeProfilingInvalidSessionEntity> ProfilingInvalidSessions { get; set; }

        public DbSet<ProfilingNodeEntity> ProfilingNodes { get; set; }

        public DbSet<RuntimeProfilingParticipationEntity> ProfilingParticipations { get; set; }

        public DbSet<RuntimeProfilingSnapshotEntity> ProfilingSnapshots { get; set; }

        public DbSet<RuntimeProfilingMetricObservationEntity> ProfilingMetricObservations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureProfiling();
        }
    }
}
