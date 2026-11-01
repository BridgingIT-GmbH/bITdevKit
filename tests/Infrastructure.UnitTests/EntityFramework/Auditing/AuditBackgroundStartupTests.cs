// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Auditing;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies the shared host-start, delay and database-readiness sequence without real time or database access.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~AuditBackgroundStartupTests</code></example>
public sealed class AuditBackgroundStartupTests
{
    [Fact]
    public async Task WaitAsync_StartupDelayThenDatabaseReady_PreservesOrdering()
    {
        using var started = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Returns(started.Token);
        var time = new StartupClock();
        var readiness = Substitute.For<IDatabaseReadyService>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        readiness.WaitForReadyAsync(Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(call => { entered.TrySetResult(); return ready.Task.WaitAsync(call.ArgAt<CancellationToken>(3)); });
        var options = new AuditEntityFrameworkOptions { StartupDelay = TimeSpan.FromSeconds(2) };

        var waiting = AuditBackgroundStartup.WaitAsync<object>(options, lifetime, readiness, time, CancellationToken.None);
        time.TimerCreated.Task.IsCompleted.ShouldBeFalse();
        started.Cancel();
        await time.TimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        entered.Task.IsCompleted.ShouldBeFalse();
        time.Advance(options.StartupDelay);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        waiting.IsCompleted.ShouldBeFalse();
        ready.TrySetResult();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await readiness.Received(1).WaitForReadyAsync(nameof(Object), timeout: options.DatabaseReadyTimeout, cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task WaitAsync_HostStopsBeforeStartup_CancelsWithoutDatabaseWait()
    {
        using var stopping = new CancellationTokenSource();
        using var started = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Returns(started.Token);
        var readiness = Substitute.For<IDatabaseReadyService>();
        var waiting = AuditBackgroundStartup.WaitAsync<object>(new(), lifetime, readiness, TimeProvider.System, stopping.Token);
        stopping.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => waiting);
        readiness.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task WaitAsync_OptionalCoordinationAbsent_CompletesWithoutDatabaseDependencies()
        => await AuditBackgroundStartup.WaitAsync<object>(new(), null, null, TimeProvider.System, CancellationToken.None);

    [Fact]
    public async Task WaitAsync_DatabaseFault_PropagatesWithoutIgnoringReadiness()
    {
        var readiness = Substitute.For<IDatabaseReadyService>();
        readiness.WaitForReadyAsync(Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Database fault")));
        await Should.ThrowAsync<InvalidOperationException>(() => AuditBackgroundStartup.WaitAsync<object>(new(), null, readiness, TimeProvider.System, CancellationToken.None));
    }

    /// <summary>Exposes timer registration so the test advances virtual time only after the delay is scheduled.</summary>
    /// <example><code>await time.TimerCreated.Task; time.Advance(TimeSpan.FromSeconds(2));</code></example>
    public sealed class StartupClock : FakeTimeProvider
    {
        /// <summary>Completes after the startup delay timer has been registered.</summary>
        /// <example><code>await time.TimerCreated.Task;</code></example>
        public TaskCompletionSource TimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            this.TimerCreated.TrySetResult();
            return timer;
        }
    }
}
