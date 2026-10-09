// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies optional Requester and Notifier profiling against real dispatch and capture faults.</summary>
/// <example>Runs request, notification and handler boundaries with the shared operation recorder.</example>
public sealed class RequesterNotifierProfilingBehaviorTests
{
    /// <summary>Checks omitted capture returns the original task without adding an asynchronous wrapper.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Behavior_OmittedProfiler_ReturnsOriginalBusinessTask(int behavior)
    {
        var sut = Behavior(behavior, null);
        IResult expected = Result.Success();
        var pending = new TaskCompletionSource<IResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var calls = 0;

        var actual = sut.HandleAsync(
            new(),
            new PublishOptions { ExecutionMode = ExecutionMode.FireAndForget },
            typeof(FirstHandler),
            () =>
            {
                calls++;
                return pending.Task;
            }
        );

        actual.ShouldBeSameAs(pending.Task);
        calls.ShouldBe(1);
        pending.SetResult(expected);
        (await actual).ShouldBeSameAs(expected);
    }

    /// <summary>Checks actual dispatch and repeated setup when no profiling dependencies exist.</summary>
    [Fact]
    public async Task Builders_OmittedProfiling_ActivateOnceWithoutHiddenDependencies()
    {
        var services = Services();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
        );
        using var scope = provider.CreateScope();
        var requestBehaviors = scope
            .ServiceProvider.GetRequiredService<IRequestBehaviorsProvider>()
            .GetBehaviors<TestRequest, string>(scope.ServiceProvider);
        var notificationBehaviors = scope
            .ServiceProvider.GetRequiredService<INotificationBehaviorsProvider>()
            .GetBehaviors<TestNotification>(scope.ServiceProvider);

        requestBehaviors
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingRequestBehavior<TestRequest, IResult<string>>>();
        notificationBehaviors.Count.ShouldBe(2);
        notificationBehaviors.Count(behavior => behavior.IsHandlerSpecific()).ShouldBe(1);
        (
            await scope
                .ServiceProvider.GetRequiredService<IRequester>()
                .SendAsync<TestRequest, string>(new())
        ).Value.ShouldBe("preserved");
        (
            await scope
                .ServiceProvider.GetRequiredService<INotifier>()
                .PublishAsync(new TestNotification())
        ).IsSuccess.ShouldBeTrue();
        scope.ServiceProvider.GetService<IOperationProfiler>().ShouldBeNull();
        scope.ServiceProvider.GetService<IProfilingStorageProvider>().ShouldBeNull();
        scope.ServiceProvider.GetService<OperationProfilingWriterService>().ShouldBeNull();
    }

    /// <summary>Checks Requester DI, original tokens/results and failure classification without payload capture.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Requester_RealDispatch_RecordsPipelineResult(bool fail)
    {
        var (profiling, sink, _) = OperationProfilerTests.Create();
        using var cancellation = new CancellationTokenSource();
        var expected = fail
            ? Result<string>.Failure("private message")
            : Result<string>.Success("private payload");
        var calls = 0;
        var services = Services(
            profiling,
            token =>
            {
                token.ShouldBe(cancellation.Token);
                calls++;
                return Task.FromResult(expected);
            }
        );
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IRequester>();

        var result = await sut.SendAsync<TestRequest, string>(
            new(),
            cancellationToken: cancellation.Token
        );

        result.ShouldBe(expected);
        calls.ShouldBe(1);
        var record = sink.Records.ShouldHaveSingleItem();
        record.Key.ShouldBe("requester:TestRequest");
        record.Kind.ShouldBe("Requester");
        record.Outcome.ShouldBe(
            fail ? OperationProfilingOutcome.Failed : OperationProfilingOutcome.Completed
        );
        record
            .Dimensions.Single(dimension => dimension.Key == "requester.handlerType")
            .Value.Scalar.ShouldBe(typeof(RequestHandler).FullName);
        record.Dimensions.ShouldNotContain(dimension => dimension.Value.Scalar.Contains("private"));
        record.Failure?.Message.ShouldBeNull();
        profiling.Current.ShouldBeNull();
        profiling.ActiveCount.ShouldBe(0);
    }

    /// <summary>Checks each boundary owns and closes its scope on success or a failed result.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Behavior_IndependentCall_PreservesAndClassifiesResult(int behavior, bool fail)
    {
        var (profiling, sink, clock) = OperationProfilerTests.Create();
        var sut = Behavior(behavior, profiling);
        IResult expected = fail ? Result.Failure("private failure") : Result.Success();
        var calls = 0;

        var result = await sut.HandleAsync(
            new(),
            new PublishOptions(),
            typeof(FirstHandler),
            () =>
            {
                profiling.Current.ShouldNotBeNull();
                calls++;
                clock.Advance(TimeSpan.FromMilliseconds(23));
                return Task.FromResult<IResult>(expected);
            }
        );

        result.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        var record = sink.Records.ShouldHaveSingleItem();
        record.Kind.ShouldBe(
            behavior switch
            {
                0 => "Requester",
                1 => "Notifier",
                _ => "NotifierHandler",
            }
        );
        record.Duration.TotalMilliseconds.ShouldBe(23);
        record.Outcome.ShouldBe(
            fail ? OperationProfilingOutcome.Failed : OperationProfilingOutcome.Completed
        );
        record.Failure?.Message.ShouldBeNull();
        profiling.ActiveCount.ShouldBe(0);
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks repeated calls aggregate within a caller-owned segment without closing the caller.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Behavior_RepeatedNestedCalls_AggregateWithinCaller(int behavior)
    {
        var (profiling, sink, clock) = OperationProfilerTests.Create();
        var sut = Behavior(behavior, profiling);
        using (var owner = profiling.BeginOperation("caller"))
        {
            await owner.RunSegmentAsync(
                "Load",
                async (_, token) =>
                {
                    for (var index = 0; index < 3; index++)
                    {
                        await sut.HandleAsync(
                            new(),
                            new PublishOptions(),
                            typeof(FirstHandler),
                            () =>
                            {
                                clock.Advance(TimeSpan.FromMilliseconds(11));
                                return Task.FromResult<IResult>(Result.Failure());
                            },
                            token
                        );
                    }
                }
            );
            profiling.Current.ShouldBeSameAs(owner);
            sink.Records.ShouldBeEmpty();
            owner.Complete();
        }

        var record = sink.Records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        var child = record.Segments.Single(segment => segment.Path.Components.Count == 2);
        child.Path.Components.First().ShouldBe("Load");
        child.Statistics.Count.ShouldBe(3);
        child.Statistics.TotalDuration.TotalMilliseconds.ShouldBe(33);
        child.Outcomes.ShouldHaveSingleItem().Outcome.ShouldBe(ProfilingSegmentOutcome.Failed);
        profiling.ActiveCount.ShouldBe(0);
    }

    /// <summary>Checks each behavior retains the exact synchronous/asynchronous exception or caller cancellation.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public async Task Behavior_ExceptionOrCancellation_PreservesIdentityAndCleansUp(
        int behavior,
        int mode
    )
    {
        var (profiling, sink, _) = OperationProfilerTests.Create();
        using var cancellation = new CancellationTokenSource();
        if (mode == 2)
        {
            cancellation.Cancel();
        }

        Exception expected =
            mode == 2
                ? new OperationCanceledException(cancellation.Token)
                : new InvalidOperationException("private payload");
        var sut = Behavior(behavior, profiling);
        var calls = 0;
        Task<IResult> Next()
        {
            calls++;
            if (mode == 0)
            {
                throw expected;
            }

            return Task.FromException<IResult>(expected);
        }

        Exception actual = null;
        try
        {
            await sut.HandleAsync(
                new(),
                new PublishOptions(),
                typeof(FirstHandler),
                Next,
                cancellation.Token
            );
        }
        catch (Exception exception)
        {
            actual = exception;
        }

        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        var record = sink.Records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(
            mode == 2 ? OperationProfilingOutcome.Canceled : OperationProfilingOutcome.Failed
        );
        record.Failure?.Message.ShouldBeNull();
        profiling.Current.ShouldBeNull();
        profiling.ActiveBytes.ShouldBe(0);
    }

    /// <summary>Checks disabled capture and caller suppression do not produce replacement roots.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Behavior_DisabledOrSuppressed_ExecutesOnceWithoutRecording(
        int behavior,
        bool suppress
    )
    {
        var (profiling, sink, _) = OperationProfilerTests.Create(options =>
            options.Enabled = suppress
        );
        var sut = Behavior(behavior, profiling);
        IResult expected = Result.Success();
        var calls = 0;
        using (suppress ? profiling.Suppress() : null)
        {
            (
                await sut.HandleAsync(
                    new(),
                    new PublishOptions(),
                    typeof(FirstHandler),
                    () =>
                    {
                        calls++;
                        return Task.FromResult<IResult>(expected);
                    }
                )
            ).ShouldBeSameAs(expected);
        }

        calls.ShouldBe(1);
        sink.Records.ShouldBeEmpty();
        profiling.ActiveCount.ShouldBe(0);
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks startup, metadata, completion and disposal faults never rerun or replace business work.</summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    public async Task Behavior_ObservationFault_PreservesBusinessResult(
        int behavior,
        bool startFault
    )
    {
        var profiling = FaultyProfiler(startFault);
        var sut = Behavior(behavior, profiling);
        IResult expected = Result.Success();
        var calls = 0;

        var result = await sut.HandleAsync(
            new(),
            new PublishOptions(),
            typeof(FirstHandler),
            () =>
            {
                calls++;
                return Task.FromResult<IResult>(expected);
            }
        );

        result.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
    }

    /// <summary>Checks sequential failure stops dispatch while concurrent handlers are all awaited and classified.</summary>
    [Theory]
    [InlineData(ExecutionMode.Sequential, false)]
    [InlineData(ExecutionMode.Sequential, true)]
    [InlineData(ExecutionMode.Concurrent, false)]
    [InlineData(ExecutionMode.Concurrent, true)]
    public async Task Notifier_RealDispatch_ProfilesDistinctHandlersAndDispatchOutcome(
        ExecutionMode mode,
        bool failFirst
    )
    {
        var (profiling, sink, _) = OperationProfilerTests.Create();
        using var cancellation = new CancellationTokenSource();
        var firstCalls = 0;
        var secondCalls = 0;
        var secondEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var services = Services(
            profiling,
            first: async token =>
            {
                token.ShouldBe(cancellation.Token);
                Interlocked.Increment(ref firstCalls);
                if (mode == ExecutionMode.Concurrent)
                {
                    await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }

                return failFirst ? Result.Failure("private handler error") : Result.Success();
            },
            second: token =>
            {
                token.ShouldBe(cancellation.Token);
                Interlocked.Increment(ref secondCalls);
                secondEntered.TrySetResult();
                return Task.FromResult(Result.Success());
            }
        );
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<INotifier>();

        var result = await sut.PublishAsync(
            new TestNotification(),
            new PublishOptions { ExecutionMode = mode },
            cancellation.Token
        );

        result.IsFailure.ShouldBe(failFirst);
        firstCalls.ShouldBe(1);
        var expectedHandlers = mode == ExecutionMode.Sequential && failFirst ? 1 : 2;
        secondCalls.ShouldBe(expectedHandlers - 1);
        var record = sink.Records.ShouldHaveSingleItem();
        record.Key.ShouldBe("notifier:TestNotification");
        record.Kind.ShouldBe("Notifier");
        record.Outcome.ShouldBe(
            failFirst ? OperationProfilingOutcome.Failed : OperationProfilingOutcome.Completed
        );
        record.Segments.Count.ShouldBe(expectedHandlers);
        record.Segments.ShouldAllBe(segment =>
            segment.Statistics.Count == 1 && segment.Path.Components.Count == 1
        );
        record
            .Segments.Select(segment => segment.Key)
            .Distinct()
            .Count()
            .ShouldBe(expectedHandlers);
        var first = record.Segments.Single(segment =>
            segment.Key.EndsWith(typeof(FirstHandler).Name, StringComparison.Ordinal)
        );
        first
            .Outcomes.ShouldHaveSingleItem()
            .Outcome.ShouldBe(
                failFirst ? ProfilingSegmentOutcome.Failed : ProfilingSegmentOutcome.Completed
            );
        record.Failure?.Message.ShouldBeNull();
        profiling.Current.ShouldBeNull();
        profiling.ActiveCount.ShouldBe(0);
    }

    /// <summary>Checks detached handlers can finish after the dispatch and HTTP owner have closed.</summary>
    [Fact]
    public async Task Notifier_FireAndForget_OwnsIndependentHandlerOperation()
    {
        var (profiling, sink, _) = OperationProfilerTests.Create();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerId = Guid.Empty;
        var calls = 0;
        var services = Services(
            profiling,
            first: async _ =>
            {
                handlerId = profiling.Current.Id;
                calls++;
                entered.SetResult();
                await release.Task;
                return Result.Failure("private failure after dispatch");
            },
            second: null
        );
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<INotifier>();
        Guid callerId;
        try
        {
            using (var caller = profiling.BeginOperation("http:caller"))
            {
                callerId = caller.Id;
                (
                    await sut.PublishAsync(
                        new TestNotification(),
                        new PublishOptions { ExecutionMode = ExecutionMode.FireAndForget }
                    )
                ).IsSuccess.ShouldBeTrue();
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                handlerId.ShouldNotBe(callerId);
                profiling.Current.ShouldBeSameAs(caller);
                sink.Records.ShouldBeEmpty();
                caller.Complete();
            }
        }
        finally
        {
            release.TrySetResult();
        }

        await WaitForRecords(sink, 2);
        calls.ShouldBe(1);
        var outer = sink.Records.Single(record => record.Id == callerId);
        outer.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        outer.Segments.ShouldHaveSingleItem().Key.ShouldBe("notifier:TestNotification");
        var handler = sink.Records.Single(record => record.Id == handlerId);
        handler.Kind.ShouldBe("NotifierHandler");
        handler.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        handler.Segments.ShouldBeEmpty();
        handler.Failure.Message.ShouldBeNull();
        profiling.Current.ShouldBeNull();
        profiling.ActiveCount.ShouldBe(0);
    }

    /// <summary>Checks safe independent-boundary initialization and cleanup cannot fail detached handler work.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handler_FireAndForgetBoundaryFault_PreservesResult(bool startFault)
    {
        var profiling = FaultyProfiler(false);
        if (startFault)
        {
            profiling
                .BeginExecutionBoundary()
                .Returns(_ => throw new InvalidOperationException("boundary fault"));
        }
        else
        {
            var restoration = Substitute.For<IDisposable>();
            restoration
                .When(value => value.Dispose())
                .Do(_ => throw new InvalidOperationException("restoration fault"));
            profiling.BeginExecutionBoundary().Returns(restoration);
        }

        IResult expected = Result.Success();
        var calls = 0;
        var sut = Behavior(2, profiling);

        (
            await sut.HandleAsync(
                new(),
                new PublishOptions { ExecutionMode = ExecutionMode.FireAndForget },
                typeof(FirstHandler),
                () =>
                {
                    calls++;
                    return Task.FromResult<IResult>(expected);
                }
            )
        ).ShouldBeSameAs(expected);

        calls.ShouldBe(1);
    }

    private static IPipelineBehavior<TestNotification, IResult> Behavior(
        int behavior,
        IOperationProfiler profiling
    ) =>
        behavior switch
        {
            0 => new ProfilingRequestBehavior<TestNotification, IResult>(profiling),
            1 => new ProfilingNotificationBehavior<TestNotification, IResult>(profiling),
            _ => new ProfilingNotificationHandlerBehavior<TestNotification, IResult>(profiling),
        };

    private static IOperationProfiler FaultyProfiler(bool startFault)
    {
        var profiling = Substitute.For<IOperationProfiler>();
        profiling.Current.Returns((IProfilingOperationScope)null);
        if (startFault)
        {
            profiling
                .BeginOperation(Arg.Any<OperationProfilingStartRequest>())
                .Returns(_ => throw new InvalidOperationException("start fault"));
        }
        else
        {
            var scope = Substitute.For<IProfilingOperationScope>();
            scope.IsRecording.Returns(true);
            scope
                .When(value => value.SetDimension(Arg.Any<string>(), Arg.Any<object>()))
                .Do(_ => throw new InvalidOperationException("metadata fault"));
            scope
                .When(value => value.Complete())
                .Do(_ => throw new InvalidOperationException("completion fault"));
            scope
                .When(value => value.Dispose())
                .Do(_ => throw new InvalidOperationException("disposal fault"));
            profiling.BeginOperation(Arg.Any<OperationProfilingStartRequest>()).Returns(scope);
        }

        return profiling;
    }

    private static ServiceCollection Services(
        IOperationProfiler profiling = null,
        Func<CancellationToken, Task<Result<string>>> request = null,
        Func<CancellationToken, Task<Result>> first = null,
        Func<CancellationToken, Task<Result>> second = null
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddRequester()
            .WithBehavior(typeof(ProfilingRequestBehavior<,>))
            .WithBehavior(typeof(ProfilingRequestBehavior<,>));
        services
            .AddNotifier()
            .WithBehavior(typeof(ProfilingNotificationBehavior<,>))
            .WithBehavior(typeof(ProfilingNotificationHandlerBehavior<,>))
            .WithBehavior(typeof(ProfilingNotificationBehavior<,>))
            .WithBehavior(typeof(ProfilingNotificationHandlerBehavior<,>));
        if (profiling is not null)
        {
            services.AddSingleton(profiling);
        }

        var requestHandler = new RequestHandler(
            request ?? (_ => Task.FromResult(Result<string>.Success("preserved")))
        );
        services.AddSingleton(requestHandler);
        services.AddSingleton<IRequestHandler<TestRequest, string>>(requestHandler);
        services.AddSingleton<INotificationHandler<TestNotification>>(
            new FirstHandler(first ?? (_ => Task.FromResult(Result.Success())))
        );
        if (second is not null)
        {
            services.AddSingleton<INotificationHandler<TestNotification>>(
                new SecondHandler(second)
            );
        }

        return services;
    }

    private static async Task WaitForRecords(OperationProfilerTests.CaptureSink sink, int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (sink.Records.Count < count)
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class TestRequest : RequestBase<string>;

    private sealed class TestNotification : NotificationBase;

    private sealed class RequestHandler(Func<CancellationToken, Task<Result<string>>> next)
        : RequestHandlerBase<TestRequest, string>
    {
        protected override Task<Result<string>> HandleAsync(
            TestRequest request,
            SendOptions options,
            CancellationToken cancellationToken
        ) => next(cancellationToken);
    }

    private sealed class FirstHandler(Func<CancellationToken, Task<Result>> next)
        : NotificationHandlerBase<TestNotification>
    {
        protected override Task<Result> HandleAsync(
            TestNotification notification,
            PublishOptions options,
            CancellationToken cancellationToken
        ) => next(cancellationToken);
    }

    private sealed class SecondHandler(Func<CancellationToken, Task<Result>> next)
        : NotificationHandlerBase<TestNotification>
    {
        protected override Task<Result> HandleAsync(
            TestNotification notification,
            PublishOptions options,
            CancellationToken cancellationToken
        ) => next(cancellationToken);
    }
}
