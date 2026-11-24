// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

public class CacheSourceResilienceTests
{
    [Fact]
    public async Task NamedSourceCircuit_SharesDependencyStateAcrossCollectionsAndPreservesHits()
    {
        var services = new ServiceCollection();
        var factories = 0;
        services.AddApplicationCaching().WithSourceResilience("catalog", _ =>
            {
                factories++;
                return new ResiliencePipelineBuilder().AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<CacheSourceException>(), FailureRatio = 1,
                    MinimumThroughput = 2, SamplingDuration = TimeSpan.FromMinutes(1), BreakDuration = TimeSpan.FromMinutes(1)
                }).Build();
            }).WithValueContract<string>("text").Defaults(c => c.With(x => x.Loading.SourceResilience, "catalog"))
            .WithCollection<string>("first").WithCollection<string>("second");
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IApplicationCache>();
        var first = cache.GetCollection<string>("first");
        var second = cache.GetCollection<string>("second");
        await first.SetAsync(new("hit"), "ready");
        var calls = 0;

        (await first.GetOrLoadAsync(new("one"), Fail)).IsFailure.ShouldBeTrue();
        (await second.GetOrLoadAsync(new("two"), Fail)).IsFailure.ShouldBeTrue();
        var rejected = await first.GetOrLoadAsync(new("three"), Fail);

        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.SourceRejected);
        calls.ShouldBe(2);
        factories.ShouldBe(1);
        (await first.GetOrLoadAsync(new("hit"), Fail)).Value.Value.ShouldBe("ready");
        calls.ShouldBe(2);

        ValueTask<Result<CacheLoadResult<string>>> Fail(CacheLoadContext context, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Failure(new CacheError(CacheErrorCode.SourceFailure, "Source unavailable.")));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExistingRetryPipeline_CannotExceedTheOwnedAttemptBudget(int maximum)
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithSourceResilience("source", _ => new ResiliencePipelineBuilder().AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<CacheSourceException>(), MaxRetryAttempts = 5, Delay = TimeSpan.Zero
            }).Build()).WithValueContract<string>("text")
            .WithCollection<string>("items", c => c.With(x => x.Loading.SourceResilience, "source").With(x => x.Loading.MaxAttempts, maximum));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var calls = 0;

        var result = await sut.GetOrLoadAsync(new("a"), (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Failure(new CacheError(CacheErrorCode.SourceFailure, "Unavailable.")));
        });

        result.IsFailure.ShouldBeTrue();
        calls.ShouldBe(maximum);
    }
}