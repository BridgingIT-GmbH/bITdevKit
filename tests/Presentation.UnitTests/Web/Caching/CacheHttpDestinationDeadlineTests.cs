// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using System.Threading.Channels;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Checks HTTP destination deadlines through final flush and actual ownership after ignored cancellation.</summary>
public sealed class CacheHttpDestinationDeadlineTests
{
    /// <summary>Destination expiry cancels a blocked write and keeps its buffer and handle until ignored cancellation actually ends.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Write_IdleDeadline_AbortsAndRetainsActualOwnership(bool ignoreCancellation)
    {
        var clock = new FakeTimeProvider();
        await using var provider = Services(clock);
        using var destination = new StalledDestination(ignoreCancellation);
        var fixture = Context(provider, destination);
        var resources = provider.GetRequiredService<CacheRuntimeResources>();
        var sut = CreateResult(provider, fixture.Handle);
        var actual = sut.ExecuteAsync(fixture.Context);
        await destination.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        clock.Advance(TimeSpan.FromSeconds(2));
        fixture.Lifetime.Aborted.ShouldBeTrue();
        if (ignoreCancellation)
        {
            await destination.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            actual.IsCompleted.ShouldBeFalse();
            resources.Transient.Used.ShouldBeGreaterThanOrEqualTo(provider.GetRequiredService<CacheCatalog>().Collections["reports"].Options.Streaming.BufferSize);
            await fixture.Handle.DidNotReceive().DisposeAsync();
            destination.Release.TrySetResult();
        }

        await actual.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Handle.Received(1).DisposeAsync();
        resources.Transient.Used.ShouldBe(0);
        AssertFailed(provider);
    }

    /// <summary>The destination idle budget remains active after successful source integrity verification.</summary>
    [Fact]
    public async Task FinalFlush_IdleDeadline_AbortsAfterSourceValidation()
    {
        var clock = new FakeTimeProvider();
        await using var provider = Services(clock);
        using var destination = new StalledDestination(false, finalFlush: true);
        var fixture = Context(provider, destination);
        var sut = CreateResult(provider, fixture.Handle);
        var actual = sut.ExecuteAsync(fixture.Context);
        await destination.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Handle.Received(1).WaitForCompletionAsync(Arg.Any<CancellationToken>());

        clock.Advance(TimeSpan.FromSeconds(2));
        await actual.WaitAsync(TimeSpan.FromSeconds(10));

        fixture.Lifetime.Aborted.ShouldBeTrue();
        await fixture.Handle.Received(1).DisposeAsync();
        provider.GetRequiredService<CacheRuntimeResources>().Transient.Used.ShouldBe(0);
        AssertFailed(provider);
    }

    /// <summary>Successful delivered chunks renew idle time without extending the fixed destination lifetime.</summary>
    [Fact]
    public async Task DeliveredProgress_DoesNotResetAbsoluteDestinationLifetime()
    {
        var clock = new FakeTimeProvider();
        await using var provider = Services(clock, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
        using var destination = new SteppedDestination();
        var fixture = Context(provider, destination, new OneByteSource());
        var sut = CreateResult(provider, fixture.Handle);
        var actual = sut.ExecuteAsync(fixture.Context);
        for (var index = 0; index < 2; index++)
        {
            var release = await destination.NextAsync();
            clock.Advance(TimeSpan.FromSeconds(1));
            release.TrySetResult();
        }

        await destination.NextAsync();
        clock.Advance(TimeSpan.FromSeconds(1));
        await actual.WaitAsync(TimeSpan.FromSeconds(10));

        fixture.Lifetime.Aborted.ShouldBeTrue();
        destination.Length.ShouldBe(2);
        await fixture.Handle.Received(1).DisposeAsync();
        provider.GetRequiredService<CacheRuntimeResources>().Transient.Used.ShouldBe(0);
        AssertFailed(provider);
    }

    /// <summary>Elapsed idle time is enforced before successful delivery can renew it even when host timers have not run.</summary>
    [Fact]
    public async Task DelayedTimer_DoesNotReviveAnAlreadyExpiredDestination()
    {
        var clock = new FakeTimeProvider();
        await using var provider = Services(new DelayedTimerTimeProvider(clock));
        using var destination = new StalledDestination(true);
        var fixture = Context(provider, destination);
        var sut = CreateResult(provider, fixture.Handle);
        var actual = sut.ExecuteAsync(fixture.Context);
        try
        {
            await destination.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            clock.Advance(TimeSpan.FromSeconds(3));
            fixture.Lifetime.Aborted.ShouldBeFalse();
        }
        finally { destination.Release.TrySetResult(); }

        await actual.WaitAsync(TimeSpan.FromSeconds(10));

        fixture.Lifetime.Aborted.ShouldBeTrue();
        await fixture.Handle.Received(1).DisposeAsync();
        provider.GetRequiredService<CacheRuntimeResources>().Transient.Used.ShouldBe(0);
        AssertFailed(provider);
    }

    private static ServiceProvider Services(TimeProvider clock, TimeSpan? idle = null, TimeSpan? lifetime = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddApplicationCaching().WithBinaryContract("raw").WithBinaryCollection("reports", collection => collection
            .With(x => x.Streaming.IdleTimeout, idle ?? TimeSpan.FromSeconds(2))
            .With(x => x.Streaming.Lifetime, lifetime ?? TimeSpan.FromSeconds(10)));
        return services.BuildServiceProvider();
    }

    private static (DefaultHttpContext Context, ICacheReadHandle Handle, Lifetime Lifetime) Context(ServiceProvider provider,
        Stream destination, Stream source = null)
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = destination;
        var lifetime = new Lifetime();
        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        var handle = Substitute.For<ICacheReadHandle>();
        handle.Stream.Returns(source ?? new MemoryStream([1, 2, 3]));
        handle.Metadata.Returns(new CacheEntryMetadata { Version = new("version"), LogicalSizeBytes = 3 });
        handle.WaitForCompletionAsync(Arg.Any<CancellationToken>()).Returns(
            ValueTask.FromResult(Result<CacheReadCompletion>.Success(CacheReadCompletion.Validated)));
        return (context, handle, lifetime);
    }

    private static CacheStreamingResult CreateResult(ServiceProvider provider, ICacheReadHandle handle) => new(handle,
        provider.GetRequiredService<CacheCatalog>().Collections["reports"], provider.GetRequiredService<CacheRuntimeResources>(), CacheHttpCachePolicy.NoStore);

    private static void AssertFailed(ServiceProvider provider)
    {
        var observed = provider.GetRequiredService<CacheDiagnostics>().ForCollection("reports").Snapshot();
        observed.Counters[CacheObservationKind.HttpDestinationFailed].ShouldBe(1);
        observed.Counters[CacheObservationKind.HttpDestinationCompleted].ShouldBe(0);
    }

    private sealed class Lifetime : IHttpRequestLifetimeFeature
    {
        /// <summary>Gets whether the destination connection was aborted.</summary>
        public bool Aborted { get; private set; }

        /// <inheritdoc />
        public CancellationToken RequestAborted { get; set; }

        /// <inheritdoc />
        public void Abort() => this.Aborted = true;
    }

    private sealed class StalledDestination(bool ignoreCancellation, bool finalFlush = false) : MemoryStream
    {
        private int flushes;

        /// <summary>Gets the explicit destination-operation entry barrier.</summary>
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the explicit cancellation observation barrier.</summary>
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the release of actual work that deliberately ignores cancellation.</summary>
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!finalFlush) { await this.WaitAsync(cancellationToken); }

            await base.WriteAsync(buffer, CancellationToken.None);
        }

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (finalFlush && ++this.flushes > 1) { await this.WaitAsync(cancellationToken); }
        }

        private async Task WaitAsync(CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => this.CancellationObserved.TrySetResult());
            this.Entered.TrySetResult();
            if (ignoreCancellation) { await this.Release.Task; }
            else { await this.Release.Task.WaitAsync(cancellationToken); }
        }
    }

    private sealed class SteppedDestination : MemoryStream
    {
        private readonly Channel<TaskCompletionSource> writes = Channel.CreateUnbounded<TaskCompletionSource>();

        /// <summary>Returns the next actual write's independent source-release barrier.</summary>
        public Task<TaskCompletionSource> NextAsync() => this.writes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this.writes.Writer.TryWrite(release).ShouldBeTrue();
            await release.Task.WaitAsync(cancellationToken);
            await base.WriteAsync(buffer, CancellationToken.None);
        }
    }

    private sealed class OneByteSource() : MemoryStream([1, 2, 3])
    {
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }

    private sealed class DelayedTimerTimeProvider(FakeTimeProvider clock) : TimeProvider
    {
        /// <inheritdoc />
        public override long TimestampFrequency => clock.TimestampFrequency;

        /// <inheritdoc />
        public override long GetTimestamp() => clock.GetTimestamp();

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();

        /// <inheritdoc />
        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period) => new DelayedTimer();
    }

    private sealed class DelayedTimer : ITimer
    {
        /// <inheritdoc />
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        /// <inheritdoc />
        public void Dispose() { }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}