// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Profiling;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies optional repository and complete Active Entity profiling boundaries through the public recorder.</summary>
/// <example>Run with the DomainProfilingBehaviorTests filter.</example>
public sealed class DomainProfilingBehaviorTests
{
    /// <summary>Existing registration APIs activate both behaviors without registering profiling dependencies.</summary>
    /// <example>Run the omitted-registration theory.</example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_OmittedProfiling_PreservesBusinessExecution(bool activeEntity)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        var repository = Substitute.For<IGenericRepository<PersonStub>>();
        repository.CountAsync(Arg.Any<CancellationToken>()).Returns(17L);
        collection
            .AddRepository<PersonStub>(_ => repository)
            .WithBehavior<RepositoryProfilingBehavior<PersonStub>>();
        var provider = Substitute.For<IActiveEntityEntityProvider<Item, Guid>>();
        provider.CountAsync(Arg.Any<CancellationToken>()).Returns(Result<long>.Success(17));
        collection.AddActiveEntity(configuration =>
            configuration
                .For<Item, Guid>()
                .UseProviderFactory(_ => provider)
                .AddProfilingBehavior()
                .AddProfilingBehavior()
        );
        using var services = collection.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
        );
        using var scope = services.CreateScope();

        if (activeEntity)
        {
            var behaviors = scope
                .ServiceProvider.GetServices<IActiveEntityBehavior<Item>>()
                .ToArray();
            behaviors.ShouldHaveSingleItem().ShouldBeOfType<ActiveEntityProfilingBehavior<Item>>();
            var context = new ActiveEntityContext<Item, Guid>(provider, behaviors);
            (await Item.CountAsync(context)).Value.ShouldBe(17);
            await provider.Received(1).CountAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            var sut = scope.ServiceProvider.GetRequiredService<IGenericRepository<PersonStub>>();
            (await sut.CountAsync()).ShouldBe(17);
            await repository.Received(1).CountAsync(Arg.Any<CancellationToken>());
        }

        services.GetService<IOperationProfiler>().ShouldBeNull();
        services.GetService<IProfilingStorageProvider>().ShouldBeNull();
    }

    /// <summary>Repository calls own a stable root and forward the supplied cancellation token unchanged.</summary>
    /// <example>Run the independent-root test.</example>
    [Fact]
    public async Task Repository_IndependentCall_OwnsRootAndPreservesToken()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var inner = Substitute.For<IGenericRepository<PersonStub>>();
        var expected = new PersonStub();
        using var cancellation = new CancellationTokenSource();
        var id = Guid.Empty;
        inner
            .InsertAsync(expected, cancellation.Token)
            .Returns(_ =>
            {
                id = profiling.Current.Id;
                return Task.FromResult(expected);
            });
        var sut = new RepositoryProfilingBehavior<PersonStub>(inner, profiling);

        (await sut.InsertAsync(expected, cancellation.Token)).ShouldBeSameAs(expected);
        await inner.Received(1).InsertAsync(expected, cancellation.Token);
        var record = await Stored(services, id);
        record.Key.ShouldBe("repository:PersonStub:Insert");
        record.Kind.ShouldBe("Repository");
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Segments.ShouldBeEmpty();
        record.Dimensions.ShouldContain(d => d.Key == "repository.entityType");
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Repeated repository reads aggregate under the caller's operation without closing it or enumerating its result.</summary>
    /// <example>Run the joined-read test.</example>
    [Fact]
    public async Task Repository_JoinedReads_AggregateWithoutEnumeratingResult()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var owner = profiling.BeginOperation("catalog");
        var inner = Substitute.For<IGenericRepository<PersonStub>>();
        var expected = LazyEntities();
        inner
            .FindAllAsync(Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new RepositoryProfilingBehavior<PersonStub>(inner, profiling);

        for (var index = 0; index < 3; index++)
        {
            (await sut.FindAllAsync()).ShouldBeSameAs(expected);
            profiling.Current.ShouldBeSameAs(owner);
        }

        owner.Complete();
        owner.Dispose();
        var segment = (await Stored(services, owner.Id)).Segments.ShouldHaveSingleItem();
        segment.Key.ShouldBe("repository:PersonStub:FindAll");
        segment.Statistics.Count.ShouldBe(3);
        await inner.Received(3).FindAllAsync(null, default);
    }

    /// <summary>The Active Entity wrapper includes both hooks and provider work in one recording boundary.</summary>
    /// <example>Run the complete-operation test.</example>
    [Fact]
    public async Task ActiveEntity_CompleteOperation_EnclosesHooksAndProvider()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var sut = new Item();
        var provider = Substitute.For<IActiveEntityEntityProvider<Item, Guid>>();
        var order = new List<string>();
        var id = Guid.Empty;
        Task<Result> Observe(string name)
        {
            id = profiling.Current.Id;
            order.Add(name);
            return Task.FromResult(Result.Success());
        }

        var hooks = new Hooks(() => Observe("before"), () => Observe("after"));
        provider
            .InsertAsync(
                sut,
                Arg.Any<ActiveEntityCallbackOptions<Item, Guid>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                Observe("provider");
                return Result<Item>.Success(sut);
            });
        var context = new ActiveEntityContext<Item, Guid>(
            provider,
            [hooks, new ActiveEntityProfilingBehavior<Item>(profiling)]
        );

        (await sut.InsertAsync(context)).Value.ShouldBeSameAs(sut);
        order.ShouldBe(["before", "provider", "after"]);
        var record = await Stored(services, id);
        record.Key.ShouldBe("activeentity:Item:Insert");
        record.Kind.ShouldBe("ActiveEntity");
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Early failures from either hook or the provider close the root without inventing successful after hooks.</summary>
    /// <example>Run all failure-boundary cases.</example>
    [Theory]
    [InlineData("before")]
    [InlineData("provider")]
    [InlineData("after")]
    public async Task ActiveEntity_FailedResult_ClosesScopeAtEveryBoundary(string boundary)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var sut = new Item();
        var id = Guid.Empty;
        var providerCalls = 0;
        var afterCalls = 0;
        var hooks = new Hooks(
            () =>
            {
                id = profiling.Current.Id;
                return Task.FromResult(
                    boundary == "before"
                        ? Result.Failure("private before detail")
                        : Result.Success()
                );
            },
            () =>
            {
                afterCalls++;
                return Task.FromResult(
                    boundary == "after" ? Result.Failure("private after detail") : Result.Success()
                );
            }
        );
        var provider = Substitute.For<IActiveEntityEntityProvider<Item, Guid>>();
        provider
            .InsertAsync(
                sut,
                Arg.Any<ActiveEntityCallbackOptions<Item, Guid>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                providerCalls++;
                return boundary == "provider"
                    ? Result<Item>.Failure("private provider detail")
                    : Result<Item>.Success(sut);
            });
        var context = new ActiveEntityContext<Item, Guid>(
            provider,
            [hooks, new ActiveEntityProfilingBehavior<Item>(profiling)]
        );

        (await sut.InsertAsync(context)).IsFailure.ShouldBeTrue();
        providerCalls.ShouldBe(boundary == "before" ? 0 : 1);
        afterCalls.ShouldBe(boundary == "after" ? 1 : 0);
        var record = await Stored(services, id);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Failure.Message.ShouldBeNull();
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Both behaviors preserve synchronous errors, asynchronous errors and caller cancellation without leaking ambient state.</summary>
    /// <example>Run all exception and cancellation cases.</example>
    [Theory]
    [InlineData(false, "sync")]
    [InlineData(false, "async")]
    [InlineData(false, "cancel")]
    [InlineData(true, "sync")]
    [InlineData(true, "async")]
    [InlineData(true, "cancel")]
    public async Task BusinessException_PreservesIdentityAndClassifiesOutcome(
        bool activeEntity,
        string mode
    )
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var cancellation = new CancellationTokenSource();
        if (mode == "cancel")
        {
            cancellation.Cancel();
        }

        Exception expected =
            mode == "cancel"
                ? new OperationCanceledException(cancellation.Token)
                : new InvalidOperationException("private exception detail");
        var id = Guid.Empty;
        Task<T> Fail<T>()
        {
            id = profiling.Current.Id;
            return mode == "sync" ? throw expected : Task.FromException<T>(expected);
        }

        Func<Task> execute;
        if (activeEntity)
        {
            var provider = Substitute.For<IActiveEntityEntityProvider<Item, Guid>>();
            provider.CountAsync(cancellation.Token).Returns(_ => Fail<Result<long>>());
            var context = new ActiveEntityContext<Item, Guid>(
                provider,
                [new ActiveEntityProfilingBehavior<Item>(profiling)]
            );
            execute = () => Item.CountAsync(context, cancellation.Token);
        }
        else
        {
            var inner = Substitute.For<IGenericRepository<PersonStub>>();
            inner.CountAsync(cancellation.Token).Returns(_ => Fail<long>());
            var sut = new RepositoryProfilingBehavior<PersonStub>(inner, profiling);
            execute = () => sut.CountAsync(cancellation.Token);
        }

        Exception actual = null;
        try
        {
            await execute();
        }
        catch (Exception exception)
        {
            actual = exception;
        }

        actual.ShouldBeSameAs(expected);
        (await Stored(services, id)).Outcome.ShouldBe(
            mode == "cancel" ? OperationProfilingOutcome.Canceled : OperationProfilingOutcome.Failed
        );
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Disabled recording and execution-local suppression never replace the parent with another root.</summary>
    /// <example>Run the no-recording cases.</example>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DisabledOrSuppressed_ExecutesOnceWithoutCapture(
        bool activeEntity,
        bool disabled
    )
    {
        using var services = await Services(!disabled);
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var suppression = disabled ? null : profiling.Suppress();
        var calls = 0;
        Task<long> Next()
        {
            calls++;
            return Task.FromResult(23L);
        }

        if (activeEntity)
        {
            (
                await new ActiveEntityProfilingBehavior<Item>(profiling).ExecuteAsync(
                    "CountAsync",
                    Next
                )
            ).ShouldBe(23);
        }
        else
        {
            var inner = Substitute.For<IGenericRepository<PersonStub>>();
            inner.CountAsync(Arg.Any<CancellationToken>()).Returns(_ => Next());
            (
                await new RepositoryProfilingBehavior<PersonStub>(inner, profiling).CountAsync()
            ).ShouldBe(23);
        }

        calls.ShouldBe(1);
        services
            .GetRequiredService<OperationProfilingCompletionQueue>()
            .Snapshot()
            .Count.ShouldBe(0);
    }

    /// <summary>Observer faults at start, metadata and disposal preserve exactly-once business execution.</summary>
    /// <example>Run all fault injection cases.</example>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProfilingFault_ExecutesBusinessOnce(bool activeEntity, bool failStart)
    {
        var profiling = Substitute.For<IOperationProfiler>();
        profiling.Current.Returns((IProfilingOperationScope)null);
        if (failStart)
        {
            profiling
                .BeginOperation(Arg.Any<OperationProfilingStartRequest>())
                .Returns(_ => throw new InvalidOperationException("capture fault"));
        }
        else
        {
            var recording = Substitute.For<IProfilingOperationScope>();
            recording.IsRecording.Returns(true);
            recording
                .When(scope => scope.SetDimension(Arg.Any<string>(), Arg.Any<object>()))
                .Do(_ => throw new InvalidOperationException("metadata fault"));
            recording
                .When(scope => scope.Dispose())
                .Do(_ => throw new InvalidOperationException("dispose fault"));
            profiling.BeginOperation(Arg.Any<OperationProfilingStartRequest>()).Returns(recording);
        }

        var calls = 0;
        Task<long> Next()
        {
            calls++;
            return Task.FromResult(31L);
        }

        if (activeEntity)
        {
            (
                await new ActiveEntityProfilingBehavior<Item>(profiling).ExecuteAsync(
                    "CountAsync",
                    Next
                )
            ).ShouldBe(31);
        }
        else
        {
            var inner = Substitute.For<IGenericRepository<PersonStub>>();
            inner.CountAsync(Arg.Any<CancellationToken>()).Returns(_ => Next());
            (
                await new RepositoryProfilingBehavior<PersonStub>(inner, profiling).CountAsync()
            ).ShouldBe(31);
        }

        calls.ShouldBe(1);
    }

    /// <summary>Nested entity and repository executions share one caller operation and retain separate aggregate paths.</summary>
    /// <example>Run the nested parallel-capture test.</example>
    [Fact]
    public async Task ActiveEntity_ParallelCalls_WithRepositoryWork_AggregateNestedPaths()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var owner = profiling.BeginOperation("catalog");
        var inner = Substitute.For<IGenericRepository<PersonStub>>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        inner
            .CountAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await gate.Task;
                return 41L;
            });
        var repository = new RepositoryProfilingBehavior<PersonStub>(inner, profiling);
        var provider = Substitute.For<IActiveEntityEntityProvider<Item, Guid>>();
        provider
            .CountAsync(Arg.Any<CancellationToken>())
            .Returns(async _ => Result<long>.Success(await repository.CountAsync()));
        var context = new ActiveEntityContext<Item, Guid>(
            provider,
            [new ActiveEntityProfilingBehavior<Item>(profiling)]
        );

        var tasks = Enumerable.Range(0, 4).Select(_ => Item.CountAsync(context)).ToArray();
        gate.SetResult();
        (await Task.WhenAll(tasks)).ShouldAllBe(result => result.Value == 41);
        profiling.Current.ShouldBeSameAs(owner);
        owner.Complete();
        owner.Dispose();
        var record = await Stored(services, owner.Id);
        record.Segments.Count.ShouldBe(2);
        record.Segments.ShouldAllBe(segment => segment.Statistics.Count == 4);
        record.Segments.ShouldContain(segment =>
            segment.Key == "repository:PersonStub:Count" && segment.Path.Components.Count == 2
        );
        await inner.Received(4).CountAsync(default);
    }

    /// <summary>Existing custom callers and the explicit-name overload retain their business result and close the operation.</summary>
    /// <example>Run both custom-context API cases.</example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomContext_LegacyAndExplicitName_UseStableOperationKeys(bool explicitName)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var context = new ActiveEntityContext<Item, Guid>(
            Substitute.For<IActiveEntityEntityProvider<Item, Guid>>(),
            [new ActiveEntityProfilingBehavior<Item>(profiling)]
        );
        var id = Guid.Empty;
        var calls = 0;
        Task<int> Next(ActiveEntityContext<Item, Guid> actual)
        {
            actual.ShouldBeSameAs(context);
            calls++;
            id = profiling.Current.Id;
            return Task.FromResult(47);
        }

        var result = explicitName
            ? await ActiveEntityContextScope.UseAsync(context, Next, default, "Lookup")
            : await ActiveEntityContextScope.UseAsync(context, Next);
        result.ShouldBe(47);
        calls.ShouldBe(1);
        (await Stored(services, id)).Key.ShouldBe(
            "activeentity:Item:" + (explicitName ? "Lookup" : "UseContext")
        );
        profiling.Current.ShouldBeNull();
    }

    private static IEnumerable<PersonStub> LazyEntities()
    {
        yield return Throw();
        static PersonStub Throw() =>
            throw new InvalidOperationException("Profiling must not enumerate business results.");
    }

    private static async Task<ServiceProvider> Services(bool enabled = true)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddProfiling(options => options.Enabled(enabled)).WithOperationProfiling();
        var services = collection.BuildServiceProvider();
        if (enabled)
        {
            await services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        }

        return services;
    }

    private static async Task<OperationProfilingRecord> Stored(IServiceProvider services, Guid id)
    {
        await services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        return (
            await services.GetRequiredService<IOperationProfilingStore>().FindAsync(id)
        ).Value.ShouldNotBeNull();
    }

    /// <summary>Provides a public entity type for provider substitutes and real Active Entity entry points.</summary>
    /// <example><code>var entity = new DomainProfilingBehaviorTests.Item();</code></example>
    public sealed class Item : ActiveEntity<Item, Guid>;

    private sealed class Hooks(Func<Task<Result>> before, Func<Task<Result>> after)
        : ActiveEntityBehaviorBase<Item>
    {
        /// <inheritdoc />
        /// <example>Supplies a controlled before hook in operation-boundary tests.</example>
        public override Task<Result> BeforeInsertAsync(
            Item entity,
            CancellationToken cancellationToken = default
        ) => before();

        /// <inheritdoc />
        /// <example>Supplies a controlled after hook in operation-boundary tests.</example>
        public override Task<Result> AfterInsertAsync(
            Item entity,
            bool success,
            CancellationToken cancellationToken = default
        ) => after();
    }
}
