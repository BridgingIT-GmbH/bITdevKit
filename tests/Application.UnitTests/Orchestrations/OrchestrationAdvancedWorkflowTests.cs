// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public partial class OrchestrationAdvancedWorkflowTests(ITestOutputHelper output) : OrchestrationTestBase(output)
{
    [Fact]
    public async Task DispatchAsync_WhenCompensatingActivitiesExist_ExecutesCompensationsInReverseOrder()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<CompensationOrderingOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<CompensationOrderingOrchestration, AdvancedWorkflowData>(new AdvancedWorkflowData());
        await sut.ContinueAllAsync();
        var context = await sut.GetContextAsync<AdvancedWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.Data.Trace.Take(5).ShouldBe([
            "step-a",
            "step-b",
            "step-fail",
            "undo-b",
            "undo-a",
        ]);
        context.Data.Trace.Count(item => item == "undo-a").ShouldBe(1);
        context.Data.Trace.Count(item => item == "undo-b").ShouldBe(1);
        history.Count(item => item.EventType == "CompensationCompleted").ShouldBe(2);
    }

    [Fact]
    public async Task DispatchAsync_WhenChildOrchestrationIsConfigured_StartsAndWaitsForChildLifecycle()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<ParentWorkflowOrchestration>()
            .WithOrchestration<ImmediateChildWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ParentWorkflowOrchestration, ParentWorkflowData>(new ParentWorkflowData());
        var parentContext = await sut.GetContextAsync<ParentWorkflowData>(dispatch.Value);
        var childInstance = await sut.GetInstanceAsync(parentContext.Data.ChildInstanceId);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        parentContext.Status.ShouldBe(OrchestrationStatus.Completed);
        parentContext.Data.ChildInstanceId.ShouldNotBe(Guid.Empty);
        parentContext.Data.Trace.ShouldBe(["parent-after-child"]);
        childInstance.ShouldNotBeNull();
        childInstance.Status.ShouldBe(OrchestrationStatus.Completed);
        history.ShouldContain(item => item.EventType == "ChildOrchestrationStarted");
        history.ShouldContain(item => item.EventType == "ChildOrchestrationCompleted");
    }

    [Fact]
    public async Task DispatchAsync_WhenParallelBranchesRun_CompletesAfterAllBranchesFinish()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<ParallelWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ParallelWorkflowOrchestration, ParallelWorkflowData>(new ParallelWorkflowData());
        var context = await sut.GetContextAsync<ParallelWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.LeftCount.ShouldBe(1);
        context.Data.RightCount.ShouldBe(1);
        context.Data.TotalCount.ShouldBe(2);
        history.ShouldContain(item => item.EventType == "ParallelJoinResolved");
    }

    [Fact]
    public async Task DispatchAsync_WhenLoopConditionBecomesFalse_StopsAfterExpectedIterations()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<LoopWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<LoopWorkflowOrchestration, LoopWorkflowData>(new LoopWorkflowData());
        var context = await sut.GetContextAsync<LoopWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.Counter.ShouldBe(3);
        history.Count(item => item.EventType == "LoopIterationCompleted").ShouldBe(3);
    }

    [Fact]
    public async Task AdvanceTimeAsync_WhenRetryPolicyUsesDurableDelay_RetriesUntilSuccess()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<RetryPolicyWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<RetryPolicyWorkflowOrchestration, RetryWorkflowData>(new RetryWorkflowData());
        await sut.Assert(dispatch.Value).BeWaitingAsync("Retrying");

        await sut.AdvanceTimeAsync(TimeSpan.FromMinutes(1));
        await sut.Assert(dispatch.Value).BeWaitingAsync("Retrying");

        await sut.AdvanceTimeAsync(TimeSpan.FromMinutes(1));

        var context = await sut.GetContextAsync<RetryWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.Attempts.ShouldBe(3);
        history.Count(item => item.EventType == "ActivityRetryScheduled").ShouldBe(2);
    }

    [Fact]
    public async Task SignalAsync_WhenApprovalHelperReceivesApprovalSignal_TransitionsAndMapsPayload()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<ApprovalHelperWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ApprovalHelperWorkflowOrchestration, ApprovalWorkflowData>(new ApprovalWorkflowData());
        await sut.Assert(dispatch.Value).BeWaitingAsync("AwaitingApproval");

        var signal = await sut.SignalAsync(dispatch.Value, "Approve", new ApprovalDecisionSignal
        {
            UserId = "manager-1",
            Comment = "approved",
        });

        var context = await sut.GetContextAsync<ApprovalWorkflowData>(dispatch.Value);

        signal.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.CurrentState.ShouldBe("Approved");
        context.Data.ApprovedBy.ShouldBe("manager-1");
        context.Data.ApprovalComment.ShouldBe("approved");
    }

    [Fact]
    public async Task SignalAsync_WhenHumanTaskHelperCompletes_TransitionsAndCapturesOutcome()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<HumanTaskWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<HumanTaskWorkflowOrchestration, HumanTaskWorkflowData>(new HumanTaskWorkflowData());
        await sut.Assert(dispatch.Value).BeWaitingAsync("AwaitingTask");

        var signal = await sut.SignalAsync(dispatch.Value, "TaskDone", new HumanTaskResolutionSignal
        {
            UserId = "worker-1",
            Outcome = "done",
            Comment = "completed",
        });

        var context = await sut.GetContextAsync<HumanTaskWorkflowData>(dispatch.Value);

        signal.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.CurrentState.ShouldBe("Completed");
        context.Data.CompletedBy.ShouldBe("worker-1");
        context.Data.TaskOutcome.ShouldBe("done");
        context.Data.TaskComment.ShouldBe("completed");
    }

    private sealed class AdvancedWorkflowData : IOrchestrationData
    {
        public List<string> Trace { get; set; } = [];
    }

    private sealed class ParentWorkflowData : IOrchestrationData
    {
        public Guid ChildInstanceId { get; set; }

        public List<string> Trace { get; set; } = [];
    }

    private sealed class ChildWorkflowData : IOrchestrationData
    {
        public bool Completed { get; set; }
    }

    private sealed class ParallelWorkflowData : IOrchestrationData
    {
        public int LeftCount { get; set; }

        public int RightCount { get; set; }

        public int TotalCount { get; set; }
    }

    private sealed class LoopWorkflowData : IOrchestrationData
    {
        public int Counter { get; set; }
    }

    private sealed class RetryWorkflowData : IOrchestrationData
    {
        public int Attempts { get; set; }
    }

    private sealed class ApprovalWorkflowData : IOrchestrationData
    {
        public string ApprovedBy { get; set; }

        public string ApprovalComment { get; set; }
    }

    private sealed class HumanTaskWorkflowData : IOrchestrationData
    {
        public string CompletedBy { get; set; }

        public string TaskOutcome { get; set; }

        public string TaskComment { get; set; }
    }

    private sealed class CompensationOrderingOrchestration : Orchestration<AdvancedWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<AdvancedWorkflowData> builder)
        {
            builder.State("Created", state => state
                .Activity(
                    (context, cancellationToken) =>
                    {
                        context.Data.Trace.Add("step-a");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    },
                    activity => activity.CompensateWith(
                        (context, cancellationToken) =>
                        {
                            context.Data.Trace.Add("undo-a");
                            return Task.FromResult(OrchestrationOutcome.Continue());
                        },
                        "UndoA"),
                    "StepA")
                .Activity(
                    (context, cancellationToken) =>
                    {
                        context.Data.Trace.Add("step-b");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    },
                    activity => activity.CompensateWith(
                        (context, cancellationToken) =>
                        {
                            context.Data.Trace.Add("undo-b");
                            return Task.FromResult(OrchestrationOutcome.Continue());
                        },
                        "UndoB"),
                    "StepB")
                .Activity(
                    (context, cancellationToken) =>
                    {
                        context.Data.Trace.Add("step-fail");
                        throw new InvalidOperationException("fail");
                    },
                    "Fail"));
        }
    }

    private sealed class ParentWorkflowOrchestration : Orchestration<ParentWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<ParentWorkflowData> builder)
        {
            builder.State("Created", state => state
                .StartChildOrchestrationActivity<ParentWorkflowData, ImmediateChildWorkflowOrchestration, ChildWorkflowData>(
                    child => child
                        .WithData(context => new ChildWorkflowData())
                        .StoreInstanceId((context, instanceId) => context.Data.ChildInstanceId = instanceId)
                        .WaitForCompletion(),
                    "RunChild")
                .TransformActivity(context => context.Data.Trace.Add("parent-after-child"), "AfterChild")
                .Complete());
        }
    }

    private sealed class ImmediateChildWorkflowOrchestration : Orchestration<ChildWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<ChildWorkflowData> builder)
        {
            builder.State("Created", state => state
                .TransformActivity(context => context.Data.Completed = true, "ChildComplete")
                .Complete());
        }
    }

    private sealed class ParallelWorkflowOrchestration : Orchestration<ParallelWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<ParallelWorkflowData> builder)
        {
            builder.State("Created", state => state
                .Parallel(parallel => parallel
                    .Branch("Left", branch => branch.Activity(
                        (context, cancellationToken) =>
                        {
                            context.Data.LeftCount++;
                            return Task.FromResult(OrchestrationOutcome.Continue());
                        },
                        "LeftStep"))
                    .Branch("Right", branch => branch.Activity(
                        (context, cancellationToken) =>
                        {
                            context.Data.RightCount++;
                            return Task.FromResult(OrchestrationOutcome.Continue());
                        },
                        "RightStep"))
                    .JoinAll(),
                    "JoinWork")
                .TransformActivity(context => context.Data.TotalCount = context.Data.LeftCount + context.Data.RightCount, "CalculateTotal")
                .Complete());
        }
    }

    private sealed class LoopWorkflowOrchestration : Orchestration<LoopWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<LoopWorkflowData> builder)
        {
            builder.State("Created", state => state
                .Loop("Repeat", loop => loop
                    .While(context => context.Data.Counter < 3)
                    .Activity(
                        (context, cancellationToken) =>
                        {
                            context.Data.Counter++;
                            return Task.FromResult(OrchestrationOutcome.Continue());
                        },
                        "Increment"))
                .Complete());
        }
    }

    private sealed class RetryPolicyWorkflowOrchestration : Orchestration<RetryWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<RetryWorkflowData> builder)
        {
            builder.State("Retrying", state => state
                .Activity(
                    (context, cancellationToken) =>
                    {
                        context.Data.Attempts++;
                        return Task.FromResult(context.Data.Attempts < 3
                            ? OrchestrationOutcome.Retry("retry")
                            : OrchestrationOutcome.Continue());
                    },
                    activity => activity.Retry(new OrchestrationRetryPolicy
                    {
                        MaxAttempts = 3,
                        Delay = TimeSpan.FromMinutes(1),
                        BackoffMode = OrchestrationRetryBackoffMode.FixedDelay,
                    }),
                    "RetryStep")
                .Complete());
        }
    }

    private sealed class ApprovalHelperWorkflowOrchestration : Orchestration<ApprovalWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<ApprovalWorkflowData> builder)
        {
            builder
                .State("AwaitingApproval", state => state
                    .ApprovalActivity(approval => approval
                        .Title(context => "Approve request")
                        .ApprovedSignal("Approve")
                        .RejectedSignal("Reject")
                        .OnApproved((context, payload) =>
                        {
                            context.Data.ApprovedBy = payload.UserId;
                            context.Data.ApprovalComment = payload.Comment;
                        })
                        .ApprovedTransition("Approved")
                        .RejectedTransition("Rejected")))
                .State("Approved", state => state
                    .Complete())
                .State("Rejected", state => state
                    .Terminate("Rejected"));
        }
    }

    private sealed class HumanTaskWorkflowOrchestration : Orchestration<HumanTaskWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<HumanTaskWorkflowData> builder)
        {
            builder
                .State("AwaitingTask", state => state
                    .HumanTaskActivity(task => task
                        .Title(context => "Complete task")
                        .Description("Finish work item")
                        .CompletedSignal("TaskDone")
                        .CancelledSignal("TaskCancelled")
                        .OnCompleted((context, payload) =>
                        {
                            context.Data.CompletedBy = payload.UserId;
                            context.Data.TaskOutcome = payload.Outcome;
                            context.Data.TaskComment = payload.Comment;
                        })
                        .CompletedTransition("Completed")
                        .CancelledTransition("Cancelled")))
                .State("Completed", state => state
                    .Complete())
                .State("Cancelled", state => state
                    .Terminate("Cancelled"));
        }
    }

}
