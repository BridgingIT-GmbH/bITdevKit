// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using System.Text;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Checks collection transfer deadlines and actual resource ownership while public JSON input is decoded.</summary>
public sealed class CacheHttpBodyDeadlineTests
{
    /// <summary>An idle body is canceled; ignored cancellation retains its decode slot and memory until the actual read returns.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypedBody_IdleDeadline_CancelsWithoutReleasingUnfinishedReadOwnership(bool ignoreCancellation)
    {
        var clock = new FakeTimeProvider();
        await using var services = Services(clock);
        var catalog = services.GetRequiredService<CacheCatalog>();
        var resources = services.GetRequiredService<CacheRuntimeResources>();
        var sut = services.GetRequiredService<CacheHttpRequestReader>();
        sut.ValidateProfiles(catalog);
        using var body = new HeldBody(ignoreCancellation);
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = body;
        var baseline = resources.Transient.Used;

        var pending = sut.ReadWriteAsync(context.Request, catalog.Collections["items"], default).AsTask();
        await body.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var occupied = resources.Transient.Used;
        occupied.ShouldBeGreaterThan(baseline);
        resources.DecodePromotions.Active.ShouldBe(1);
        clock.Advance(TimeSpan.FromSeconds(2));
        if (ignoreCancellation)
        {
            await body.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            pending.IsCompleted.ShouldBeFalse();
            resources.Transient.Used.ShouldBe(occupied);
            resources.DecodePromotions.Active.ShouldBe(1);
            body.Release.TrySetResult();
        }

        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().ShouldContain(error => error.Code == CacheErrorCode.CoordinationTimeout);
        resources.Transient.Used.ShouldBe(baseline);
        resources.DecodePromotions.Active.ShouldBe(0);
        body.Disposed.ShouldBeFalse();
    }

    /// <summary>Successful body progress renews inactivity but cannot extend the absolute collection transfer lifetime.</summary>
    [Fact]
    public async Task TypedBody_ContinuousProgress_StillStopsAtAbsoluteLifetime()
    {
        var clock = new FakeTimeProvider();
        await using var services = Services(clock);
        var catalog = services.GetRequiredService<CacheCatalog>();
        var resources = services.GetRequiredService<CacheRuntimeResources>();
        var sut = services.GetRequiredService<CacheHttpRequestReader>();
        sut.ValidateProfiles(catalog);
        using var body = new ProgressBody(clock);
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = body;
        var baseline = resources.Transient.Used;

        var result = await sut.ReadWriteAsync(context.Request, catalog.Collections["items"], default);

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().ShouldContain(error => error.Code == CacheErrorCode.CoordinationTimeout);
        body.Reads.ShouldBe(3);
        resources.Transient.Used.ShouldBe(baseline);
        resources.DecodePromotions.Active.ShouldBe(0);
    }

    private static ServiceProvider Services(FakeTimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithCollection<string>("items", collection => collection.With(x => x.Streaming.IdleTimeout, TimeSpan.FromSeconds(2))
                .With(x => x.Streaming.Lifetime, TimeSpan.FromSeconds(3)))
            .WithHttpModel<string, string>("text", value => value, value => 64 + (value?.Length ?? 0) * 2L, 128, 1024)
            .WithEndpoints();
        return services.BuildServiceProvider();
    }

    private sealed class HeldBody(bool ignoreCancellation) : MemoryStream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var registration = cancellationToken.Register(() => this.CancellationObserved.TrySetResult());
            this.Entered.TrySetResult();
            if (ignoreCancellation) { await this.Release.Task; }
            else { await this.Release.Task.WaitAsync(cancellationToken); }

            return 0;
        }

        protected override void Dispose(bool disposing)
        {
            this.Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ProgressBody(FakeTimeProvider clock) : MemoryStream
    {
        private readonly byte[] payload = Encoding.UTF8.GetBytes("\"a\"");

        public int Reads { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (this.Reads == this.payload.Length) { return ValueTask.FromResult(0); }

            buffer.Span[0] = this.payload[this.Reads++];
            clock.Advance(TimeSpan.FromSeconds(1));
            return ValueTask.FromResult(1);
        }
    }
}