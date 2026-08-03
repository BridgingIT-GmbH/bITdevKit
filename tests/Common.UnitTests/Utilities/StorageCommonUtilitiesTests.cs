// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Text;
using Microsoft.Extensions.Hosting;

public sealed class StorageCommonUtilitiesTests
{
    [Fact]
    public void ExpirationHelper_After_UsesOperationClockAndNormalizesUtc()
    {
        var now = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);

        var relative = ExpirationHelper.Resolve(ExpirationChange.After(TimeSpan.FromMinutes(5)), null, clock);
        var absolute = ExpirationHelper.Resolve(ExpirationChange.At(now.ToOffset(TimeSpan.FromHours(2))), null, clock);

        relative.ShouldBe(now.AddMinutes(5));
        absolute.ShouldBe(now);
        ExpirationHelper.IsDue(relative, relative.Value).ShouldBeTrue();
    }

    [Fact]
    public async Task AsyncInitializationGate_ConcurrentCallers_RunInitializerOnce()
    {
        var sut = new AsyncInitializationGate();
        var calls = 0;

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => sut.EnsureInitializedAsync(async cancellationToken =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(20, cancellationToken);
        })));

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task AsyncInitializationGate_FailedInitialization_CanBeRetried()
    {
        var sut = new AsyncInitializationGate();
        var calls = 0;
        Task Initialize(CancellationToken _)
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("failed");
            }

            return Task.CompletedTask;
        }

        await Should.ThrowAsync<InvalidOperationException>(() => sut.EnsureInitializedAsync(Initialize));
        await sut.EnsureInitializedAsync(Initialize);

        calls.ShouldBe(2);
    }

    [Fact]
    public async Task ContentHashHelper_StreamAndByteHashes_AreCanonicalAndEqual()
    {
        var content = Encoding.UTF8.GetBytes("document payload");
        await using var source = new MemoryStream(content);
        await using var destination = new MemoryStream();

        var copied = await ContentHashHelper.CopyAndComputeSha256Async(source, destination, content.Length);

        copied.Length.ShouldBe(content.Length);
        copied.ContentHash.ShouldBe(ContentHashHelper.ComputeSha256(content));
        ContentHashHelper.IsSha256(copied.ContentHash).ShouldBeTrue();
    }

    [Fact]
    public void ContentTransformEnvelopeCodec_RoundTripsAndRejectsDuplicateTransforms()
    {
        var envelope = new ContentTransformEnvelope
        {
            LogicalLength = 10,
            StoredLength = 8,
            LogicalContentHash = ContentHashHelper.ComputeSha256("logical"),
            StoredContentHash = ContentHashHelper.ComputeSha256("stored"),
            Transforms = [new() { Id = "gzip" }]
        };

        var decoded = ContentTransformEnvelopeCodec.Decode(ContentTransformEnvelopeCodec.Encode(envelope));

        decoded.Version.ShouldBe(envelope.Version);
        decoded.LogicalLength.ShouldBe(envelope.LogicalLength);
        decoded.LogicalContentHash.ShouldBe(envelope.LogicalContentHash);
        decoded.StoredLength.ShouldBe(envelope.StoredLength);
        decoded.StoredContentHash.ShouldBe(envelope.StoredContentHash);
        decoded.Transforms.Single().Id.ShouldBe("gzip");
        Should.Throw<FormatException>(() => ContentTransformEnvelopeCodec.Encode(envelope with
        {
            Transforms = [new() { Id = "gzip" }, new() { Id = "GZIP" }]
        }));
    }

    [Fact]
    public void KeyDisplayStrategies_UseRawKeysByDefaultAndStableSafeHashesWhenConfigured()
    {
        const string key = "customers/42";

        new RawKeyDisplayStrategy().Display(key).ShouldBe(key);
        new Sha256KeyDisplayStrategy().Display(key).ShouldBe(ContentHashHelper.ComputeSha256(key));
    }

    [Fact]
    public async Task PeriodicBackgroundService_WaitsForStartupAndRunsOneIterationAtATime()
    {
        var lifetime = new TestApplicationLifetime();
        var service = new TestPeriodicService(lifetime);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(25);
        service.IterationCount.ShouldBe(0);

        lifetime.Start();
        await service.FirstIteration.WaitAsync(TimeSpan.FromSeconds(2));
        service.MaximumConcurrency.ShouldBe(1);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PeriodicBackgroundService_ExposesUnexpectedIterationFailure()
    {
        var lifetime = new TestApplicationLifetime();
        lifetime.Start();
        var service = new TestPeriodicService(lifetime, fail: true);

        await service.StartAsync(CancellationToken.None);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class TestPeriodicService(IHostApplicationLifetime lifetime, bool fail = false)
        : PeriodicBackgroundService(
            new()
            {
                Interval = TimeSpan.FromMilliseconds(10),
                StopTimeout = TimeSpan.FromSeconds(1)
            },
            lifetime)
    {
        private readonly TaskCompletionSource firstIteration = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int concurrency;
        private int iterationCount;
        private int maximumConcurrency;

        public Task FirstIteration => this.firstIteration.Task;
        public int IterationCount => Volatile.Read(ref this.iterationCount);
        public int MaximumConcurrency => Volatile.Read(ref this.maximumConcurrency);

        protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref this.concurrency);
            InterlockedExtensions.Max(ref this.maximumConcurrency, current);
            try
            {
                Interlocked.Increment(ref this.iterationCount);
                this.firstIteration.TrySetResult();
                if (fail)
                {
                    throw new InvalidOperationException("iteration failed");
                }

                await Task.Delay(5, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref this.concurrency);
            }
        }
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => this.started.Token;
        public CancellationToken ApplicationStopping => this.stopping.Token;
        public CancellationToken ApplicationStopped => this.stopped.Token;
        public void StopApplication() => this.stopping.Cancel();
        public void Start() => this.started.Cancel();
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            var current = Volatile.Read(ref target);
            while (current < value)
            {
                var observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }
}
