// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class CacheBehaviorTests
{
    [Fact]
    public async Task OwnedLoadBehavior_CannotStartNestedSourceWorkWhileHoldingSourceAdmission()
    {
        var services = new ServiceCollection();
        var probe = new NestedProbe();
        services.AddSingleton(probe);
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithBehavior<NestedLoad>("nested", CacheBehaviorStage.SourceExecution)
            .WithCollection<string>("items", c => c.With(x => x.Limits.MaxActiveSources, 1).With(x => x.Behaviors.SourceExecution, new[] { "nested" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        var result = await sut.GetOrLoadAsync(new("owner"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("value"))));

        result.Value.Value.ShouldBe("value");
        probe.Code.ShouldBe(CacheErrorCode.NestedCacheLoad);
    }

    [Fact]
    public async Task OperationChain_EntersInRegistrationOrderAndUnwindsInReverse()
    {
        var services = new ServiceCollection();
        var trace = new Trace();
        services.AddSingleton(trace);
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithBehavior<First>("first", CacheBehaviorStage.CollectionOperations)
            .WithBehavior<Second>("second", CacheBehaviorStage.CollectionOperations)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.CollectionOperations, new[] { "first", "second" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        (await sut.SetAsync(new("a"), "value")).IsSuccess.ShouldBeTrue();

        trace.Events.ShouldBe(new[] { "first:enter", "second:enter", "second:exit", "first:exit" });
    }

    [Fact]
    public async Task ObserverFailure_AfterConfirmedPublication_PreservesItsActualOutcome()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithBehavior<ThrowAfter>("observer", CacheBehaviorStage.CollectionOperations)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.CollectionOperations, new[] { "observer" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        var written = await sut.SetAsync(new("a"), "value");
        var read = await sut.GetAsync("a");

        written.IsSuccess.ShouldBeTrue();
        written.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        read.Value.Value.ShouldBe("value");
        read.Value.Version.ShouldBe(written.Value.Version);
    }

    [Fact]
    public async Task DuplicateContinuation_CannotRepeatAWriteOrEraseItsFirstOutcome()
    {
        var services = new ServiceCollection();
        var codec = new CountingCodec();
        services.AddApplicationCaching().WithValueContract<string>("text", c => c.WithJsonCodec(_ => codec))
            .WithBehavior<Duplicate>("duplicate", CacheBehaviorStage.CollectionOperations)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.CollectionOperations, new[] { "duplicate" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        var result = await sut.SetAsync(new("a"), "value");

        result.IsSuccess.ShouldBeTrue();
        result.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        codec.Encodes.ShouldBe(1);
    }

    [Fact]
    public async Task OptionalPolicyRejection_DoesNotConstructTheProviderOrInvokeTheSource()
    {
        var services = new ServiceCollection();
        var factories = 0;
        services.AddApplicationCaching().WithProviderKind("counted", InMemoryCacheStoreProvider.DefaultCapabilities, (_, name, options) =>
            {
                factories++;
                return new InMemoryCacheStoreProvider(name, options);
            }).WithProvider("memory", p => p.With(x => x.Kind, "counted"))
            .WithValueContract<string>("text").WithBehavior<Reject>("reject", CacheBehaviorStage.CollectionOperations)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.CollectionOperations, new[] { "reject" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        var result = await sut.GetOrLoadAsync(new("a"), (_, _) => throw new InvalidOperationException());

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.SourceRejected);
        factories.ShouldBe(0);
    }

    [Fact]
    public async Task BehaviorCannotFabricateASuccessOrHideTheEngineFailure()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithBehavior<MaskFailure>("mask", CacheBehaviorStage.CollectionOperations)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.CollectionOperations, new[] { "mask" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        using var input = new MemoryStream([1, 2, 3]);

        var result = await sut.SetEncodedAsync(new("a"), input);

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Unsupported);
        input.Position.ShouldBe(0);
    }

    [Fact]
    public async Task OwnedLoadBehavior_RunsOnceForTheOwnerAndAllFollowers()
    {
        var services = new ServiceCollection();
        var probe = new LoadProbe();
        services.AddSingleton(probe);
        services.AddApplicationCaching().WithValueContract<string>("text").WithBehavior<LoadObserver>("load", CacheBehaviorStage.SourceExecution)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.SourceExecution, new[] { "load" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = sut.GetOrLoadAsync(new("a"), async (_, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new("value"));
        }).AsTask();
        await entered.Task;
        var followers = Enumerable.Range(0, 4).Select(_ => sut.GetOrLoadAsync(new("A"), (_, _) => throw new InvalidOperationException()).AsTask()).ToArray();

        release.SetResult();
        var results = await Task.WhenAll(followers.Prepend(owner));

        results.ShouldAllBe(r => r.IsSuccess && r.Value.Value == "value");
        probe.Starts.ShouldBe(1);
        probe.Completions.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FluentGlobalBeforeCollection_AndConfiguredWholeStageReplacement(bool configured)
    {
        var configuration = configured ? new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Collections:items:Behaviors:CollectionOperations:0"] = "second"
        }).Build() : null;
        var services = new ServiceCollection();
        var trace = new Trace();
        services.AddSingleton(trace);
        services.AddApplicationCaching().WithConfiguration(configuration).WithValueContract<string>("text")
            .WithBehavior<First>("first", CacheBehaviorStage.CollectionOperations)
            .WithBehavior<Second>("second", CacheBehaviorStage.CollectionOperations)
            .Defaults(c => c.With(x => x.Behaviors.CollectionOperations, new[] { "first" }))
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.CollectionOperations, new[] { "second" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        (await sut.SetAsync(new("a"), "value")).IsSuccess.ShouldBeTrue();

        trace.Events.ShouldBe(configured ? new[] { "second:enter", "second:exit" } : new[] { "first:enter", "second:enter", "second:exit", "first:exit" });
    }

    [Fact]
    public async Task CompressionCapability_UsesThePayloadStageAndIsVisibleInTheEffectivePolicy()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithCollection<string>("items", c => c.Compression(CacheCompressionAlgorithm.GZip, System.IO.Compression.CompressionLevel.Fastest));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var value = new string('a', 10000);

        (await sut.SetAsync(new("a"), value)).IsSuccess.ShouldBeTrue();
        var acquired = await sut.OpenReadAsync("a");
        await using (var handle = acquired.Value.Value)
        {
            handle.Metadata.Compression.ShouldBe("gzip");
        }

        (await sut.GetAsync("a")).Value.Value.ShouldBe(value);
        provider.GetRequiredService<CacheCatalog>().Collections["items"].Options.Behaviors.PayloadTransformation.ShouldContain("COMPRESSION");
    }

    [Fact]
    public async Task PayloadFailureBeforePublication_PreservesThePreviousValidEntry()
    {
        var services = new ServiceCollection();
        var state = new PayloadState();
        services.AddSingleton(state);
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithBehavior<PayloadObserver>("payload", CacheBehaviorStage.PayloadTransformation)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.PayloadTransformation, new[] { "payload" }));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        await sut.SetAsync(new("a"), "old");
        state.Fail = true;

        var result = await sut.SetAsync(new("a"), "new");

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Contract);
        (await sut.GetAsync("a")).Value.Value.ShouldBe("old");
        provider.GetRequiredService<CacheRuntimeResources>().Transient.Used.ShouldBe(0);
    }

    [Fact]
    public async Task IncompatibleTypedBehavior_FailsBeforeNativeProviderAccess()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text")
            .WithBehavior<IntegerOnly>("wrong", CacheBehaviorStage.CollectionOperations)
            .WithCollection<string>("items", c => c.With(x => x.Behaviors.CollectionOperations, new[] { "wrong" }));
        await using var provider = services.BuildServiceProvider();

        Should.Throw<ArgumentException>(() => provider.GetRequiredService<IApplicationCache>());
    }

    public sealed class Trace
    {
        public List<string> Events { get; } = [];
    }

    public sealed class LoadProbe
    {
        public int Starts;
        public int Completions;
    }

    public sealed class PayloadState
    {
        public bool Fail;
    }

    public sealed class PayloadObserver(PayloadState state) : ICachePayloadBehavior
    {
        public async ValueTask<Result> ExecuteAsync(CachePayloadBehaviorContext context, CachePayloadContinuation next,
            CancellationToken cancellationToken = default)
        {
            var result = await next(context, cancellationToken);
            if (state.Fail)
            {
                throw new InvalidOperationException("The payload observer failed before publication.");
            }

            return result;
        }
    }

    public sealed class LoadObserver(LoadProbe probe) : ICacheLoadBehavior
    {
        public async ValueTask<Result<T>> ExecuteAsync<T>(CacheLoadBehaviorContext context, CacheLoadContinuation<T> next,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref probe.Starts);
            var result = await next(context, cancellationToken);
            Interlocked.Increment(ref probe.Completions);
            return result;
        }
    }

    public sealed class First(Trace trace) : CacheCollectionBehaviorBase<string>
    {
        public override async ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next,
            CancellationToken cancellationToken = default)
        {
            trace.Events.Add("first:enter");
            var result = await next(context, cancellationToken);
            trace.Events.Add("first:exit");
            return result;
        }
    }

    public sealed class Second(Trace trace) : CacheCollectionBehaviorBase<string>
    {
        public override async ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next,
            CancellationToken cancellationToken = default)
        {
            trace.Events.Add("second:enter");
            var result = await next(context, cancellationToken);
            trace.Events.Add("second:exit");
            return result;
        }
    }

    public sealed class ThrowAfter : CacheCollectionBehaviorBase<string>
    {
        public override async ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next,
            CancellationToken cancellationToken = default)
        {
            await next(context, cancellationToken);
            throw new InvalidOperationException("Observer failed.");
        }
    }

    public sealed class Duplicate : CacheCollectionBehaviorBase<string>
    {
        public override async ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next,
            CancellationToken cancellationToken = default)
        {
            await next(context, cancellationToken);
            return await next(context, cancellationToken);
        }
    }

    public sealed class Reject : CacheCollectionBehaviorBase<string>
    {
        public override ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Result<T>.Failure(new CacheError(CacheErrorCode.SourceRejected, "Policy rejected this operation.")));
    }

    public sealed class MaskFailure : CacheCollectionBehaviorBase<string>
    {
        public override async ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next,
            CancellationToken cancellationToken = default)
        {
            await next(context, cancellationToken);
            return Result<T>.Success(default);
        }
    }

    public sealed class IntegerOnly : CacheCollectionBehaviorBase<int>;

    public sealed class NestedProbe
    {
        internal CacheErrorCode? Code { get; set; }
    }

    public sealed class NestedLoad(NestedProbe probe) : ICacheLoadBehavior
    {
        public async ValueTask<Result<T>> ExecuteAsync<T>(CacheLoadBehaviorContext context, CacheLoadContinuation<T> next, CancellationToken cancellationToken = default)
        {
            var nested = await context.Items[0].Services.GetRequiredService<IApplicationCache>().GetCollection<string>("items")
                .GetOrLoadAsync(new("nested"), (_, _) => throw new InvalidOperationException("Nested source must never execute."), cancellationToken);
            probe.Code = nested.Errors.OfType<CacheError>().FirstOrDefault()?.Code;
            return await next(context, cancellationToken);
        }
    }

    private sealed class CountingCodec : IValueCodec<string>
    {
        private readonly SystemTextJsonValueCodec<string> inner = new();

        public int Encodes { get; private set; }

        public string FormatId => this.inner.FormatId;

        public int FormatVersion => this.inner.FormatVersion;

        public ValueCodecCapabilities Capabilities => this.inner.Capabilities;

        public ValueTask<Result> EncodeAsync(string value, Stream destination, CancellationToken cancellationToken = default)
        {
            this.Encodes++;
            return this.inner.EncodeAsync(value, destination, cancellationToken);
        }

        public ValueTask<Result<string>> DecodeAsync(Stream source, CancellationToken cancellationToken = default) => this.inner.DecodeAsync(source, cancellationToken);
    }
}