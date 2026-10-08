// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies optional action capture and paired executor slices across retries, waits and compensation.</summary>
/// <example>Uses the real executor and public in-memory provider, with no Runtime session.</example>
public sealed class OrchestrationProfilingBehaviorTests
{
    /// <summary>Faulty instrumentation status and disposal preserve the action result and run it once.</summary>
    [Fact]
    public async Task Behavior_ThrowingScopeStatus_PreservesActionAndOriginalException()
    {
        // Arrange
        var profiling = Substitute.For<IOperationProfiler>();
        profiling.Current.Returns(Substitute.For<IProfilingOperationScope>());
        var scope = Substitute.For<IProfilingSegmentScope>();
        scope.IsRecording.Returns(_ => throw new InvalidOperationException("status"));
        scope.When(value => value.Dispose()).Do(_ => throw new InvalidOperationException("dispose"));
        profiling.BeginSegment(Arg.Any<string>(), Arg.Any<string>()).Returns(scope);
        var sut = new OrchestrationProfilingBehavior(profiling);
        var context = new OrchestrationActivityExecutionContext(Guid.NewGuid(), "Test", "correlation", "Start", "Work", OrchestrationActivityExecutionKind.Activity, 1, Substitute.For<IServiceProvider>(), new object());
        var expected = OrchestrationOutcome.Complete();
        var calls = 0;

        // Act
        var result = await sut.ExecuteAsync(context, default, () => { calls++; return Task.FromResult(expected); });

        // Assert
        result.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        var failure = new InvalidOperationException("business");
        var actual = await Should.ThrowAsync<InvalidOperationException>(() => sut.ExecuteAsync(context, default, () => throw failure));
        actual.ShouldBeSameAs(failure);
    }

    /// <summary>Checks paired registration remains optional and does not install Profiling or duplicate behaviors.</summary>
    [Fact]
    public async Task Registration_OmittedProfiler_PreservesRealWorkflowExecution()
    {
        using var services = await Services(false);
        services.GetService<IOperationProfiler>().ShouldBeNull();
        services.GetServices<IOrchestrationBehavior>().OfType<OrchestrationProfilingBehavior>().ShouldHaveSingleItem();
        var result = await services.GetRequiredService<IOrchestrationService>().ExecuteAsync<RetryWorkflow, Data>(new());
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(nameof(OrchestrationStatus.Completed));
    }

    /// <summary>Checks repeated activity attempts share one owner, aggregate paths and preserve mixed attempt metadata.</summary>
    [Fact]
    public async Task ExecuteAsync_RetryAttempts_AggregatesUnderOneSlice()
    {
        using var services = await Services();
        var result = await services.GetRequiredService<IOrchestrationService>().ExecuteAsync<RetryWorkflow, Data>(new(), correlationId: "retry-correlation");
        result.IsSuccess.ShouldBeTrue();
        var records = await Stored(services);
        var record = records.ShouldHaveSingleItem();
        record.Key.ShouldBe("orchestration:RetryWorkflow");
        record.Kind.ShouldBe("Orchestration");
        record.CorrelationId.ShouldBe("retry-correlation");
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        var action = record.Segments.Single(s => s.Key == "action:Activity:Work");
        action.Statistics.Count.ShouldBe(2);
        action.Path.Components.ShouldBe(new[] { "state:Start", "action:Activity:Work" });
        action.Dimensions.Single(d => d.Key == "orchestration.attempt").Mixed.ShouldBeTrue();
        services.GetRequiredService<IOperationProfiler>().Current.ShouldBeNull();
    }

    /// <summary>Checks waiting closes a slice, and a resumed signal creates a distinct correlated profile.</summary>
    [Fact]
    public async Task ExecuteAsync_WaitThenSignal_CreatesSeparateCorrelatedSlices()
    {
        using var services = await Services();
        var executor = services.GetRequiredService<IOrchestrationService>();
        var waiting = await services.GetRequiredService<IOrchestrationExecutor>().ExecuteAsync<WaitingWorkflow, Data>(new());
        waiting.Status.ShouldBe(OrchestrationStatus.Waiting);
        var first = (await Stored(services)).ShouldHaveSingleItem();
        first.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        first.Segments.ShouldBeEmpty();
        services.GetRequiredService<IOperationProfiler>().Current.ShouldBeNull();
        var signal = await executor.SignalAsync(waiting.InstanceId, "Approve");
        signal.IsSuccess.ShouldBeTrue();
        var records = await Stored(services);
        records.Count.ShouldBe(2);
        var resumed = records.Single(r => r.Id != first.Id);
        resumed.CorrelationId.ShouldBe(first.CorrelationId);
        resumed.Segments.ShouldContain(s => s.Key == "action:SignalActivity:Approve");
        resumed.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    /// <summary>Checks failed actions and compensation remain in the same failed executor slice.</summary>
    [Fact]
    public async Task ExecuteAsync_FailureAndCompensation_PreservesFailedAndCompletedSegments()
    {
        using var services = await Services();
        var result = await services.GetRequiredService<IOrchestrationService>().ExecuteAsync<CompensationWorkflow, Data>(new());
        result.Value.Status.ShouldBe(nameof(OrchestrationStatus.Failed));
        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Segments.ShouldContain(s => s.Key.Contains("Compensation", StringComparison.Ordinal));
        record.Segments.Single(s => s.Key == "action:Activity:Fail").Outcomes.ShouldContain(s => s.Outcome == ProfilingSegmentOutcome.Failed);
        record.Failure.Message.ShouldBeNull();
    }

    /// <summary>Checks joined workflows retain caller ownership and suppression forbids fallback roots.</summary>
    [Fact]
    public async Task ExecuteAsync_JoinedAndSuppressed_PreservesOuterOwnership()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var executor = services.GetRequiredService<IOrchestrationService>();
        using var owner = profiling.BeginOperation("caller");
        await executor.ExecuteAsync<RetryWorkflow, Data>(new());
        owner.IsRecording.ShouldBeTrue();
        profiling.Current.ShouldBeSameAs(owner);
        owner.Complete(); owner.Dispose();
        using (profiling.Suppress()) { await executor.ExecuteAsync<RetryWorkflow, Data>(new()); }

        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Key.ShouldBe("caller");
        record.Segments.ShouldContain(s => s.Key == "orchestration:RetryWorkflow");
        record.Segments.Single(s => s.Key == "action:Activity:Work").Path.Components.ShouldBe(new[] { "orchestration:RetryWorkflow", "state:Start", "action:Activity:Work" });
    }

    /// <summary>Checks fallback actions outside the executor still own one bounded root and delegate once.</summary>
    [Theory]
    [InlineData(OrchestrationOutcomeKind.Wait, OperationProfilingOutcome.Completed)]
    [InlineData(OrchestrationOutcomeKind.Cancel, OperationProfilingOutcome.Canceled)]
    [InlineData(OrchestrationOutcomeKind.Retry, OperationProfilingOutcome.Completed)]
    public async Task Behavior_IndependentAction_RecordsActualDomainOutcome(OrchestrationOutcomeKind outcome, OperationProfilingOutcome expected)
    {
        using var services = await Services();
        var sut = new OrchestrationProfilingBehavior(services.GetRequiredService<IOperationProfiler>());
        var context = new OrchestrationActivityExecutionContext(Guid.NewGuid(), "Independent", "action-correlation", "Start", "Work", OrchestrationActivityExecutionKind.Activity, 1, services, new object());
        var result = new OrchestrationOutcome(outcome);
        var calls = 0;
        var actual = await sut.ExecuteAsync(context, default, () => { calls++; return Task.FromResult(result); });
        actual.ShouldBeSameAs(result);
        calls.ShouldBe(1);
        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Key.ShouldBe("orchestration:Independent");
        record.Outcome.ShouldBe(expected);
        record.Segments.Count.ShouldBe(2);
    }

    /// <summary>Checks thrown action errors preserve identity and leave no ambient owner.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Behavior_ThrownException_PreservesExceptionAndClosesScopes(bool asynchronous)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var sut = new OrchestrationProfilingBehavior(profiling);
        var context = new OrchestrationActivityExecutionContext(Guid.NewGuid(), "Independent", "correlation", "Start", "Work", OrchestrationActivityExecutionKind.Activity, 1, services, new object());
        var expected = new InvalidOperationException("private failure detail");
        var calls = 0;
        var actual = await Should.ThrowAsync<InvalidOperationException>(() => sut.ExecuteAsync(context, default, () =>
        {
            calls++;
            return asynchronous ? Task.FromException<OrchestrationOutcome>(expected) : throw expected;
        }));
        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        profiling.Current.ShouldBeNull();
        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Segments.ShouldAllBe(s => s.Outcomes.Single().Outcome == ProfilingSegmentOutcome.Failed);
    }

    /// <summary>Checks cancellation is preserved by both the outer slice and activity segments.</summary>
    [Fact]
    public async Task Behavior_CanceledAction_PreservesCancellation()
    {
        using var services = await Services();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var sut = new OrchestrationProfilingBehavior(profiling);
        var context = new OrchestrationActivityExecutionContext(Guid.NewGuid(), "Independent", "correlation", "Start", "Work", OrchestrationActivityExecutionKind.Activity, 1, services, new object());
        var expected = new OperationCanceledException(cancellation.Token);
        OperationCanceledException actual = null;
        try
        {
            await sut.ExecuteAsync(context, cancellation.Token, () => Task.FromException<OrchestrationOutcome>(expected));
        }
        catch (OperationCanceledException caught) { actual = caught; }

        actual.ShouldBeSameAs(expected);
        var record = (await Stored(services)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Canceled);
        record.Segments.ShouldAllBe(s => s.Outcomes.Single().Outcome == ProfilingSegmentOutcome.Canceled);
    }

    /// <summary>Checks concurrent independent executor calls retain distinct root ownership.</summary>
    [Fact]
    public async Task ExecuteAsync_ConcurrentSlices_RecordDistinctRoots()
    {
        using var services = await Services();
        var executor = services.GetRequiredService<IOrchestrationService>();
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => executor.ExecuteAsync<RetryWorkflow, Data>(new(), correlationId: "parallel-" + i)));
        results.ShouldAllBe(r => r.IsSuccess);
        var records = await Stored(services);
        records.Count.ShouldBe(5);
        records.Select(r => r.Id).Distinct().Count().ShouldBe(5);
        records.ShouldAllBe(r => r.Segments.Single(s => s.Key == "action:Activity:Work").Statistics.Count == 2);
        services.GetRequiredService<IOperationProfiler>().Current.ShouldBeNull();
    }

    private static async Task<ServiceProvider> Services(bool capture = true)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddSingleton(new OrchestrationExecutionSettings { EnableBackgroundExecution = false });
        collection.AddOrchestrations().AliveEnabled(false).WithProfilingBehavior().WithProfilingBehavior()
            .WithOrchestration<RetryWorkflow>().WithOrchestration<WaitingWorkflow>().WithOrchestration<CompensationWorkflow>();
        if (capture) { collection.AddProfiling(o => o.Enabled()).WithOperationProfiling(); }

        var services = collection.BuildServiceProvider();
        if (capture) { await services.GetRequiredService<OperationProfilingWriterService>().TickAsync(); }

        return services;
    }

    private static async Task<IReadOnlyList<OperationProfilingRecord>> Stored(IServiceProvider services)
    {
        await services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var result = await services.GetRequiredService<IOperationProfilingStore>().QueryAsync(new()
        {
            FromUtc = DateTimeOffset.UtcNow.AddHours(-1), ToUtc = DateTimeOffset.UtcNow.AddHours(1),
            Outcomes = Enum.GetValues<OperationProfilingOutcome>(), View = OperationProfilingView.Recent,
        });
        result.IsSuccess.ShouldBeTrue();
        return result.Value.Records;
    }

    private sealed class Data : IOrchestrationData
    {
        /// <summary>Gets or sets the fixture's actual action attempt count.</summary>
        public int Attempts { get; set; }
    }

    private sealed class RetryWorkflow : Orchestration<Data>
    {
        /// <inheritdoc />
        protected override void Define(IOrchestrationBuilder<Data> builder) => builder.State("Start", state => state
            .Activity((context, _) => Task.FromResult(++context.Data.Attempts == 1 ? OrchestrationOutcome.Retry() : OrchestrationOutcome.Continue()), name: "Work")
            .Complete());
    }

    private sealed class WaitingWorkflow : Orchestration<Data>
    {
        /// <inheritdoc />
        protected override void Define(IOrchestrationBuilder<Data> builder) => builder
            .State("Waiting", state => state.WaitForSignal("Approve", signal => signal
                .Activity((_, _) => Task.FromResult(OrchestrationOutcome.Continue()), "Approve").TransitionTo("Done")))
            .State("Done", state => state.Complete());
    }

    private sealed class CompensationWorkflow : Orchestration<Data>
    {
        /// <inheritdoc />
        protected override void Define(IOrchestrationBuilder<Data> builder) => builder.State("Start", state => state
            .Activity((_, _) => Task.FromResult(OrchestrationOutcome.Continue()),
                activity => activity.CompensateWith((_, _) => Task.FromResult(OrchestrationOutcome.Continue()), "Undo"), "Do")
            .Activity((_, _) => Task.FromException<OrchestrationOutcome>(new InvalidOperationException("raw failure")), name: "Fail")
            .Complete());
    }
}
