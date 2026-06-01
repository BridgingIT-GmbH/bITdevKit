// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public partial class OrchestrationAdvancedWorkflowTests
{
    [Fact]
    public async Task DispatchAsync_WhenParallelCheckpointIsRecovered_DoesNotReplayCompletedBranches()
    {
        var probe = new ReplayProbe();
        var provider = new LeaseFaultingPersistenceProvider("ParallelBranchActivityExecuted", triggerOccurrence: 1);

        await using var sut = this.CreateHarnessBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(probe);
                services.AddSingleton<IOrchestrationStorageProvider>(provider);
            })
            .WithOrchestration<ParallelCheckpointRecoveryOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ParallelCheckpointRecoveryOrchestration, ReplayWorkflowData>(new ReplayWorkflowData());
        await sut.ContinueAllAsync();

        var context = await sut.GetContextAsync<ReplayWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        probe.LeftExecutions.ShouldBe(1);
        probe.RightExecutions.ShouldBe(1);
        history.Count(item => item.EventType == "ParallelBranchActivityExecuted").ShouldBe(2);
        history.Count(item => item.EventType == "ParallelJoinResolved").ShouldBe(1);
    }

    [Fact]
    public async Task DispatchAsync_WhenLoopCheckpointIsRecovered_DoesNotReplayCompletedIterations()
    {
        var probe = new ReplayProbe();
        var provider = new LeaseFaultingPersistenceProvider("LoopIterationCompleted", triggerOccurrence: 1);

        await using var sut = this.CreateHarnessBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(probe);
                services.AddSingleton<IOrchestrationStorageProvider>(provider);
            })
            .WithOrchestration<LoopCheckpointRecoveryOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<LoopCheckpointRecoveryOrchestration, ReplayWorkflowData>(new ReplayWorkflowData());
        await sut.ContinueAllAsync();

        var context = await sut.GetContextAsync<ReplayWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.IterationCount.ShouldBe(2);
        probe.LoopExecutions.ShouldBe(2);
        history.Count(item => item.EventType == "LoopIterationCompleted").ShouldBe(2);
    }

    [Fact]
    public async Task DispatchAsync_WhenChildHelperReentersSameState_StartsFreshChildInstance()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<ChildStateReentryOrchestration>()
            .WithOrchestration<ReentryChildOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ChildStateReentryOrchestration, ChildReentryData>(new ChildReentryData());
        var context = await sut.GetContextAsync<ChildReentryData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.VisitCount.ShouldBe(2);
        context.Data.ChildInstanceIds.Count.ShouldBe(2);
        context.Data.ChildInstanceIds.Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task DispatchAsync_WhenParallelHelperReentersSameState_RerunsBranches()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<ParallelStateReentryOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ParallelStateReentryOrchestration, ParallelReentryData>(new ParallelReentryData());
        var context = await sut.GetContextAsync<ParallelReentryData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.VisitCount.ShouldBe(2);
        context.Data.LeftCount.ShouldBe(2);
        context.Data.RightCount.ShouldBe(2);
    }

    [Fact]
    public async Task SignalAsync_WhenApprovalHelperReentersSameState_RecreatesPendingMetadata()
    {
        await using var sut = this.CreateHarnessBuilder()
            .WithOrchestration<ApprovalStateReentryOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ApprovalStateReentryOrchestration, ApprovalReentryData>(new ApprovalReentryData());
        await sut.Assert(dispatch.Value).BeWaitingAsync("AwaitingApproval");

        var firstSignal = await sut.SignalAsync(dispatch.Value, "Approve", new ApprovalDecisionSignal
        {
            UserId = "manager-1",
            Comment = "first-pass",
        });

        var waitingContext = await sut.GetContextAsync<ApprovalReentryData>(dispatch.Value);
        var approvalKey = waitingContext.Properties.Keys
            .Single(key => key.StartsWith("__orchestration.approval.AwaitingApproval:2:", StringComparison.OrdinalIgnoreCase));
        var approvalState = waitingContext.Properties[approvalKey];

        firstSignal.IsSuccess.ShouldBeTrue();
        waitingContext.Status.ShouldBe(OrchestrationStatus.Waiting);
        waitingContext.CurrentState.ShouldBe("AwaitingApproval");
        GetStringProperty(approvalState, "Status").ShouldBe("Pending");

        var secondSignal = await sut.SignalAsync(dispatch.Value, "Approve", new ApprovalDecisionSignal
        {
            UserId = "manager-2",
            Comment = "second-pass",
        });

        var completedContext = await sut.GetContextAsync<ApprovalReentryData>(dispatch.Value);

        secondSignal.IsSuccess.ShouldBeTrue();
        completedContext.Status.ShouldBe(OrchestrationStatus.Completed);
        completedContext.Data.VisitCount.ShouldBe(2);
        completedContext.Data.ApprovedBy.ShouldBe("manager-2");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationCompensationFails_ReportsFailedConsistently()
    {
        using var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<CancelCompensationFailureOrchestration>());
        var runtime = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var execute = await runtime.ExecuteAsync<CancelCompensationFailureOrchestration, CompensationFailureData>(new CompensationFailureData());
        var wait = await runtime.DispatchAndWaitAsync<CancelCompensationFailureOrchestration, CompensationFailureData>(
            new CompensationFailureData(),
            waitFor: WaitFor.Completion(),
            timeout: TimeSpan.FromSeconds(2));
        var snapshot = await queries.GetInstanceAsync(wait.Value.InstanceId);
        var history = await queries.GetHistoryAsync(wait.Value.InstanceId);

        execute.IsSuccess.ShouldBeTrue();
        execute.Value.Status.ShouldBe(nameof(OrchestrationStatus.Failed));
        execute.Value.Outcome.ShouldBe(nameof(OrchestrationStatus.Failed));
        wait.IsSuccess.ShouldBeTrue();
        wait.Value.Status.ShouldBe(nameof(OrchestrationStatus.Failed));
        wait.Value.Outcome.ShouldBe(nameof(OrchestrationStatus.Failed));
        snapshot.Status.ShouldBe(OrchestrationStatus.Failed);
        history.Last().EventType.ShouldBe("Failed");
        history.ShouldNotContain(item => item.EventType == "Cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTerminationCompensationFails_ReportsFailedConsistently()
    {
        using var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<TerminateCompensationFailureOrchestration>());
        var runtime = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var execute = await runtime.ExecuteAsync<TerminateCompensationFailureOrchestration, CompensationFailureData>(new CompensationFailureData());
        var wait = await runtime.DispatchAndWaitAsync<TerminateCompensationFailureOrchestration, CompensationFailureData>(
            new CompensationFailureData(),
            waitFor: WaitFor.Completion(),
            timeout: TimeSpan.FromSeconds(2));
        var snapshot = await queries.GetInstanceAsync(wait.Value.InstanceId);
        var history = await queries.GetHistoryAsync(wait.Value.InstanceId);

        execute.IsSuccess.ShouldBeTrue();
        execute.Value.Status.ShouldBe(nameof(OrchestrationStatus.Failed));
        execute.Value.Outcome.ShouldBe(nameof(OrchestrationStatus.Failed));
        wait.IsSuccess.ShouldBeTrue();
        wait.Value.Status.ShouldBe(nameof(OrchestrationStatus.Failed));
        wait.Value.Outcome.ShouldBe(nameof(OrchestrationStatus.Failed));
        snapshot.Status.ShouldBe(OrchestrationStatus.Failed);
        history.Last().EventType.ShouldBe("Failed");
        history.ShouldNotContain(item => item.EventType == "Terminated");
    }

    private ServiceProvider CreateServices(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        services.AddSingleton(new OrchestrationExecutionSettings { EnableBackgroundExecution = false });
        configure(services);
        return services.BuildServiceProvider();
    }

    private static string GetStringProperty(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName)?.GetValue(instance)?.ToString();
    }

    private sealed class ReplayProbe
    {
        public int LeftExecutions;

        public int RightExecutions;

        public int LoopExecutions;
    }

    private sealed class ReplayWorkflowData : IOrchestrationData
    {
        public int IterationCount { get; set; }
    }

    private sealed class ChildReentryData : IOrchestrationData
    {
        public int VisitCount { get; set; }

        public List<Guid> ChildInstanceIds { get; set; } = [];
    }

    private sealed class ParallelReentryData : IOrchestrationData
    {
        public int VisitCount { get; set; }

        public int LeftCount { get; set; }

        public int RightCount { get; set; }
    }

    private sealed class ApprovalReentryData : IOrchestrationData
    {
        public int VisitCount { get; set; }

        public string ApprovedBy { get; set; }
    }

    private sealed class CompensationFailureData : IOrchestrationData
    {
    }

    private sealed class ParallelCheckpointRecoveryOrchestration : Orchestration<ReplayWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<ReplayWorkflowData> builder)
        {
            builder.State("Created", state => state
                .Parallel(parallel => parallel
                    .Branch("Left", branch => branch.Activity(
                        (context, cancellationToken) =>
                        {
                            Interlocked.Increment(ref context.Services.GetRequiredService<ReplayProbe>().LeftExecutions);
                            return Task.FromResult(OrchestrationOutcome.Continue());
                        },
                        "LeftStep"))
                    .Branch("Right", branch => branch.Activity(
                        (context, cancellationToken) =>
                        {
                            Interlocked.Increment(ref context.Services.GetRequiredService<ReplayProbe>().RightExecutions);
                            return Task.FromResult(OrchestrationOutcome.Continue());
                        },
                        "RightStep"))
                    .JoinAll(),
                    "CheckpointedParallel")
                .Complete());
        }
    }

    private sealed class LoopCheckpointRecoveryOrchestration : Orchestration<ReplayWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<ReplayWorkflowData> builder)
        {
            builder.State("Created", state => state
                .Loop("CheckpointedLoop", loop => loop
                    .While(context => context.Data.IterationCount < 2)
                    .Activity(
                        (context, cancellationToken) =>
                        {
                            Interlocked.Increment(ref context.Services.GetRequiredService<ReplayProbe>().LoopExecutions);
                            context.Data.IterationCount++;
                            return Task.FromResult(OrchestrationOutcome.Continue());
                        },
                        "LoopStep"))
                .Complete());
        }
    }

    private sealed class ChildStateReentryOrchestration : Orchestration<ChildReentryData>
    {
        protected override void Define(IOrchestrationBuilder<ChildReentryData> builder)
        {
            builder
                .State("Review", state => state
                    .StartChildOrchestrationActivity<ChildReentryData, ReentryChildOrchestration, ChildWorkflowData>(
                        child => child
                            .WithData(context => new ChildWorkflowData())
                            .StoreInstanceId((context, instanceId) => context.Data.ChildInstanceIds.Add(instanceId))
                            .WaitForCompletion(),
                        "RunChild")
                    .TransformActivity(context => context.Data.VisitCount++, "CountVisit")
                    .TransitionTo("Review", context => context.Data.VisitCount < 2)
                    .TransitionTo("Done", context => context.Data.VisitCount >= 2))
                .State("Done", state => state
                    .Complete());
        }
    }

    private sealed class ReentryChildOrchestration : Orchestration<ChildWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<ChildWorkflowData> builder)
        {
            builder.State("Created", state => state
                .TransformActivity(context => context.Data.Completed = true, "ChildDone")
                .Complete());
        }
    }

    private sealed class ParallelStateReentryOrchestration : Orchestration<ParallelReentryData>
    {
        protected override void Define(IOrchestrationBuilder<ParallelReentryData> builder)
        {
            builder
                .State("Review", state => state
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
                        "ReentryParallel")
                    .TransformActivity(context => context.Data.VisitCount++, "CountVisit")
                    .TransitionTo("Review", context => context.Data.VisitCount < 2)
                    .TransitionTo("Done", context => context.Data.VisitCount >= 2))
                .State("Done", state => state
                    .Complete());
        }
    }

    private sealed class ApprovalStateReentryOrchestration : Orchestration<ApprovalReentryData>
    {
        protected override void Define(IOrchestrationBuilder<ApprovalReentryData> builder)
        {
            builder
                .State("AwaitingApproval", state => state
                    .ApprovalActivity(approval => approval
                        .Title(context => "Approve request")
                        .ApprovedSignal("Approve")
                        .RejectedSignal("Reject")
                        .OnApproved((context, payload) =>
                        {
                            context.Data.VisitCount++;
                            context.Data.ApprovedBy = payload.UserId;
                        })
                        .ApprovedTransition("Decide")
                        .RejectedTransition("Rejected")))
                .State("Decide", state => state
                    .TransitionTo("AwaitingApproval", context => context.Data.VisitCount < 2)
                    .TransitionTo("Done", context => context.Data.VisitCount >= 2))
                .State("Done", state => state
                    .Complete())
                .State("Rejected", state => state
                    .Terminate("Rejected"));
        }
    }

    private sealed class CancelCompensationFailureOrchestration : Orchestration<CompensationFailureData>
    {
        protected override void Define(IOrchestrationBuilder<CompensationFailureData> builder)
        {
            builder.State("Created", state => state
                .Activity(
                    (context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()),
                    activity => activity.CompensateWith(
                        (context, cancellationToken) => throw new InvalidOperationException("undo-cancel-failed"),
                        "UndoStep"),
                    "Step")
                .Cancel("cancel-requested"));
        }
    }

    private sealed class TerminateCompensationFailureOrchestration : Orchestration<CompensationFailureData>
    {
        protected override void Define(IOrchestrationBuilder<CompensationFailureData> builder)
        {
            builder.State("Created", state => state
                .Activity(
                    (context, cancellationToken) => Task.FromResult(OrchestrationOutcome.Continue()),
                    activity => activity.CompensateWith(
                        (context, cancellationToken) => throw new InvalidOperationException("undo-terminate-failed"),
                        "UndoStep"),
                    "Step")
                .Terminate("terminate-requested"));
        }
    }

    private sealed class LeaseFaultingPersistenceProvider : IOrchestrationStorageProvider
    {
        private readonly InMemoryOrchestrationStorageProvider inner;

        public LeaseFaultingPersistenceProvider(string triggerEventType, int triggerOccurrence)
        {
            this.inner = new InMemoryOrchestrationStorageProvider(new SystemTextJsonSerializer());
            this.Leases = new LeaseFaultingLeaseStore(this.inner.Leases);
            this.History = new LeaseFaultingHistoryStore(this.inner.History, this.Leases, triggerEventType, triggerOccurrence);
        }

        public IOrchestrationInstanceStore Instances => this.inner.Instances;

        public LeaseFaultingLeaseStore Leases { get; }

        IOrchestrationLeaseStore IOrchestrationStorageProvider.Leases => this.Leases;

        public IOrchestrationHistoryStore History { get; }

        public IOrchestrationSignalStore Signals => this.inner.Signals;

        public IOrchestrationTimerStore Timers => this.inner.Timers;

        public IOrchestrationQueryStore Queries => this.inner.Queries;

        public IOrchestrationAdministrationStore Administration => this.inner.Administration;

        public ISerializer Serializer => this.inner.Serializer;
    }

    private sealed class LeaseFaultingLeaseStore : IOrchestrationLeaseStore
    {
        private readonly IOrchestrationLeaseStore inner;
        private readonly ConcurrentDictionary<Guid, Guid> activeLeaseIds = new();
        private readonly ConcurrentDictionary<(Guid InstanceId, Guid LeaseId), byte> invalidatedLeases = new();

        public LeaseFaultingLeaseStore(IOrchestrationLeaseStore inner)
        {
            this.inner = inner;
        }

        public async Task<OrchestrationLease> AcquireAsync(Guid instanceId, string owner, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            var lease = await this.inner.AcquireAsync(instanceId, owner, duration, cancellationToken).ConfigureAwait(false);
            this.activeLeaseIds[instanceId] = lease.LeaseId;
            return lease;
        }

        public void InvalidateActiveLease(Guid instanceId)
        {
            if (this.activeLeaseIds.TryGetValue(instanceId, out var leaseId))
            {
                this.invalidatedLeases.TryAdd((instanceId, leaseId), 0);
            }
        }

        public async Task<OrchestrationLease> RenewAsync(Guid instanceId, Guid leaseId, string owner, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            var lease = await this.inner.RenewAsync(instanceId, leaseId, owner, duration, cancellationToken).ConfigureAwait(false);
            this.activeLeaseIds[instanceId] = lease.LeaseId;
            return lease;
        }

        public async Task ReleaseAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
        {
            this.activeLeaseIds.TryRemove(instanceId, out _);
            this.invalidatedLeases.TryRemove((instanceId, leaseId), out _);
            await this.inner.ReleaseAsync(instanceId, leaseId, owner, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> VerifyAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
        {
            if (this.invalidatedLeases.ContainsKey((instanceId, leaseId)))
            {
                return false;
            }

            return await this.inner.VerifyAsync(instanceId, leaseId, owner, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class LeaseFaultingHistoryStore : IOrchestrationHistoryStore
    {
        private readonly IOrchestrationHistoryStore inner;
        private readonly LeaseFaultingLeaseStore leases;
        private readonly string triggerEventType;
        private readonly int triggerOccurrence;
        private int eventCount;

        public LeaseFaultingHistoryStore(
            IOrchestrationHistoryStore inner,
            LeaseFaultingLeaseStore leases,
            string triggerEventType,
            int triggerOccurrence)
        {
            this.inner = inner;
            this.leases = leases;
            this.triggerEventType = triggerEventType;
            this.triggerOccurrence = triggerOccurrence;
        }

        public async Task<OrchestrationHistoryEntry> AppendAsync(OrchestrationHistoryEntry entry, CancellationToken cancellationToken = default)
        {
            var persisted = await this.inner.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
            if (string.Equals(entry.EventType, this.triggerEventType, StringComparison.OrdinalIgnoreCase) &&
                Interlocked.Increment(ref this.eventCount) == this.triggerOccurrence)
            {
                this.leases.InvalidateActiveLease(entry.InstanceId);
            }

            return persisted;
        }

        public Task<IReadOnlyCollection<OrchestrationHistoryEntry>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
        {
            return this.inner.GetAsync(instanceId, cancellationToken);
        }
    }
}
