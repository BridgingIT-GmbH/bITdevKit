// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Verifies optional database readiness for Runtime node metadata registration.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~RuntimeProfilingNodeRegistrationHostedServiceTests</code></example>
public class RuntimeProfilingNodeRegistrationHostedServiceTests
{
    /// <summary>Verifies DI registration waits for the configured database before querying the registry.</summary>
    [Fact]
    public async Task StartAsync_WithDatabaseReadiness_DoesNotQueryUntilReady()
    {
        // Arrange
        var waitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readiness = Substitute.For<IDatabaseReadyService>();
        readiness.WaitForReadyAsync("CoreDbContext", timeout: TimeSpan.FromMinutes(2), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                waitStarted.TrySetResult();
                return ready.Task.WaitAsync(call.Arg<CancellationToken>());
            });
        var (registry, adapter, identity, registered) = CreateRegistration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostApplicationLifetime>());
        services.AddBroadcasting(options => options.DatabaseReadiness("CoreDbContext"));
        services.AddProfiling(options => options.Enabled()).WithRuntimeProfiling();
        services.AddSingleton(readiness);
        services.AddSingleton(registry);
        services.AddSingleton(adapter);
        services.AddSingleton(identity);
        await using var provider = services.BuildServiceProvider();
        using var sut = provider.GetServices<IHostedService>().OfType<RuntimeProfilingNodeRegistrationHostedService>().Single();

        // Act
        await sut.StartAsync(CancellationToken.None);
        await waitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await registry.DidNotReceiveWithAnyArgs().FindAsync(default);
        registered.Task.IsCompleted.ShouldBeFalse();
        ready.SetResult();
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));
        await registry.Received(1).FindAsync("node-a", Arg.Any<CancellationToken>());
        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>Verifies readiness faults and timeouts skip metadata registration without failing the host.</summary>
    /// <param name="faulted">Whether readiness fails with a database fault rather than a timeout.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_ReadinessFaultOrTimeout_SkipsQueriesWithoutFailingHost(bool faulted)
    {
        // Arrange
        var readiness = Substitute.For<IDatabaseReadyService>();
        readiness.WaitForReadyAsync("CoreDbContext", timeout: TimeSpan.FromMinutes(2), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException(faulted
                ? new InvalidOperationException("Database creation failed.")
                : new TimeoutException("Database not ready.")));
        var (registry, adapter, identity, _) = CreateRegistration();
        using var sut = new RuntimeProfilingNodeRegistrationHostedService(
            adapter, registry, identity, broadcastingOptions: ReadyOptions(), databaseReadyService: readiness);

        // Act
        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        sut.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
        await registry.DidNotReceiveWithAnyArgs().FindAsync(default);
        await adapter.DidNotReceiveWithAnyArgs().RegisterLocalAsync(default);
        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>Verifies shutdown cancels readiness waiting without accessing the registry.</summary>
    [Fact]
    public async Task StopAsync_WhileWaitingForDatabase_CancelsWithoutQueryingRegistry()
    {
        // Arrange
        var waitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readiness = Substitute.For<IDatabaseReadyService>();
        readiness.WaitForReadyAsync("CoreDbContext", timeout: TimeSpan.FromMinutes(2), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                waitStarted.TrySetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
            });
        var (registry, adapter, identity, _) = CreateRegistration();
        using var sut = new RuntimeProfilingNodeRegistrationHostedService(
            adapter, registry, identity, broadcastingOptions: ReadyOptions(), databaseReadyService: readiness);
        await sut.StartAsync(CancellationToken.None);
        await waitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        sut.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
        await registry.DidNotReceiveWithAnyArgs().FindAsync(default);
    }

    /// <summary>Verifies registration remains available when the optional readiness service is absent.</summary>
    [Fact]
    public async Task StartAsync_WithoutReadinessService_RegistersWithoutWaiting()
    {
        // Arrange
        var (registry, adapter, identity, registered) = CreateRegistration();
        using var sut = new RuntimeProfilingNodeRegistrationHostedService(
            adapter, registry, identity, broadcastingOptions: ReadyOptions());

        // Act
        await sut.StartAsync(CancellationToken.None);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await registry.Received(1).FindAsync("node-a", Arg.Any<CancellationToken>());
        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>Verifies explicitly disabled readiness does not delay registration.</summary>
    [Fact]
    public async Task StartAsync_ReadinessDisabled_RegistersWithoutReadinessCalls()
    {
        // Arrange
        var readiness = Substitute.For<IDatabaseReadyService>();
        var (registry, adapter, identity, registered) = CreateRegistration();
        using var sut = new RuntimeProfilingNodeRegistrationHostedService(
            adapter, registry, identity, broadcastingOptions: new BroadcastingOptions(), databaseReadyService: readiness);

        // Act
        await sut.StartAsync(CancellationToken.None);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await readiness.DidNotReceiveWithAnyArgs().WaitForReadyAsync();
        await registry.Received(1).FindAsync("node-a", Arg.Any<CancellationToken>());
        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>Verifies disabled Broadcasting skips both readiness and registry access.</summary>
    [Fact]
    public async Task StartAsync_BroadcastingDisabled_DoesNotQueryRegistry()
    {
        // Arrange
        var readiness = Substitute.For<IDatabaseReadyService>();
        var (registry, adapter, identity, _) = CreateRegistration();
        using var sut = new RuntimeProfilingNodeRegistrationHostedService(
            adapter, registry, identity, broadcastingOptions: new BroadcastingOptions { Enabled = false }, databaseReadyService: readiness);

        // Act
        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await readiness.DidNotReceiveWithAnyArgs().WaitForReadyAsync();
        await registry.DidNotReceiveWithAnyArgs().FindAsync(default);
        await sut.StopAsync(CancellationToken.None);
    }

    private static BroadcastingOptions ReadyOptions() => new()
    {
        WaitForDatabaseReady = true,
        DatabaseReadyName = "CoreDbContext",
        DatabaseReadyTimeout = TimeSpan.FromMinutes(2)
    };

    private static (IBroadcastRegistryStore Registry, IRuntimeProfilingNodeRegistrationAdapter Adapter,
        IBroadcastNodeIdentityProvider Identity, TaskCompletionSource Registered) CreateRegistration()
    {
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = new BroadcastNodeRegistration { NodeIdentity = "node-a", ProcessStartedUtc = DateTimeOffset.UtcNow };
        var registry = Substitute.For<IBroadcastRegistryStore>();
        registry.FindAsync("node-a", Arg.Any<CancellationToken>()).Returns(registration);
        var adapter = Substitute.For<IRuntimeProfilingNodeRegistrationAdapter>();
        adapter.RegisterLocalAsync(registration, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            registered.TrySetResult();
            return Task.FromResult<IResult<ProfilingNode>>(Result<ProfilingNode>.Success(new ProfilingNode()));
        });
        var identity = Substitute.For<IBroadcastNodeIdentityProvider>();
        identity.GetNodeIdentity().Returns("node-a");
        return (registry, adapter, identity, registered);
    }
}
