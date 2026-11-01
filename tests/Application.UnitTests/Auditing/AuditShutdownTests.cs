// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Checks shutdown closes new capture while allowing owned roots to finish.</summary>
/// <example><code>runtime.BeginShutdown(); await runtime.WaitForIdleAsync();</code></example>
public class AuditShutdownTests
{
    /// <summary>Skips new configuration callbacks even while an existing root remains current.</summary>
    /// <example><code>runtime.BeginShutdown();</code></example>
    [Fact]
    public async Task BeginShutdown_ExistingRoot_RejectsNewScopesWithoutConfigurationCallbacks()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = host.GetRequiredService<AuditRuntime>();
        var context = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = context.BeginScope(builder => builder.Key("orders.approve"));
        var callbacks = 0;

        sut.BeginShutdown();
        await using (var rejected = context.BeginScope(builder => { callbacks++; builder.Key("orders.late"); }))
        {
            rejected.IsRecording.ShouldBeFalse();
        }

        using (services.ServiceProvider.GetRequiredService<IAuditExecutionBoundary>().DetachExecution())
        await using (var rejected = context.BeginScope(builder => { callbacks++; builder.Key("orders.independent"); }))
        {
            rejected.IsRecording.ShouldBeFalse();
        }

        callbacks.ShouldBe(0);
        context.Current.ShouldBeSameAs(root);
        root.Complete();
        await root.DisposeAsync();
        await sut.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var stored = await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId);
        stored.IsSuccess.ShouldBeTrue();
        stored.Value.Key.ShouldBe("orders.approve");
        stored.Value.Actions.ShouldBeEmpty();
    }

    /// <summary>Waits through real root disposal and sealing until provider admission completes.</summary>
    /// <example><code>await root.DisposeAsync();</code></example>
    [Fact]
    public async Task WaitForIdleAsync_RootFinalization_WaitsForSealingAndPersistsRealRecord()
    {
        var sealer = new GatedSealer();
        using var host = CreateHost(collection => collection.AddSingleton<IAuditRecordSealer>(sealer));
        using var services = host.CreateScope();
        var sut = host.GetRequiredService<AuditRuntime>();
        var context = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = context.BeginScope(builder => builder.Key("orders.approve"));
        root.SetDetail("order", AuditValue.From("O-123"));
        root.Complete();
        sut.BeginShutdown();
        var idle = sut.WaitForIdleAsync();
        idle.IsCompleted.ShouldBeFalse();
        var finalization = root.DisposeAsync().AsTask();

        try
        {
            await sealer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            finalization.IsCompleted.ShouldBeFalse();
            idle.IsCompleted.ShouldBeFalse();
            context.Current.ShouldBeNull();
            (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).IsFailure.ShouldBeTrue();
        }
        finally
        {
            sealer.Release.TrySetResult();
            await finalization.WaitAsync(TimeSpan.FromSeconds(10));
        }

        await idle.WaitAsync(TimeSpan.FromSeconds(10));
        var stored = await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId);
        stored.IsSuccess.ShouldBeTrue();
        stored.Value.Outcome.ShouldBe(AuditOutcome.Succeeded);
        stored.Value.Details["order"].Data.GetString().ShouldBe("O-123");
        stored.Value.ContentHash.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Canceled idle waits leave the owning root available for later finalization.</summary>
    /// <example><code>await runtime.WaitForIdleAsync(cancellationToken);</code></example>
    [Fact]
    public async Task WaitForIdleAsync_CanceledWait_DoesNotCancelRootFinalization()
    {
        using var host = CreateHost();
        using var services = host.CreateScope();
        var sut = host.GetRequiredService<AuditRuntime>();
        var context = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = context.BeginScope(builder => builder.Key("orders.approve"));
        sut.BeginShutdown();
        using var cancellation = new CancellationTokenSource();
        var idle = sut.WaitForIdleAsync(cancellation.Token);

        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => idle);
        root.Complete();
        await root.DisposeAsync();

        await sut.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Allows an expired owner to re-enter bounded finalization after shutdown begins.</summary>
    /// <example><code>await expiredRoot.DisposeAsync();</code></example>
    [Fact]
    public async Task DisposeAsync_ExpiredRootAfterShutdown_ReadmitsFinalizationAndWaitsForIdle()
    {
        var time = new FakeTimeProvider();
        var sealer = new GatedSealer();
        using var host = CreateHost(collection =>
        {
            collection.AddSingleton<TimeProvider>(time);
            collection.AddSingleton<IAuditRecordSealer>(sealer);
        }, options => options.Limits(limits => limits with { MaxLifetime = TimeSpan.FromSeconds(1) }));
        using var services = host.CreateScope();
        var sut = host.GetRequiredService<AuditRuntime>();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("orders.expired"));
        time.Advance(TimeSpan.FromSeconds(2));
        root.IsRecording.ShouldBeFalse();
        sut.BeginShutdown();
        var finalization = root.DisposeAsync().AsTask();

        try
        {
            await sealer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var idle = sut.WaitForIdleAsync();
            idle.IsCompleted.ShouldBeFalse();
            sealer.Release.SetResult();
            await finalization.WaitAsync(TimeSpan.FromSeconds(10));
            await idle.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            sealer.Release.TrySetResult();
            await finalization.WaitAsync(TimeSpan.FromSeconds(10));
        }

        var stored = await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId);
        stored.IsSuccess.ShouldBeTrue();
        stored.Value.Notes.ShouldContain(note => note.Code == "AuditCaptureExpired");
    }

    /// <summary>Preserves disabled and shutdown no-op business behavior without metadata evaluation.</summary>
    /// <example><code>await context.RunAsync(configure, action);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_DisabledOrStopping_ExecutesBusinessWorkOnceWithoutMetadata(bool disabled)
    {
        using var host = CreateHost(configure: options => options.Enabled(!disabled));
        using var services = host.CreateScope();
        var runtime = host.GetRequiredService<AuditRuntime>();
        var sut = services.ServiceProvider.GetRequiredService<IAuditContext>();
        if (!disabled) runtime.BeginShutdown();
        var callbacks = 0;
        var calls = 0;

        var result = await sut.RunAsync<int>(_ => { callbacks++; throw new InvalidOperationException("unused metadata"); }, (scope, _) =>
        {
            scope.IsRecording.ShouldBeFalse();
            return Task.FromResult(++calls);
        });

        result.ShouldBe(1);
        callbacks.ShouldBe(0);
        calls.ShouldBe(1);
        sut.Current.ShouldBeNull();
        await runtime.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        runtime.SnapshotFailures().ShouldBeEmpty();
    }

    /// <summary>Retains the root slot until late external work actually ends, including after runtime disposal.</summary>
    /// <example><code>runtime.CanStart.ShouldBeFalse();</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeAsync_UncooperativeSealer_KeepsBoundedOwnershipAfterCallerTimeout(bool fails)
    {
        var time = new FakeTimeProvider();
        var sealer = new GatedSealer(ignoreCancellation: true, fails);
        using var host = CreateHost(collection =>
        {
            collection.AddSingleton<TimeProvider>(time);
            collection.AddSingleton<IAuditRecordSealer>(sealer);
        }, options => options.Configure(value => value with { FinalizationTimeout = TimeSpan.FromSeconds(1) })
            .Limits(limits => limits with { MaxActiveRoots = 1 }));
        using var services = host.CreateScope();
        var runtime = host.GetRequiredService<AuditRuntime>();
        var context = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = context.BeginScope(builder => builder.Key("orders.delayed"));
        root.Complete();
        var finalization = root.DisposeAsync().AsTask();
        await sealer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var idle = runtime.WaitForIdleAsync();

        try
        {
            time.Advance(TimeSpan.FromSeconds(2));
            await finalization.WaitAsync(TimeSpan.FromSeconds(10));
            idle.IsCompleted.ShouldBeFalse();
            runtime.CanStart.ShouldBeFalse();
            await using (var rejected = context.BeginScope(builder => builder.Key("orders.over-capacity"))) rejected.IsRecording.ShouldBeFalse();
            runtime.Dispose();
            idle.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            sealer.Release.TrySetResult();
            await finalization.WaitAsync(TimeSpan.FromSeconds(10));
            await idle.WaitAsync(TimeSpan.FromSeconds(10));
        }

        (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).IsFailure.ShouldBeTrue();
        runtime.SnapshotFailures()["AuditFinalizationFailed"].ShouldBe(1);
    }

    private static ServiceProvider CreateHost(Action<IServiceCollection> services = null, Action<AuditingOptionsBuilder> configure = null)
    {
        var collection = new ServiceCollection();
        services?.Invoke(collection);
        collection.AddAuditing(configure);
        return collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private sealed class GatedSealer(bool ignoreCancellation = false, bool fails = false) : IAuditRecordSealer
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Holds real finalization until the test releases its explicit barrier.</summary>
        /// <example><code>await sealer.SealAsync(record, cancellationToken);</code></example>
        public async Task<Result<AuditRecord>> SealAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            this.Entered.SetResult();
            await this.Release.Task.WaitAsync(ignoreCancellation ? CancellationToken.None : cancellationToken);
            if (fails) throw new InvalidOperationException("A safe test-only late failure.");
            return Result<AuditRecord>.Success(record);
        }
    }
}
