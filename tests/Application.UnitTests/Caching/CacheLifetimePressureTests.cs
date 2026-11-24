// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

public class CacheLifetimePressureTests
{
    [Fact]
    public async Task FullStreamAdmission_DoesNotBlockMetadataMissOrNegativeHit()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithCollection<string>("items", c => c.With(x => x.NegativeCaching.Enabled, true));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        (await sut.SetAsync(new("positive"), "value")).IsSuccess.ShouldBeTrue();
        (await sut.GetOrLoadAsync(new("negative"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound())))).IsSuccess.ShouldBeTrue();
        var handles = new List<ICacheReadHandle>();
        try
        {
            for (var index = 0; index < 4; index++) { handles.Add((await sut.OpenReadAsync("positive")).Value.Value); }

            using var cancellation = new CancellationTokenSource();
            var miss = sut.GetAsync("missing", cancellation.Token);
            var negative = sut.GetAsync("negative", cancellation.Token);
            try
            {
                miss.IsCompletedSuccessfully.ShouldBeTrue();
                negative.IsCompletedSuccessfully.ShouldBeTrue();
                (await miss).Value.Found.ShouldBeFalse();
                (await negative).Value.IsNotFound.ShouldBeTrue();
            }
            finally { cancellation.Cancel(); }
        }
        finally
        {
            foreach (var handle in handles) { await handle.DisposeAsync(); }
        }
    }

    [Fact]
    public async Task HostStop_CancelsOwnedWorkAndPreventsNewSourceAdmissions()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("items");
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var load = sut.GetOrLoadAsync(new("key"), async (_, token) =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { canceled.TrySetResult(); }

            return Result<CacheLoadResult<string>>.Success(new("value"));
        }).AsTask();
        await started.Task;

        provider.GetRequiredService<CacheRuntimeTasks>().BeginStop();

        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        (await load).IsFailure.ShouldBeTrue();
        (await sut.GetOrLoadAsync(new("other"), (_, _) => throw new InvalidOperationException("must not run"))).IsFailure.ShouldBeTrue();
        (await sut.GetAsync("key")).Value.Found.ShouldBeFalse();
    }
}