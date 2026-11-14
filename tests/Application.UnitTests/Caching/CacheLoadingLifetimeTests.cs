// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Proves last-interest cancellation and explicit bounded continuation using source gates and a controlled deadline clock.</summary>
public sealed class CacheLoadingLifetimeTests
{
    /// <summary>A loader cancellation callback fault cannot escape its deadline timer or release occupied source admission.</summary>
    [Fact]
    public async Task SourceDeadline_ThrowingLoaderCallback_RetainsAdmissionUntilActualCompletion()
    {
        await using var fixture = new CacheBackgroundFixture(builder => builder.WithCollection<string>("cards", collection =>
            collection.With(x => x.Loading.SourceTimeout, TimeSpan.FromSeconds(10))));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<Result<CacheLoadResult<string>>> Load(CacheLoadContext context, CancellationToken token)
        {
            using var registration = token.Register(() => throw new InvalidOperationException("Expected loader notification fault."));
            entered.TrySetResult();
            await release.Task;
            return Result<CacheLoadResult<string>>.Success(new("late"));
        }

        var sut = fixture.Collection();
        var pending = sut.GetOrLoadAsync(new("one"), Load).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var resources = fixture.Services.GetRequiredService<CacheRuntimeResources>();
        try
        {
            fixture.Time.Advance(TimeSpan.FromSeconds(10));
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));

            result.IsFailure.ShouldBeTrue();
            result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.SourceTimeout);
            resources.Sources.Active.ShouldBe(1);
        }
        finally { release.TrySetResult(); }

        var tasks = fixture.Services.GetRequiredService<CacheRuntimeTasks>();
        using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (tasks.Count != 0) { await Task.Delay(1, drain.Token); }

        resources.Sources.Active.ShouldBe(0);
        (await sut.GetAsync("one")).Value.Found.ShouldBeFalse();
    }

    /// <summary>Default work loses interest immediately; opt-in work continues only until its original deadline and retains admission through an ignoring callback.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LastInterestedCaller_ContinuationNeverRenewsTheOwnedDeadlineOrReleasesActualOccupancy(bool continueWithoutWaiters)
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards", c =>
        {
            c.With(x => x.Loading.SourceTimeout, TimeSpan.FromSeconds(10));
            if (continueWithoutWaiters) { c.With(x => x.Loading.ContinueWithoutWaiters, true); }
        }));
        using var leaving = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var expectedDeadline = fixture.Time.GetUtcNow().AddSeconds(10);
        var actualDeadline = DateTimeOffset.MinValue;
        CancellationToken ownedToken = default;
        async ValueTask<Result<CacheLoadResult<string>>> Load(CacheLoadContext context, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            actualDeadline = context.Deadline;
            ownedToken = token;
            using var registration = token.Register(() => cancelled.TrySetResult());
            started.TrySetResult();
            await release.Task;
            return Result<CacheLoadResult<string>>.Success(new("Late"));
        }

        var owner = fixture.Collection().GetOrLoadAsync(new("one"), Load, leaving.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.Time.Advance(TimeSpan.FromSeconds(9));
        leaving.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => owner);
        var resources = fixture.Services.GetRequiredService<CacheRuntimeResources>();
        resources.Sources.Active.ShouldBe(1);
        Task<Result<CacheLookup<string>>> follower = null;
        try
        {
            actualDeadline.ShouldBe(expectedDeadline);
            if (continueWithoutWaiters)
            {
                ownedToken.IsCancellationRequested.ShouldBeFalse();
                follower = fixture.Collection().GetOrLoadAsync(new("one"), Load).AsTask();
                fixture.Time.Advance(TimeSpan.FromSeconds(1));
            }

            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            resources.Sources.Active.ShouldBe(1);
            ownedToken.IsCancellationRequested.ShouldBeTrue();
            actualDeadline.ShouldBe(expectedDeadline);
        }
        finally { release.TrySetResult(); }

        if (follower is not null)
        {
            var finished = await follower;
            finished.IsFailure.ShouldBeTrue();
            finished.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.SourceTimeout);
        }

        var tasks = fixture.Services.GetRequiredService<CacheRuntimeTasks>();
        using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (tasks.Count != 0) { await Task.Delay(1, drain.Token); }

        resources.Sources.Active.ShouldBe(0);
        calls.ShouldBe(1);
        (await fixture.Collection().GetAsync("one")).Value.Found.ShouldBeFalse();
    }
}