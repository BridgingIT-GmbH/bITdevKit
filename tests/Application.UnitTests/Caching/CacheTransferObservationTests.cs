// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks acquisition and terminal source-integrity observation without destination coupling.</summary>
public sealed class CacheTransferObservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ReadOrEarlyDisposal_ObservesOneActualTerminalOutcome(bool abandon)
    {
        var services = new ServiceCollection();
        var probe = new Probe();
        services.AddSingleton(probe);
        services.AddApplicationCaching().WithBinaryContract("raw")
            .WithBehavior<ProbeBehavior>("observe", CacheBehaviorStage.CollectionOperations)
            .WithBinaryCollection("binary", options => options.With(x => x.Behaviors.CollectionOperations, new[] { "observe" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetBinaryCollection("binary");
        using var source = new MemoryStream([1, 2, 3, 4]);
        (await sut.SetAsync(new("key"), source)).IsSuccess.ShouldBeTrue();
        var lookup = await sut.OpenReadAsync("key");
        probe.Observations.ShouldBeEmpty();
        if (!abandon) { await lookup.Value.Value.Stream.CopyToAsync(Stream.Null); }

        await lookup.Value.Value.DisposeAsync();
        await lookup.Value.Value.DisposeAsync();

        probe.Observations.Count.ShouldBe(1);
        probe.Observations[0].BytesDelivered.ShouldBe(abandon ? 0 : 4);
        probe.Observations[0].Context.Collection.ShouldBe("BINARY");
        var snapshot = provider.GetRequiredService<CacheDiagnostics>().ForCollection("binary").Snapshot();
        snapshot.Counters[CacheObservationKind.TransferAcquired].ShouldBe(1);
        snapshot.Counters[abandon ? CacheObservationKind.TransferAbandoned : CacheObservationKind.TransferCompleted].ShouldBe(1);
    }

    [Fact]
    public async Task ObserverThrows_AfterVerifiedEof_DoesNotChangeReadCompletion()
    {
        var services = new ServiceCollection();
        var probe = new Probe { Throw = true };
        services.AddSingleton(probe);
        services.AddApplicationCaching().WithBinaryContract("raw")
            .WithBehavior<ProbeBehavior>("observe", CacheBehaviorStage.CollectionOperations)
            .WithBinaryCollection("binary", options => options.With(x => x.Behaviors.CollectionOperations, new[] { "observe" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetBinaryCollection("binary");
        using var source = new MemoryStream([1, 2, 3]);
        await sut.SetAsync(new("key"), source);
        var lookup = await sut.OpenReadAsync("key");
        await using var handle = lookup.Value.Value;

        await handle.Stream.CopyToAsync(Stream.Null);
        var completion = await handle.WaitForCompletionAsync();

        completion.IsSuccess.ShouldBeTrue();
        completion.Value.ShouldBe(CacheReadCompletion.Validated);
        probe.Observations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Cancellation_IgnoredNativeRead_RemainsOccupiedAfterTerminalObservation()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithBinaryContract("raw").WithBinaryCollection("binary");
        await using var provider = services.BuildServiceProvider();
        var resources = provider.GetRequiredService<CacheRuntimeResources>();
        var diagnostics = provider.GetRequiredService<CacheDiagnostics>();
        resources.ForCollection("binary").Transfers.TryAcquire(out var admission).ShouldBeTrue();
        resources.Transient.TryReserve(32, out var memory).ShouldBeTrue();
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new IgnoredRead();
        var metadata = new CacheEntryMetadata { Identity = new("app", "binary", "1", "key"), LogicalSizeBytes = 1 };
        await using var sut = new CacheReadStream(inner, metadata, new byte[16], memory,
            (_, _) => ValueTask.FromResult(Result.Success()), () =>
            {
                admission.Dispose();
                released.TrySetResult();
                return ValueTask.CompletedTask;
            }, provider.GetRequiredService<CacheRuntimeTasks>(), TimeProvider.System, new());
        sut.Observe(CacheTransferObservationSession.Start("BINARY", "MEMORY", false, diagnostics.ForCollection("binary"), TimeProvider.System, []));
        using var cancelled = new CancellationTokenSource();
        var read = sut.ReadAsync(new byte[1], cancelled.Token).AsTask();
        await inner.Entered.Task;
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);

        (await sut.WaitForCompletionAsync(default)).IsFailure.ShouldBeTrue();
        resources.Transfers.Active.ShouldBe(1);
        diagnostics.SnapshotCollections().Single().Active[CacheActiveKind.Transfers].ShouldBe(1);
        diagnostics.ForCollection("binary").Snapshot().Counters[CacheObservationKind.TransferAbandoned].ShouldBe(1);
        inner.Release.SetResult(1);
        await released.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.DisposeAsync();
        resources.Transfers.Active.ShouldBe(0);
    }

    private sealed class Probe
    {
        public List<CacheTransferObservation> Observations { get; } = [];

        public bool Throw { get; init; }
    }

    private sealed class ProbeBehavior(Probe probe) : ICacheCollectionBehavior, ICacheTransferObserver
    {
        public ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next, CancellationToken cancellationToken = default) =>
            next(context, cancellationToken);

        public ICacheTransferObservationScope BeginTransfer(CacheTransferContext context) => new Scope(probe);
    }

    private sealed class Scope(Probe probe) : ICacheTransferObservationScope
    {
        public void Complete(CacheTransferObservation observation)
        {
            probe.Observations.Add(observation);
            if (probe.Throw) { throw new InvalidOperationException("Expected optional observer failure."); }
        }
    }

    private sealed class IgnoredRead : Stream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<int> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => 1;

        public override long Position { get; set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.Entered.TrySetResult();
            return new(this.Release.Task);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}