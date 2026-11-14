// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

public class CacheBatchTests
{
    [Fact]
    public async Task GroupedBatch_WithOneSourceSlot_LoadsAllOwnedMissesInOneCallback()
    {
        await using var fixture = new Fixture(c => c.With(x => x.Limits.MaxActiveSources, 1));
        var sut = fixture.Collection;
        await sut.SetAsync(new("hit"), "present");
        var calls = 0;
        var items = Enumerable.Range(0, 12).Select(i => new CacheItem(i.ToString())).Prepend(new("hit")).ToArray();

        var result = await sut.GetOrLoadManyAsync(items, Load);

        result.IsSuccess.ShouldBeTrue();
        calls.ShouldBe(1);
        result.Value[0].Value.Value.ShouldBe("present");
        result.Value.Skip(1).ShouldAllBe(r => r.IsSuccess && r.Value.StorageOutcome == CacheStorageOutcome.Stored);

        async IAsyncEnumerable<CacheBatchLoadResult<string>> Load(CacheBatchLoadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            calls++;
            context.Items.Count.ShouldBe(12);
            context.Items.ShouldAllBe(c => c.Item.Key != "hit");
            await Task.CompletedTask;
            foreach (var item in context.Items.Reverse())
            {
                token.ThrowIfCancellationRequested();
                yield return new(item.Item.Key, Result<CacheLoadResult<string>>.Success(new(item.Item.Key)));
            }
        }
    }

    [Fact]
    public async Task OverlappingGroupedBatches_CompleteOwnedKeysBeforeJoiningForeignWork()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondOwnedFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = sut.GetOrLoadManyAsync([new("a"), new("b")], First).AsTask();
        await firstEntered.Task;

        var second = sut.GetOrLoadManyAsync([new("b"), new("c")], Second).AsTask();
        var results = await Task.WhenAll(first, second);

        results.ShouldAllBe(r => r.IsSuccess && r.Value.All(item => item.IsSuccess));
        results[1].Value[0].Value.LookupOutcome.ShouldBe(CacheLookupOutcome.Joined);
        results[1].Value[1].Value.Value.ShouldBe("c");

        async IAsyncEnumerable<CacheBatchLoadResult<string>> First(CacheBatchLoadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            firstEntered.SetResult();
            yield return new("a", Result<CacheLoadResult<string>>.Success(new("a")));
            await secondOwnedFinished.Task.WaitAsync(token);
            yield return new("b", Result<CacheLoadResult<string>>.Success(new("b")));
        }

        async IAsyncEnumerable<CacheBatchLoadResult<string>> Second(CacheBatchLoadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            context.Items.Single().Item.Key.ShouldBe("c");
            yield return new("c", Result<CacheLoadResult<string>>.Success(new("c")));
            await Task.CompletedTask;
            secondOwnedFinished.SetResult();
        }
    }

    [Fact]
    public async Task OmittedCompletion_IsExplicitlyIncompleteWithoutRollingBackPublishedItems()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;

        var result = await sut.GetOrLoadManyAsync([new("a"), new("b")], Load);

        result.Value[0].IsSuccess.ShouldBeTrue();
        result.Value[1].Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Incomplete);
        (await sut.GetAsync("a")).Value.Value.ShouldBe("a");

        async IAsyncEnumerable<CacheBatchLoadResult<string>> Load(CacheBatchLoadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            await Task.CompletedTask;
            yield return new("a", Result<CacheLoadResult<string>>.Success(new("a")));
        }
    }

    [Fact]
    public async Task DuplicateCompletion_ReportsContractFailureAndPreservesConfirmedMutation()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;

        var result = await sut.GetOrLoadManyAsync([new("a")], Load);

        result.Value[0].Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Contract);
        (await sut.GetAsync("a")).Value.Value.ShouldBe("first");

        async IAsyncEnumerable<CacheBatchLoadResult<string>> Load(CacheBatchLoadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            await Task.CompletedTask;
            yield return new("a", Result<CacheLoadResult<string>>.Success(new("first")));
            yield return new("A", Result<CacheLoadResult<string>>.Success(new("second")));
        }
    }

    [Fact]
    public async Task EquivalentDuplicateInput_IsRejectedBeforeAnyCallback()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;

        var result = await sut.GetOrLoadManyAsync([new("a"), new("A")], (CacheLoadDelegate<string>)((_, _) => throw new InvalidOperationException()));

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Validation);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly CacheRuntimeTasks tasks;

        internal Fixture(Action<CacheCollectionBuilder> configure = null)
        {
            var registrations = new ServiceCollection();
            registrations.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("items", configure);
            this.services = registrations.BuildServiceProvider();
            var catalog = this.services.GetRequiredService<CacheCatalog>();
            var resources = this.services.GetRequiredService<CacheRuntimeResources>();
            var provider = this.services.GetRequiredService<CacheProviderRegistry>().Get("memory");
            this.tasks = new(catalog);
            var binding = catalog.Collections["items"];
            var runtime = new CacheCollectionRuntime(binding, provider, catalog, resources, new(catalog), this.tasks, TimeProvider.System);
            this.Collection = new(runtime, (CacheValueContract<string>)binding.Contract, this.services);
        }

        internal CacheCollection<string> Collection { get; }

        public async ValueTask DisposeAsync()
        {
            await this.tasks.DisposeAsync();
            await this.services.DisposeAsync();
        }
    }
}