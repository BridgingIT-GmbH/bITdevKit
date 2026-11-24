// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

/// <summary>Checks consecutive infrastructure failures and nonmutating single-probe recovery.</summary>
public sealed class CacheCircuitBreakerTests
{
    [Fact]
    public async Task ConsecutiveFailures_NonInfrastructureResultResetsThreshold()
    {
        var native = Native();
        await using var sut = new CircuitBreakingCacheStoreProviderBehavior(native, new() { Enabled = true, FailureThreshold = 2 });
        await sut.InitializeAsync([]);
        native.GetQuotaAsync().Returns(ValueTask.FromResult(Failure<CacheProviderQuota>(CacheErrorCode.ProviderUnavailable)));
        (await sut.GetQuotaAsync()).IsFailure.ShouldBeTrue();
        sut.Snapshot().ConsecutiveFailures.ShouldBe(1);
        native.GetQuotaAsync().Returns(ValueTask.FromResult(Failure<CacheProviderQuota>(CacheErrorCode.Capacity)));
        await sut.GetQuotaAsync();
        sut.Snapshot().ConsecutiveFailures.ShouldBe(0);
        native.GetQuotaAsync().Returns(ValueTask.FromResult(Failure<CacheProviderQuota>(CacheErrorCode.ProviderUnavailable)));
        await sut.GetQuotaAsync();
        sut.Snapshot().State.ShouldBe("Closed");
        await sut.GetQuotaAsync();
        sut.Snapshot().State.ShouldBe("Open");
        native.ClearReceivedCalls();

        (await sut.GetQuotaAsync()).IsFailure.ShouldBeTrue();

        await native.DidNotReceive().GetQuotaAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cooldown_ConcurrentCalls_AdmitsOneQuotaProbeAndNoMutationReplay()
    {
        var clock = new Clock();
        var native = Native();
        await using var sut = new CircuitBreakingCacheStoreProviderBehavior(native, new() { Enabled = true, FailureThreshold = 1 }, clock);
        await sut.InitializeAsync([]);
        native.GetQuotaAsync().Returns(ValueTask.FromResult(Failure<CacheProviderQuota>(CacheErrorCode.ProviderUnavailable)));
        await sut.GetQuotaAsync();
        clock.Advance(TimeSpan.FromSeconds(11));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<Result<CacheProviderQuota>>(TaskCreationOptions.RunContinuationsAsynchronously);
        native.GetQuotaAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            entered.TrySetResult();
            return new ValueTask<Result<CacheProviderQuota>>(release.Task);
        });
        var identity = new CacheEntryIdentity("app", "items", "1", "a");
        native.RemoveAsync(identity, default(CacheWriteCondition), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<CacheInvalidationOutcome>.Success(new() { Completed = true, AffectedCount = 0 })));
        var recovery = sut.RemoveAsync(identity).AsTask();
        await entered.Task;

        var rejected = await sut.RemoveAsync(identity);

        rejected.IsFailure.ShouldBeTrue();
        sut.Snapshot().State.ShouldBe("HalfOpen");
        await native.DidNotReceive().RemoveAsync(identity, default(CacheWriteCondition), Arg.Any<CancellationToken>());
        release.SetResult(Result<CacheProviderQuota>.Success(new(0, 0, 0, 0, 0, 1024, true)));
        (await recovery).IsSuccess.ShouldBeTrue();
        await native.Received(1).RemoveAsync(identity, default(CacheWriteCondition), Arg.Any<CancellationToken>());
        sut.Snapshot().Probes.ShouldBe(1);
        sut.Snapshot().State.ShouldBe("Closed");
    }

    [Fact]
    public async Task Initialization_FirstReadinessFailure_RemainsExplicitAndRetryable()
    {
        var native = Native();
        native.InitializeAsync(Arg.Any<IReadOnlyList<CacheProviderCollection>>(), Arg.Any<CancellationToken>()).Returns(
            ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "offline"))), ValueTask.FromResult(Result.Success()));
        await using var sut = new CircuitBreakingCacheStoreProviderBehavior(native, new() { Enabled = true, FailureThreshold = 1 });

        (await sut.InitializeAsync([])).IsFailure.ShouldBeTrue();
        sut.Snapshot().State.ShouldBe("Closed");
        (await sut.InitializeAsync([])).IsSuccess.ShouldBeTrue();
        native.GetQuotaAsync().Returns(ValueTask.FromResult(Failure<CacheProviderQuota>(CacheErrorCode.ProviderUnavailable)));
        await sut.GetQuotaAsync();
        sut.Snapshot().State.ShouldBe("Open");
    }

    [Theory]
    [InlineData(CacheErrorCode.SourceFailure)]
    [InlineData(CacheErrorCode.SourceRejected)]
    [InlineData(CacheErrorCode.Capacity)]
    [InlineData(CacheErrorCode.Conflict)]
    [InlineData(CacheErrorCode.Corruption)]
    [InlineData(CacheErrorCode.Validation)]
    public async Task Classification_NonInfrastructureFailure_DoesNotOpen(CacheErrorCode code)
    {
        var native = Native();
        await using var sut = new CircuitBreakingCacheStoreProviderBehavior(native, new() { Enabled = true, FailureThreshold = 1 });
        await sut.InitializeAsync([]);
        native.GetQuotaAsync().Returns(ValueTask.FromResult(Failure<CacheProviderQuota>(code)));

        (await sut.GetQuotaAsync()).IsFailure.ShouldBeTrue();

        sut.Snapshot().State.ShouldBe("Closed");
    }

    [Fact]
    public async Task Cancellation_CallerAlreadyCancelled_DoesNotInvokeNativeOrOpen()
    {
        var native = Native();
        await using var sut = new CircuitBreakingCacheStoreProviderBehavior(native, new() { Enabled = true, FailureThreshold = 1 });
        await sut.InitializeAsync([]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.GetQuotaAsync(cancellation.Token).AsTask());

        await native.DidNotReceive().GetQuotaAsync(Arg.Any<CancellationToken>());
        sut.Snapshot().State.ShouldBe("Closed");
    }

    [Fact]
    public async Task Timeout_StaleCircuitEpoch_CannotOpenRecoveredCircuit()
    {
        var clock = new Clock();
        var native = Native();
        await using var sut = new CircuitBreakingCacheStoreProviderBehavior(native, new() { Enabled = true, FailureThreshold = 1 }, clock);
        await sut.InitializeAsync([]);
        var previous = sut.CircuitEpoch;
        await sut.ReportInfrastructureTimeoutAsync(previous);
        clock.Advance(TimeSpan.FromSeconds(11));
        native.GetQuotaAsync().Returns(ValueTask.FromResult(Result<CacheProviderQuota>.Success(new(0, 0, 0, 0, 0, 1024, true))));
        (await sut.GetQuotaAsync()).IsSuccess.ShouldBeTrue();

        await sut.ReportInfrastructureTimeoutAsync(previous);

        sut.Snapshot().State.ShouldBe("Closed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeStream_LateReadFailure_TripsInfrastructureOnly(bool corruption)
    {
        var native = Native();
        await using var sut = new CircuitBreakingCacheStoreProviderBehavior(native, new() { Enabled = true, FailureThreshold = 1 });
        await sut.InitializeAsync([]);
        var source = new FaultingStream(corruption);
        var stream = new CacheProviderIoStream(source, null, TimeSpan.FromSeconds(5), infrastructureObserver: sut);

        var exception = await Assert.ThrowsAsync<CacheTransferException>(() => stream.ReadAsync(new byte[1]).AsTask());

        exception.Code.ShouldBe(corruption ? CacheErrorCode.Corruption : CacheErrorCode.ProviderUnavailable);
        sut.Snapshot().State.ShouldBe(corruption ? "Closed" : "Open");
        source.Reads.ShouldBe(1);
    }

    [Fact]
    public async Task NativeStream_SuccessfulRead_ResetsConsecutiveFailures()
    {
        var native = Native();
        await using var sut = new CircuitBreakingCacheStoreProviderBehavior(native, new() { Enabled = true, FailureThreshold = 2 });
        await sut.InitializeAsync([]);
        await sut.ReportInfrastructureTimeoutAsync(sut.CircuitEpoch);
        using var source = new MemoryStream([1]);
        var stream = new CacheProviderIoStream(source, null, TimeSpan.FromSeconds(5), infrastructureObserver: sut);

        (await stream.ReadAsync(new byte[1])).ShouldBe(1);

        sut.Snapshot().ConsecutiveFailures.ShouldBe(0);
        sut.Snapshot().State.ShouldBe("Closed");
    }

    [Fact]
    public async Task Session_PublicationInfrastructureFailure_CountsOnceWithoutReplay()
    {
        var native = Native();
        var session = Substitute.For<ICacheProviderWriteSession>();
        using var destination = new MemoryStream();
        session.Stream.Returns(destination);
        session.Version.Returns(CacheEntryVersion.New());
        session.OperationId.Returns("test-operation");
        session.PublishAsync(Arg.Any<CacheEntryMetadata>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Failure<CacheWriteOutcome>(CacheErrorCode.ProviderUnavailable)));
        native.BeginWriteAsync(Arg.Any<CacheProviderWriteRequest>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result<ICacheProviderWriteSession>.Success(session)));
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithProviderKind("relational", native.Capabilities, (_, _, _) => native)
            .WithProvider("database", provider => provider.With(x => x.Kind, "relational").With(x => x.Breaker.Enabled, true).With(x => x.Breaker.FailureThreshold, 2))
            .WithValueContract<string>("text").WithCollection<string>("items", collection => collection.With(x => x.Provider, "database"));
        await using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<CacheCatalog>();
        var binding = catalog.Collections["items"];
        var resources = provider.GetRequiredService<CacheRuntimeResources>();
        var guarded = (CircuitBreakingCacheStoreProviderBehavior)provider.GetRequiredService<CacheProviderRegistry>().Get("database");
        var access = new CacheProviderAccess(binding, guarded, resources, new(catalog), provider.GetRequiredService<CacheRuntimeTasks>(), TimeProvider.System);
        var started = await CacheWriteSessionAccess.BeginAsync(access, new(new("app", "items", "1", "key"), default, default, 1024),
            binding, resources.ForCollection("items"), provider.GetRequiredService<CacheRuntimeTasks>(), default);
        await using var sut = started.Value;

        var result = await sut.PublishAsync(new());

        result.IsFailure.ShouldBeTrue();
        guarded.Snapshot().ConsecutiveFailures.ShouldBe(1);
        guarded.Snapshot().State.ShouldBe("Closed");
        await session.Received(1).PublishAsync(Arg.Any<CacheEntryMetadata>(), Arg.Any<CancellationToken>());
        native.GetQuotaAsync().Returns(ValueTask.FromResult(Failure<CacheProviderQuota>(CacheErrorCode.ProviderUnavailable)));
        await guarded.GetQuotaAsync();
        guarded.Snapshot().State.ShouldBe("Open");
    }

    private sealed class FaultingStream(bool corruption) : MemoryStream
    {
        public int Reads { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.Reads++;
            throw corruption ? new InvalidDataException("Expected invalid frame.") : new IOException("Expected native outage.");
        }
    }

    private static ICacheStoreProvider Native()
    {
        var native = Substitute.For<ICacheStoreProvider>();
        native.Name.Returns("DATABASE");
        native.Capabilities.Returns(new CacheProviderCapabilities(true, true, true, false, true, false));
        native.InitializeAsync(Arg.Any<IReadOnlyList<CacheProviderCollection>>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Result.Success()));
        return native;
    }

    private static Result<T> Failure<T>(CacheErrorCode code) => Result<T>.Failure(new CacheError(code, "Expected test outcome."));

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan duration) => this.now += duration;
    }
}