// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class OrchestrationBehaviorTests(ITestOutputHelper output) : OrchestrationTestBase(output)
{
    [Fact]
    public async Task ExecuteAsync_WhenBehaviorIsRegistered_AppliesToAllOrchestrations()
    {
        var recorder = new BehaviorRecorder();

        await using var sut = this.CreateHarnessBuilder()
            .ConfigureServices(services => services.AddSingleton(recorder))
            .WithBehavior<RecordingOrchestrationBehavior>()
            .WithOrchestration<FirstBehaviorOrchestration>()
            .WithOrchestration<SecondBehaviorOrchestration>()
            .Build();

        var first = await sut.ExecuteAsync<FirstBehaviorOrchestration, BehaviorData>(new BehaviorData());
        var second = await sut.ExecuteAsync<SecondBehaviorOrchestration, BehaviorData>(new BehaviorData());

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        recorder.Entries.ShouldContain(entry =>
            entry.Stage == "before" &&
            entry.OrchestrationName == nameof(FirstBehaviorOrchestration) &&
            entry.ActivityName == "FirstStep" &&
            entry.Kind == OrchestrationActivityExecutionKind.Activity);
        recorder.Entries.ShouldContain(entry =>
            entry.Stage == "before" &&
            entry.OrchestrationName == nameof(SecondBehaviorOrchestration) &&
            entry.ActivityName == "SecondStep" &&
            entry.Kind == OrchestrationActivityExecutionKind.Activity);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActivityRetries_BehaviorReceivesIncrementingAttemptNumbers()
    {
        var recorder = new BehaviorRecorder();

        await using var sut = this.CreateHarnessBuilder()
            .ConfigureServices(services => services.AddSingleton(recorder))
            .WithBehavior<RecordingOrchestrationBehavior>()
            .WithOrchestration<RetryBehaviorOrchestration>()
            .Build();

        var result = await sut.ExecuteAsync<RetryBehaviorOrchestration, BehaviorData>(new BehaviorData());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(nameof(OrchestrationStatus.Completed));
        recorder.Entries
            .Where(entry => entry.Stage == "before" && entry.ActivityName == "RetryStep")
            .Select(entry => entry.Attempt)
            .ShouldBe([1, 2]);
    }

    [Fact]
    public async Task SignalAsync_WhenSignalActivityExecutes_BehaviorWrapsSignalActivity()
    {
        var recorder = new BehaviorRecorder();

        await using var sut = this.CreateHarnessBuilder()
            .ConfigureServices(services => services.AddSingleton(recorder))
            .WithBehavior<RecordingOrchestrationBehavior>()
            .WithOrchestration<SignalBehaviorOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<SignalBehaviorOrchestration, BehaviorData>(new BehaviorData());
        var signal = await sut.SignalAsync(dispatch.Value, "Approve");
        var context = await sut.GetContextAsync<BehaviorData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        signal.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        recorder.Entries.ShouldContain(entry =>
            entry.Stage == "before" &&
            entry.Kind == OrchestrationActivityExecutionKind.SignalActivity &&
            entry.ActivityName == "SignalStep");
    }

    [Fact]
    public async Task DispatchAsync_WhenCompensationExecutes_BehaviorWrapsCompensationActivity()
    {
        var recorder = new BehaviorRecorder();

        await using var sut = this.CreateHarnessBuilder()
            .ConfigureServices(services => services.AddSingleton(recorder))
            .WithBehavior<RecordingOrchestrationBehavior>()
            .WithOrchestration<CompensationBehaviorOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<CompensationBehaviorOrchestration, BehaviorData>(new BehaviorData());
        var context = await sut.GetContextAsync<BehaviorData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        recorder.Entries.ShouldContain(entry =>
            entry.Stage == "before" &&
            entry.Kind == OrchestrationActivityExecutionKind.Compensation &&
            entry.ActivityName == "UndoStep");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDummyBehaviorIsRegistered_RemainsPassThrough()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithBehavior<DummyOrchestrationBehavior>()
            .WithOrchestration<FirstBehaviorOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<FirstBehaviorOrchestration, BehaviorData>(new BehaviorData());

        dispatch.IsSuccess.ShouldBeTrue();
        await sut.Assert(dispatch.Value).BeCompletedAsync("Done");
    }

    [Fact]
    public async Task ExecuteAsync_WhenChaosBehaviorIsRegisteredAndOrchestrationOptsIn_InjectsConfiguredFailure()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithBehavior<ChaosExceptionOrchestrationBehavior>()
            .WithOrchestration<ChaosBehaviorOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ChaosBehaviorOrchestration, BehaviorData>(new BehaviorData());
        var context = await sut.GetContextAsync<BehaviorData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.ShouldNotBeNull();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("chaos injected");
    }

    private sealed class BehaviorRecorder
    {
        private readonly ConcurrentQueue<BehaviorRecord> entries = new();

        public IReadOnlyCollection<BehaviorRecord> Entries => this.entries.ToArray();

        public void Add(BehaviorRecord entry)
        {
            this.entries.Enqueue(entry);
        }
    }

    private sealed record BehaviorRecord(
        string Stage,
        string OrchestrationName,
        string StateName,
        string ActivityName,
        OrchestrationActivityExecutionKind Kind,
        int Attempt);

    private sealed class RecordingOrchestrationBehavior(
        BehaviorRecorder recorder,
        ILoggerFactory loggerFactory = null) : OrchestrationBehaviorBase(loggerFactory)
    {
        public override async Task<OrchestrationOutcome> ExecuteAsync(
            OrchestrationActivityExecutionContext context,
            CancellationToken cancellationToken,
            OrchestrationDelegate next)
        {
            recorder.Add(new BehaviorRecord(
                "before",
                context.OrchestrationName,
                context.StateName,
                context.ActivityName,
                context.Kind,
                context.Attempt));

            var outcome = await next().ConfigureAwait(false);

            recorder.Add(new BehaviorRecord(
                "after",
                context.OrchestrationName,
                context.StateName,
                context.ActivityName,
                context.Kind,
                context.Attempt));

            return outcome;
        }
    }

    private sealed class BehaviorData : IOrchestrationData
    {
        public int Attempts { get; set; }
    }

    private sealed class FirstBehaviorOrchestration : Orchestration<BehaviorData>
    {
        protected override void Define(IOrchestrationBuilder<BehaviorData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()), name: "FirstStep")
                    .TransitionTo("Done"))
                .State("Done", state => state.Complete());
        }
    }

    private sealed class SecondBehaviorOrchestration : Orchestration<BehaviorData>
    {
        protected override void Define(IOrchestrationBuilder<BehaviorData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()), name: "SecondStep")
                    .TransitionTo("Done"))
                .State("Done", state => state.Complete());
        }
    }

    private sealed class RetryBehaviorOrchestration : Orchestration<BehaviorData>
    {
        protected override void Define(IOrchestrationBuilder<BehaviorData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.Attempts++;

                        return Task.FromResult(context.Data.Attempts == 1
                            ? OrchestrationOutcome.Retry("retry")
                            : OrchestrationOutcome.Continue());
                    }, name: "RetryStep")
                    .Complete());
        }
    }

    private sealed class SignalBehaviorOrchestration : Orchestration<BehaviorData>
    {
        protected override void Define(IOrchestrationBuilder<BehaviorData> builder)
        {
            builder
                .State("Waiting", state => state
                    .WaitForSignal("Approve", signal => signal
                        .Activity((context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()), "SignalStep")
                        .TransitionTo("Done")))
                .State("Done", state => state.Complete());
        }
    }

    private sealed class CompensationBehaviorOrchestration : Orchestration<BehaviorData>
    {
        protected override void Define(IOrchestrationBuilder<BehaviorData> builder)
        {
            builder.State("Start", state => state
                .Activity(
                    (context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()),
                    activity => activity.CompensateWith(
                        (context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()),
                        "UndoStep"),
                    "DoStep")
                .Activity(
                    (context, cancellationToken) => Task.FromException<OrchestrationOutcome>(new InvalidOperationException("boom")),
                    name: "FailStep")
                .Complete());
        }
    }

    private sealed class ChaosBehaviorOrchestration : Orchestration<BehaviorData>, IChaosExceptionOrchestration
    {
        ChaosExceptionOrchestrationOptions IChaosExceptionOrchestration.Options => new()
        {
            InjectionRate = 1.0,
            Fault = new ChaosException("chaos injected"),
        };

        protected override void Define(IOrchestrationBuilder<BehaviorData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()), name: "ChaosStep")
                    .TransitionTo("Done"))
                .State("Done", state => state.Complete());
        }
    }
}
