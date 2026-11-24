// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Broadcasting;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies node registration readiness and once-only graceful shutdown.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~BroadcastNodeLifecycleServiceTests</code></example>
public class BroadcastNodeLifecycleServiceTests
{
    /// <summary>Startup delay leaves the host available and postpones registration until the configured time.</summary>
    [Fact]
    public async Task StartAsync_WithStartupDelay_DoesNotBlockHostAndRegistersAfterDelay()
    {
        // Arrange
        var timeProvider = new NotifyingTimeProvider();
        var registry = new RecordingRegistry();
        var sut = CreateService(
            new BroadcastingOptions
            {
                NodeIdentity = "node-a",
                StartupDelay = TimeSpan.FromMinutes(1),
                Scopes = { "Alpha" },
            },
            registry,
            timeProvider);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        registry.Upserted.Task.IsCompleted.ShouldBeFalse();

        await timeProvider.TimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        var registration = await registry.Upserted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        registration.NodeIdentity.ShouldBe("node-a");

        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>Database readiness postpones registration until the named application context is ready.</summary>
    [Fact]
    public async Task StartAsync_WithDatabaseReadiness_WaitsBeforeInitialRegistration()
    {
        // Arrange
        var readiness = new GatedDatabaseReadyService();
        var registry = new RecordingRegistry();
        var sut = CreateService(
            new BroadcastingOptions
            {
                NodeIdentity = "node-a",
                WaitForDatabaseReady = true,
                DatabaseReadyName = "AppDbContext",
                Scopes = { "Alpha" },
            },
            registry,
            TimeProvider.System,
            readiness);

        // Act
        await sut.StartAsync(CancellationToken.None);
        var requestedName = await readiness.WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        requestedName.ShouldBe("AppDbContext");
        registry.Upserted.Task.IsCompleted.ShouldBeFalse();

        readiness.SetReady();
        await registry.Upserted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>An omitted readiness service does not prevent registration.</summary>
    [Fact]
    public async Task StartAsync_WithoutDatabaseReadyService_RegistersWithoutWaiting()
    {
        // Arrange
        var registry = new RecordingRegistry();
        var sut = CreateService(
            new BroadcastingOptions
            {
                NodeIdentity = "node-a",
                WaitForDatabaseReady = true,
                DatabaseReadyName = "AppDbContext",
                Scopes = { "Alpha" },
            },
            registry,
            TimeProvider.System);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        await registry.Upserted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>Concurrent and post-disposal stop requests wait for one registry removal without accessing disposed dependencies again.</summary>
    [Fact]
    public async Task StopAsync_ConcurrentAndRepeatedCalls_UnregistersOnce()
    {
        // Arrange
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var removing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = Substitute.For<IBroadcastRegistryStore>();
        registry.Capabilities.Returns(new BroadcastRegistryCapabilities(false, false));
        registry.UpsertAsync(Arg.Any<BroadcastNodeRegistrationRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            registered.TrySetResult();
            return Task.CompletedTask;
        });
        registry.RemoveAsync("node-a", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            removing.TrySetResult();
            return release.Task;
        });
        using var sut = CreateService(new BroadcastingOptions
        {
            NodeIdentity = "node-a", StartupDelay = TimeSpan.Zero, Scopes = { "Alpha" }
        }, registry, TimeProvider.System);
        await sut.StartAsync(CancellationToken.None);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        var first = sut.StopAsync(CancellationToken.None);
        await removing.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = sut.StopAsync(CancellationToken.None);
        try
        {
            first.IsCompleted.ShouldBeFalse();
            second.IsCompleted.ShouldBeFalse();
        }
        finally { release.TrySetResult(); }

        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        sut.Dispose();
        await sut.StopAsync(CancellationToken.None);

        // Assert
        await registry.Received(1).RemoveAsync("node-a", Arg.Any<CancellationToken>());
    }

    private static BroadcastNodeLifecycleService CreateService(
        BroadcastingOptions options,
        IBroadcastRegistryStore registry,
        TimeProvider timeProvider,
        IDatabaseReadyService databaseReadyService = null) =>
        new(
            options,
            new DefaultBroadcastNodeIdentityProvider(options),
            registry,
            [],
            new StartedApplicationLifetime(),
            timeProvider,
            logger: NullLogger<BroadcastNodeLifecycleService>.Instance,
            databaseReadyService: databaseReadyService);

    private sealed class StartedApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource started = CreateStartedSource();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => this.started.Token;

        public CancellationToken ApplicationStopping => this.stopping.Token;

        public CancellationToken ApplicationStopped => this.stopped.Token;

        public void StopApplication()
        {
            this.stopping.Cancel();
            this.stopped.Cancel();
        }

        private static CancellationTokenSource CreateStartedSource()
        {
            var source = new CancellationTokenSource();
            source.Cancel();
            return source;
        }
    }

    private sealed class NotifyingTimeProvider : TimeProvider
    {
        private readonly FakeTimeProvider inner = new();

        public TaskCompletionSource TimerCreated { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public override DateTimeOffset GetUtcNow() => this.inner.GetUtcNow();

        public override long GetTimestamp() => this.inner.GetTimestamp();

        public override TimeZoneInfo LocalTimeZone => this.inner.LocalTimeZone;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = this.inner.CreateTimer(callback, state, dueTime, period);
            this.TimerCreated.TrySetResult();
            return timer;
        }

        public void Advance(TimeSpan delta) => this.inner.Advance(delta);
    }

    private sealed class GatedDatabaseReadyService : IDatabaseReadyService
    {
        private readonly TaskCompletionSource ready = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<string> WaitStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsReady(string name = null) => this.ready.Task.IsCompletedSuccessfully;

        public bool IsFaulted(string name = null) => false;

        public string FaultMessage(string name = null) => null;

        public void SetReady(string name = null) => this.ready.TrySetResult();

        public void SetFaulted(string name = null, string message = null) =>
            throw new NotSupportedException();

        public async Task WaitForReadyAsync(
            string name = null,
            TimeSpan? pollInterval = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            this.WaitStarted.TrySetResult(name);
            await this.ready.Task.WaitAsync(cancellationToken);
        }

        public Task<TResult> OnReadyAsync<TResult>(
            Func<Task<TResult>> onReady,
            Func<Task<TResult>> onFaulted = null,
            string name = null,
            TimeSpan? pollInterval = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TResult> OnReadyAsync<TResult>(
            Func<TResult> onReady,
            Func<TResult> onFaulted = null,
            string name = null,
            TimeSpan? pollInterval = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task OnReadyAsync(
            Action onReady,
            Action onFaulted = null,
            string name = null,
            TimeSpan? pollInterval = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRegistry : IBroadcastRegistryStore
    {
        public TaskCompletionSource<BroadcastNodeRegistrationRequest> Upserted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public BroadcastRegistryCapabilities Capabilities { get; } = new(false, false);

        public Task UpsertAsync(
            BroadcastNodeRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            this.Upserted.TrySetResult(request);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<BroadcastNodeRegistration>> GetActiveAsync(
            IReadOnlyCollection<string> scopes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BroadcastNodeRegistration>>([]);

        public Task<BroadcastNodeRegistration> FindAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BroadcastNodeRegistration>(null);

        public Task RecordDeliveryAsync(
            string nodeIdentity,
            bool succeeded,
            string failure,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RenewLeaseAsync(
            string nodeIdentity,
            DateTimeOffset leaseExpiresUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ExpireLeasesAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<BroadcastNodeRegistration>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BroadcastNodeRegistration>>([]);
    }
}