// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

public class CacheBinaryEngineTests
{
    /// <summary>Stream limits reject publication; a source hiding its failed destination remains a contract violation.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OversizedPayload_DirectAndSourceWrites_RejectWithoutPublication(bool sourceLoad, bool catchFailure)
    {
        await using var fixture = new Fixture(c => c.Limits(l => l.MaxPayloadBytes(4)));
        var sut = fixture.Collection;
        IResult result;
        if (sourceLoad)
        {
            result = await sut.GetOrLoadAsync(new("oversized"), async (_, destination, token) =>
            {
                try { await destination.WriteAsync(new byte[5], token); }
                catch (StreamSizeLimitExceededException) when (catchFailure) { }

                return Result<CacheBinaryLoadResult>.Success(new());
            });
        }
        else
        {
            using var input = new MemoryStream(new byte[5]);
            result = await sut.SetAsync(new("oversized"), input);
        }

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(catchFailure ? CacheErrorCode.Contract : CacheErrorCode.Capacity);
        (await sut.OpenReadAsync("oversized")).Value.Found.ShouldBeFalse();
    }

    /// <summary>A manual binary refresh can replace a retained absence marker with a fully validated payload.</summary>
    [Fact]
    public async Task RefreshAsync_RetainedNegativeMarker_LoadsAndPublishesTheNewPayload()
    {
        await using var fixture = new Fixture(c => c.With(x => x.NegativeCaching.Enabled, true));
        var sut = fixture.Collection;
        var marker = await sut.GetOrLoadAsync(new("missing"), (_, _, _) =>
            ValueTask.FromResult(Result<CacheBinaryLoadResult>.Success(CacheBinaryLoadResult.NotFound())));
        var calls = 0;

        var result = await sut.RefreshAsync(new("missing"), async (_, destination, token) =>
        {
            calls++;
            await destination.WriteAsync(new byte[] { 1, 2, 3 }, token);
            return Result<CacheBinaryLoadResult>.Success(new());
        });

        result.IsSuccess.ShouldBeTrue();
        calls.ShouldBe(1);
        result.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        result.Value.Version.ShouldNotBe(marker.Value.Version);
        await using var handle = result.Value.Value;
        using var output = new MemoryStream();
        await handle.Stream.CopyToAsync(output);
        output.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
        (await handle.WaitForCompletionAsync()).Value.ShouldBe(CacheReadCompletion.Validated);
    }

    [Fact]
    public async Task SharedBinaryLoad_PublishesOnceAndReturnsIndependentPositions()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var owner = sut.GetOrLoadAsync(new("a"), async (_, stream, token) =>
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            await release.Task.WaitAsync(token);
            await stream.WriteAsync(new byte[] { 1, 2, 3 }, token);
            return Result<CacheBinaryLoadResult>.Success(new());
        }).AsTask();
        await entered.Task;
        var follower = sut.GetOrLoadAsync(new("A"), (_, _, _) => throw new InvalidOperationException()).AsTask();
        release.SetResult();
        var results = await Task.WhenAll(owner, follower);

        calls.ShouldBe(1);
        results.ShouldAllBe(r => r.IsSuccess && r.Value.StorageOutcome == CacheStorageOutcome.Stored);
        await using var first = results[0].Value.Value;
        await using var second = results[1].Value.Value;
        first.ShouldNotBeSameAs(second);
        first.Stream.ReadByte().ShouldBe(1);
        second.Stream.ReadByte().ShouldBe(1);
        first.Metadata.Version.ShouldBe(second.Metadata.Version);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyPayloadAndSourceAbsence_AreDistinct(bool absent)
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;

        var result = await sut.GetOrLoadAsync(new("a"), (_, _, _) => ValueTask.FromResult(Result<CacheBinaryLoadResult>.Success(
            absent ? CacheBinaryLoadResult.NotFound() : new())));

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsNotFound.ShouldBe(absent);
        result.Value.Found.ShouldBe(!absent);
        if (absent)
        {
            result.Value.Value.ShouldBeNull();
            result.Value.Version.ShouldBeNull();
        }
        else
        {
            await using var handle = result.Value.Value;
            handle.Stream.ReadByte().ShouldBe(-1);
            (await handle.WaitForCompletionAsync()).Value.ShouldBe(CacheReadCompletion.Validated);
        }
    }

    [Fact]
    public async Task SuppressedBinarySource_FailsWithoutPublicationOrReplay()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        var calls = 0;

        var result = await sut.GetOrLoadAsync(new("a"), async (_, stream, token) =>
        {
            calls++;
            await stream.WriteAsync(new byte[] { 1 }, token);
            return Result<CacheBinaryLoadResult>.Success(new(new() { SuppressStorage = true }));
        });

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Unsupported);
        calls.ShouldBe(1);
        (await sut.OpenReadAsync("a")).Value.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task DisabledOrBypassedBinaryLoad_RejectsBeforeSource()
    {
        await using var disabled = new Fixture(c => c.Enabled(false));
        var first = await disabled.Collection.GetOrLoadAsync(new("a"), (_, _, _) => throw new InvalidOperationException());
        first.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Unsupported);
        await using var enabled = new Fixture();
        var second = await enabled.Collection.GetOrLoadAsync(new("a"), (_, _, _) => throw new InvalidOperationException(), new CacheAccessOptions(CacheAccessMode.Bypass));
        second.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Unsupported);
    }

    [Fact]
    public async Task FailedRefresh_PreservesTheIndependentlyValidOldVersion()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        using var input = new MemoryStream([1, 2, 3]);
        await sut.SetAsync(new("a"), input);

        var failed = await sut.RefreshAsync(new("a"), (_, stream, _) =>
        {
            stream.Dispose();
            return ValueTask.FromResult(Result<CacheBinaryLoadResult>.Success(new()));
        });

        failed.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Contract);
        var current = await sut.OpenReadAsync("a");
        await using var handle = current.Value.Value;
        handle.Stream.ReadByte().ShouldBe(1);
    }

    [Fact]
    public async Task GroupedBinaryBatch_PublishesSequentiallyAndReturnsBoundedPartialHandles()
    {
        await using var fixture = new Fixture();
        var sut = fixture.Collection;
        var calls = 0;
        var items = Enumerable.Range(0, 8).Select(i => new CacheItem(i.ToString())).ToArray();

        var result = await sut.GetOrLoadManyAsync(items, Load);

        calls.ShouldBe(1);
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count(r => r.IsSuccess).ShouldBe(4);
        result.Value.Where(r => r.IsFailure).ShouldAllBe(r => r.Errors.OfType<CacheError>().Single().Code == CacheErrorCode.Capacity);
        foreach (var item in result.Value.Where(r => r.IsSuccess))
        {
            await item.Value.Value.DisposeAsync();
        }

        var later = await sut.OpenReadAsync("7");
        later.Value.Found.ShouldBeTrue();
        await later.Value.Value.DisposeAsync();

        async IAsyncEnumerable<CacheBinaryBatchLoadResult> Load(CacheBinaryBatchLoadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            calls++;
            foreach (var item in context.Items)
            {
                var opened = await context.OpenWriteAsync(item.Item.Key, token);
                opened.IsSuccess.ShouldBeTrue();
                await opened.Value.WriteAsync(new byte[] { 1, 2, 3 }, token);
                yield return new(item.Item.Key, Result<CacheBinaryLoadResult>.Success(new()));
            }
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly CacheRuntimeTasks tasks;

        internal Fixture(Action<CacheCollectionBuilder> configure = null)
        {
            var registrations = new ServiceCollection();
            registrations.AddApplicationCaching().WithBinaryContract("raw").WithBinaryCollection("items", configure);
            this.services = registrations.BuildServiceProvider();
            var catalog = this.services.GetRequiredService<CacheCatalog>();
            var resources = this.services.GetRequiredService<CacheRuntimeResources>();
            var provider = this.services.GetRequiredService<CacheProviderRegistry>().Get("memory");
            this.tasks = new(catalog);
            var binding = catalog.Collections["items"];
            var runtime = new CacheCollectionRuntime(binding, provider, catalog, resources, new(catalog), this.tasks, TimeProvider.System);
            this.Collection = new(runtime, (CacheBinaryValueContract)binding.Contract, this.services);
        }

        internal CacheBinaryCollection Collection { get; }

        public async ValueTask DisposeAsync()
        {
            await this.tasks.DisposeAsync();
            await this.services.DisposeAsync();
        }
    }
}