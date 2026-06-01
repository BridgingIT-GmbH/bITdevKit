// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

public class OrchestrationRuntimeServiceTests(ITestOutputHelper output) : OrchestrationTestBase(output)
{
    [Fact]
    public async Task DispatchAsync_WhenSignalWaitIsConfigured_PersistsWaitingStateAndCompletesAfterSignal()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SignalDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        // Act
        var dispatch = await sut.DispatchAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());
        var waiting = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Waiting);
        var signal = await sut.SignalAsync(dispatch.Value, "Approved", new ApprovalSignal { ApprovedBy = "manager-1" });
        var completed = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Completed);
        var context = await queries.GetContextAsync<RuntimeOrchestrationData>(dispatch.Value, serviceProvider);

        // Assert
        dispatch.IsSuccess.ShouldBeTrue();
        waiting.CurrentState.ShouldBe("AwaitingApproval");
        signal.IsSuccess.ShouldBeTrue();
        completed.CurrentState.ShouldBe("Done");
        context.Data.ApprovalUserId.ShouldBe("manager-1");
        context.Data.Trace.ShouldBe(["started", "completed"]);
    }

    [Fact]
    public async Task DispatchAsync_WhenTimeoutIsConfigured_TransitionsAfterTimerBecomesDue()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<TimeoutDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        // Act
        var dispatch = await sut.DispatchAsync<TimeoutDrivenOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());
        await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Waiting);
        await Task.Delay(150);
        await ContinueInstanceAsync<TimeoutDrivenOrchestration, RuntimeOrchestrationData>(serviceProvider, dispatch.Value);
        var completed = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Completed);
        var context = await queries.GetContextAsync<RuntimeOrchestrationData>(dispatch.Value, serviceProvider);

        // Assert
        dispatch.IsSuccess.ShouldBeTrue();
        completed.CurrentState.ShouldBe("Expired");
        context.Data.TimedOut.ShouldBeTrue();
    }

    [Fact]
    public async Task PauseAsync_WhenSignalArrivesWhilePaused_DoesNotAdvanceUntilResume()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SignalDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var dispatch = await sut.DispatchAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());
        await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Waiting);

        // Act
        var pause = await sut.PauseAsync(dispatch.Value, "maintenance");
        var paused = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Paused);
        var signal = await sut.SignalAsync(dispatch.Value, "Approved", new ApprovalSignal { ApprovedBy = "manager-2" });
        await Task.Delay(150);
        var stillPaused = await queries.GetInstanceAsync(dispatch.Value);
        var signals = await queries.GetSignalsAsync(dispatch.Value);
        var resume = await sut.ResumeAsync(dispatch.Value);
        var completed = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Completed);

        // Assert
        pause.IsSuccess.ShouldBeTrue();
        paused.Status.ShouldBe(OrchestrationStatus.Paused);
        signal.IsSuccess.ShouldBeTrue();
        stillPaused.Status.ShouldBe(OrchestrationStatus.Paused);
        signals.ShouldContain(item => item.Status == OrchestrationSignalStatus.Pending);
        resume.IsSuccess.ShouldBeTrue();
        completed.CurrentState.ShouldBe("Done");
    }

    [Fact]
    public async Task CancelAsync_WhenInstanceIsWaiting_MarksInstanceCancelled()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SignalDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var dispatch = await sut.DispatchAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());
        await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Waiting);

        // Act
        var cancel = await sut.CancelAsync(dispatch.Value, "user requested");
        var cancelled = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Cancelled);

        // Assert
        cancel.IsSuccess.ShouldBeTrue();
        cancelled.Status.ShouldBe(OrchestrationStatus.Cancelled);
        cancelled.CompletedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task TerminateAsync_WhenInstanceIsWaiting_MarksInstanceTerminated()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SignalDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var dispatch = await sut.DispatchAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());
        await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Waiting);

        // Act
        var terminate = await sut.TerminateAsync(dispatch.Value, "forced stop");
        var terminated = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Terminated);

        // Assert
        terminate.IsSuccess.ShouldBeTrue();
        terminated.Status.ShouldBe(OrchestrationStatus.Terminated);
        terminated.CompletedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task DispatchAsync_WhenRetryOutcomeIsReturned_RetriesAndCompletes()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<RetryDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        // Act
        var dispatch = await sut.DispatchAsync<RetryDrivenOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());
        var completed = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Completed);
        var context = await queries.GetContextAsync<RuntimeOrchestrationData>(dispatch.Value, serviceProvider);
        var history = await queries.GetHistoryAsync(dispatch.Value);

        // Assert
        dispatch.IsSuccess.ShouldBeTrue();
        completed.CurrentState.ShouldBe("Done");
        context.Data.RetryCount.ShouldBe(2);
        history.Count(item => item.EventType == "ActivityRetried").ShouldBe(1);
    }

    [Fact]
    public async Task DispatchAndWaitAsync_WhenStateWaitConditionIsReached_ReturnsSuccessfulWaitResult()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<TimeoutDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();

        // Act
        var result = await sut.DispatchAndWaitAsync<TimeoutDrivenOrchestration, RuntimeOrchestrationData>(
            new RuntimeOrchestrationData(),
            waitFor: WaitFor.State("Expired"),
            timeout: TimeSpan.FromSeconds(2));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TimedOut.ShouldBeFalse();
        result.Value.CurrentState.ShouldBe("Expired");
    }

    [Fact]
    public async Task DispatchAndWaitAsync_WhenBackgroundExecutionIsDisabled_StillAdvancesUntilStateWaitConditionIsReached()
    {
        // Arrange
        var serviceProvider = CreateServices(services =>
        {
            services.AddSingleton(CreateExecutionSettings(enableBackgroundExecution: false));
            services.AddOrchestrations()
                .WithOrchestration<TimeoutDrivenOrchestration>();
        });
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();

        // Act
        var result = await sut.DispatchAndWaitAsync<TimeoutDrivenOrchestration, RuntimeOrchestrationData>(
            new RuntimeOrchestrationData(),
            waitFor: WaitFor.State("Expired"),
            timeout: TimeSpan.FromSeconds(2));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TimedOut.ShouldBeFalse();
        result.Value.CurrentState.ShouldBe("Expired");
    }

    [Fact]
    public async Task DispatchAndWaitAsync_WhenTimeoutExpires_ReturnsTimedOutWaitResult()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SignalDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();

        // Act
        var result = await sut.DispatchAndWaitAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(
            new RuntimeOrchestrationData(),
            waitFor: WaitFor.Completion(),
            timeout: TimeSpan.FromMilliseconds(150));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TimedOut.ShouldBeTrue();
        result.Value.Status.ShouldBe(nameof(OrchestrationStatus.Waiting));
    }

    [Fact]
    public async Task DispatchAndWaitAsync_WhenCancellationIsRequested_ReturnsFailedResult()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SignalDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        // Act
        var result = await sut.DispatchAndWaitAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(
            new RuntimeOrchestrationData(),
            waitFor: WaitFor.Completion(),
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: cancellationTokenSource.Token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(item => item.Message.Contains("canceled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_WhenWaitingConditionIsReached_ReturnsFailedInlineIncompletionResult()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SignalDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();

        // Act
        var result = await sut.ExecuteAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(item => item.Message.Contains("waiting or paused", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SignalAsync_WhenDuplicateIdempotencyKeyIsSubmittedWhilePaused_PersistsSingleSignalAndProcessesOnceOnResume()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SignalDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var dispatch = await sut.DispatchAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());
        await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Waiting);
        await sut.PauseAsync(dispatch.Value, "hold duplicates");
        await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Paused);

        // Act
        var first = await sut.SignalAsync(dispatch.Value, "Approved", new ApprovalSignal { ApprovedBy = "manager-1" }, "approval-1");
        var duplicate = await sut.SignalAsync(dispatch.Value, "Approved", new ApprovalSignal { ApprovedBy = "manager-2" }, "approval-1");
        var signalsBeforeResume = await queries.GetSignalsAsync(dispatch.Value);

        var resume = await sut.ResumeAsync(dispatch.Value);
        var completed = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Completed);
        var context = await queries.GetContextAsync<RuntimeOrchestrationData>(dispatch.Value, serviceProvider);
        var history = await queries.GetHistoryAsync(dispatch.Value);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        duplicate.IsSuccess.ShouldBeTrue();
        resume.IsSuccess.ShouldBeTrue();
        signalsBeforeResume.Count.ShouldBe(1);
        signalsBeforeResume.Single().Status.ShouldBe(OrchestrationSignalStatus.Pending);
        signalsBeforeResume.Single().Payload.ShouldContain("manager-1");
        signalsBeforeResume.Single().PayloadType.ShouldContain(nameof(ApprovalSignal));
        completed.CurrentState.ShouldBe("Done");
        context.Data.ApprovalUserId.ShouldBe("manager-1");
        history.Count(item => item.EventType == "SignalProcessed").ShouldBe(1);
    }

    [Fact]
    public async Task SignalAsync_WhenCurrentStateHasNoHandler_RejectsSignalAndDoesNotAdvanceWorkflow()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SignalDrivenOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var dispatch = await sut.DispatchAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());
        await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Waiting);

        // Act
        var result = await sut.SignalAsync(dispatch.Value, "RejectedByState", new ApprovalSignal { ApprovedBy = "manager-x" }, "reject-1");
        var snapshot = await queries.GetInstanceAsync(dispatch.Value);
        var signals = await queries.GetSignalsAsync(dispatch.Value);
        var history = await queries.GetHistoryAsync(dispatch.Value);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        snapshot.Status.ShouldBe(OrchestrationStatus.Waiting);
        signals.Count.ShouldBe(1);
        signals.Single().Status.ShouldBe(OrchestrationSignalStatus.Rejected);
        signals.Single().ProcessedUtc.ShouldNotBeNull();
        history.Count(item => item.EventType == "SignalRejected").ShouldBe(1);
    }

    [Fact]
    public async Task ContinueInstanceAsync_WhenPendingSignalBecomesStale_MarksSignalIgnored()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<TimeoutDrivenOrchestration>());
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();
        var persistence = serviceProvider.GetRequiredService<IOrchestrationStorageProvider>();

        var instanceId = await CreateInstanceAsync<TimeoutDrivenOrchestration, RuntimeOrchestrationData>(serviceProvider, new RuntimeOrchestrationData());
        await ContinueInstanceAsync<TimeoutDrivenOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId);
        await WaitForInstanceAsync(queries, instanceId, instance => instance.Status == OrchestrationStatus.Waiting);
        var stale = await persistence.Signals.PersistAsync(instanceId, "Unused", new ApprovalSignal { ApprovedBy = "manager-y" }, "AwaitingTimeout", "stale-1");

        // Act
        await Task.Delay(150);
        await ContinueInstanceAsync<TimeoutDrivenOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId);
        var completed = await WaitForInstanceAsync(queries, instanceId, instance => instance.Status == OrchestrationStatus.Completed);
        var signals = await queries.GetSignalsAsync(instanceId);

        // Assert
        completed.CurrentState.ShouldBe("Expired");
        signals.ShouldContain(item => item.SignalId == stale.SignalId && item.Status == OrchestrationSignalStatus.Ignored);
    }

    [Fact]
    public async Task PauseAsync_WhenTimerBecomesOverdue_RemainsPendingUntilResumeAndThenFiresImmediately()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<PausedTimeoutOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var dispatch = await sut.DispatchAsync<PausedTimeoutOrchestration, RuntimeOrchestrationData>(new RuntimeOrchestrationData());
        await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Waiting);

        // Act
        var pause = await sut.PauseAsync(dispatch.Value, "pause timer");
        await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Paused);
        await Task.Delay(450);

        var pausedSnapshot = await queries.GetInstanceAsync(dispatch.Value);
        var timersWhilePaused = await queries.GetTimersAsync(dispatch.Value);

        var resume = await sut.ResumeAsync(dispatch.Value);
        var completed = await WaitForInstanceAsync(queries, dispatch.Value, instance => instance.Status == OrchestrationStatus.Completed);
        var timersAfterResume = await queries.GetTimersAsync(dispatch.Value);
        var context = await queries.GetContextAsync<RuntimeOrchestrationData>(dispatch.Value, serviceProvider);

        // Assert
        pause.IsSuccess.ShouldBeTrue();
        pausedSnapshot.Status.ShouldBe(OrchestrationStatus.Paused);
        timersWhilePaused.Count.ShouldBe(1);
        timersWhilePaused.Single().Status.ShouldBe(OrchestrationTimerStatus.Pending);
        timersWhilePaused.Single().DueTimeUtc.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
        resume.IsSuccess.ShouldBeTrue();
        completed.CurrentState.ShouldBe("Expired");
        timersAfterResume.Single().Status.ShouldBe(OrchestrationTimerStatus.Consumed);
        context.Data.TimedOut.ShouldBeTrue();
    }

    [Fact]
    public async Task SignalAsync_WhenAnotherWorkerHoldsLease_ReturnsLeaseConflict()
    {
        // Arrange
        var provider = new ControllablePersistenceProvider();
        var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<SignalDrivenOrchestration>(),
            provider);
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var instanceId = await CreateInstanceAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(serviceProvider, new RuntimeOrchestrationData());
        await ContinueInstanceAsync<SignalDrivenOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId);
        await WaitForInstanceAsync(queries, instanceId, instance => instance.Status == OrchestrationStatus.Waiting);

        var heldLease = await provider.Leases.AcquireAsync(instanceId, "external-worker", TimeSpan.FromSeconds(10));

        try
        {
            // Act
            var result = await sut.SignalAsync(instanceId, "Approved", new ApprovalSignal { ApprovedBy = "manager-1" });
            var signals = await queries.GetSignalsAsync(instanceId);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Errors.ShouldContain(item => item.Message.Contains("active worker", StringComparison.OrdinalIgnoreCase));
            signals.ShouldBeEmpty();
        }
        finally
        {
            await provider.Leases.ReleaseAsync(instanceId, heldLease.LeaseId, heldLease.Owner);
        }
    }

    [Fact]
    public async Task ContinueInstanceAsync_WhenWorkersRace_OnlyOneWorkerExecutesInstance()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SlowExecutionOrchestration>());
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var instanceId = await CreateInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, new RuntimeOrchestrationData());

        // Act
        await Task.WhenAll(
            ContinueInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId),
            ContinueInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId));

        var completed = await WaitForInstanceAsync(queries, instanceId, instance => instance.Status == OrchestrationStatus.Completed);
        var history = await queries.GetHistoryAsync(instanceId);

        // Assert
        completed.CurrentState.ShouldBe("Done");
        history.Count(item => item.EventType == "ActivityCompleted").ShouldBe(1);
        history.Count(item => item.EventType == "Completed").ShouldBe(1);
    }

    [Fact]
    public async Task ContinueInstanceAsync_WhenLeaseExpiresBeforePersist_KeepsLatestPersistedSnapshot()
    {
        // Arrange
        var provider = new ControllablePersistenceProvider { ForcedLeaseDuration = TimeSpan.FromMilliseconds(50) };
        var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<SlowExecutionOrchestration>(),
            provider);
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var instanceId = await CreateInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, new RuntimeOrchestrationData());

        // Act
        await ContinueInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId);
        var snapshot = await queries.GetInstanceAsync(instanceId);
        var context = await queries.GetContextAsync<RuntimeOrchestrationData>(instanceId, serviceProvider);

        // Assert
        snapshot.Status.ShouldBe(OrchestrationStatus.Running);
        snapshot.CurrentState.ShouldBe("Start");
        context.Data.Trace.ShouldBeEmpty();
    }

    [Fact]
    public async Task ContinueInstanceAsync_WhenLeaseIsLostDuringActivity_StopsWithoutCompleting()
    {
        // Arrange
        var provider = new ControllablePersistenceProvider { ForcedLeaseDuration = TimeSpan.FromMilliseconds(50) };
        var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<SlowExecutionOrchestration>(),
            provider);
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var instanceId = await CreateInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, new RuntimeOrchestrationData());

        // Act
        await ContinueInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId);
        var snapshot = await queries.GetInstanceAsync(instanceId);
        var history = await queries.GetHistoryAsync(instanceId);

        // Assert
        snapshot.Status.ShouldNotBe(OrchestrationStatus.Completed);
        history.ShouldNotContain(item => item.EventType == "Completed");
    }

    [Fact]
    public async Task ContinueInstanceAsync_WhenLeaseExpiresAndNextWorkerRetries_RecoversFromPersistedSnapshot()
    {
        // Arrange
        var provider = new ControllablePersistenceProvider { ForcedLeaseDuration = TimeSpan.FromMilliseconds(50) };
        var serviceProvider = CreateServices(
            services => services.AddOrchestrations().WithOrchestration<SlowExecutionOrchestration>(),
            provider);
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var instanceId = await CreateInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, new RuntimeOrchestrationData());
        await ContinueInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId);

        // Act
        provider.ForcedLeaseDuration = null;
        await ContinueInstanceAsync<SlowExecutionOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId);

        var completed = await WaitForInstanceAsync(queries, instanceId, instance => instance.Status == OrchestrationStatus.Completed);
        var context = await queries.GetContextAsync<RuntimeOrchestrationData>(instanceId, serviceProvider);

        // Assert
        completed.CurrentState.ShouldBe("Done");
        context.Data.Trace.ShouldBe(["slow-completed"]);
    }

    [Fact]
    public async Task SignalAsync_WhenSignalsArriveConcurrently_OnlyOneSignalWorkerAdvancesInstance()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SlowSignalOrchestration>());
        var sut = serviceProvider.GetRequiredService<IOrchestrationService>();
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var instanceId = await CreateInstanceAsync<SlowSignalOrchestration, RuntimeOrchestrationData>(serviceProvider, new RuntimeOrchestrationData());
        await ContinueInstanceAsync<SlowSignalOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId);
        await WaitForInstanceAsync(queries, instanceId, instance => instance.Status == OrchestrationStatus.Waiting);

        // Act
        var results = await Task.WhenAll(
            sut.SignalAsync(instanceId, "Approved", new ApprovalSignal { ApprovedBy = "manager-1" }),
            sut.SignalAsync(instanceId, "Approved", new ApprovalSignal { ApprovedBy = "manager-2" }));

        var completed = await WaitForInstanceAsync(queries, instanceId, instance => instance.Status == OrchestrationStatus.Completed);
        var signals = await queries.GetSignalsAsync(instanceId);

        // Assert
        results.Count(item => item.IsSuccess).ShouldBe(1);
        results.Count(item => item.IsFailure).ShouldBe(1);
        completed.CurrentState.ShouldBe("Done");
        signals.Count(item => item.Status == OrchestrationSignalStatus.Processed).ShouldBe(1);
    }

    [Fact]
    public async Task ContinueInstanceAsync_WhenTimerWorkersRace_ConsumesTimerOnce()
    {
        // Arrange
        var serviceProvider = CreateServices(services => services
            .AddOrchestrations()
            .WithOrchestration<SlowTimerOrchestration>());
        var queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();

        var instanceId = await CreateInstanceAsync<SlowTimerOrchestration, RuntimeOrchestrationData>(serviceProvider, new RuntimeOrchestrationData());
        await ContinueInstanceAsync<SlowTimerOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId);
        await WaitForInstanceAsync(queries, instanceId, instance => instance.Status == OrchestrationStatus.Waiting);
        await Task.Delay(150);

        // Act
        await Task.WhenAll(
            ContinueInstanceAsync<SlowTimerOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId),
            ContinueInstanceAsync<SlowTimerOrchestration, RuntimeOrchestrationData>(serviceProvider, instanceId));

        var completed = await WaitForInstanceAsync(queries, instanceId, instance => instance.Status == OrchestrationStatus.Completed);
        var history = await queries.GetHistoryAsync(instanceId);

        // Assert
        completed.CurrentState.ShouldBe("Expired");
        history.Count(item => item.EventType == "TimerConsumed").ShouldBe(1);
    }

    private ServiceProvider CreateServices(Action<IServiceCollection> configure, IOrchestrationStorageProvider persistenceProvider = null)
    {
        var services = new ServiceCollection();
        this.ConfigureLogging(services);
        if (persistenceProvider is not null)
        {
            services.AddSingleton(persistenceProvider);
        }

        configure(services);

        return services.BuildServiceProvider();
    }

    private static async Task<Guid> CreateInstanceAsync<TOrchestration, TData>(ServiceProvider serviceProvider, TData data)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        var executor = serviceProvider.GetRequiredService<InMemoryOrchestrationExecutor>();
        var method = typeof(InMemoryOrchestrationExecutor).GetMethod("CreateInstanceAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CreateInstanceAsync could not be resolved.");

        var task = (Task<Guid>)method.MakeGenericMethod(typeof(TOrchestration), typeof(TData))
            .Invoke(executor, [data, CancellationToken.None])!;

        return await task;
    }

    private static async Task ContinueInstanceAsync<TOrchestration, TData>(ServiceProvider serviceProvider, Guid instanceId)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        var executor = serviceProvider.GetRequiredService<InMemoryOrchestrationExecutor>();
        var method = typeof(InMemoryOrchestrationExecutor).GetMethod("ContinueInstanceCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ContinueInstanceCoreAsync could not be resolved.");

        var task = (Task)method.MakeGenericMethod(typeof(TOrchestration), typeof(TData))
            .Invoke(executor, [instanceId, CancellationToken.None])!;

        await task;
    }

    private static async Task<OrchestrationInstanceSnapshot> WaitForInstanceAsync(
        IOrchestrationQueryStore queries,
        Guid instanceId,
        Func<OrchestrationInstanceSnapshot, bool> predicate,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(5));

        while (DateTimeOffset.UtcNow < deadline)
        {
            var instance = await queries.GetInstanceAsync(instanceId);
            if (instance is not null && predicate(instance))
            {
                return instance;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Orchestration instance '{instanceId}' did not reach the expected condition in time.");
    }

    private static object CreateExecutionSettings(bool enableBackgroundExecution)
    {
        var type = typeof(InMemoryOrchestrationExecutor).Assembly.GetType("BridgingIT.DevKit.Application.Orchestrations.OrchestrationExecutionSettings")
            ?? throw new InvalidOperationException("OrchestrationExecutionSettings type could not be resolved.");
        var instance = Activator.CreateInstance(type, nonPublic: true)
            ?? throw new InvalidOperationException("OrchestrationExecutionSettings instance could not be created.");

        type.GetProperty("EnableBackgroundExecution", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(instance, enableBackgroundExecution);

        return instance;
    }

    private class RuntimeOrchestrationData : IOrchestrationData
    {
        public string ApprovalUserId { get; set; }

        public int RetryCount { get; set; }

        public bool TimedOut { get; set; }

        public List<string> Trace { get; set; } = [];
    }

    private class ApprovalSignal
    {
        public string ApprovedBy { get; set; }
    }

    private class SignalDrivenOrchestration : Orchestration<RuntimeOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<RuntimeOrchestrationData> builder)
        {
            builder
                .State("AwaitingApproval", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.Trace.Add("started");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .WaitForSignal<ApprovalSignal>("Approved", signal => signal
                        .MapToContext((context, payload) => context.Data.ApprovalUserId = payload.ApprovedBy)
                        .TransitionTo("Done")))
                .State("Done", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.Trace.Add("completed");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .Complete());
        }
    }

    private class TimeoutDrivenOrchestration : Orchestration<RuntimeOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<RuntimeOrchestrationData> builder)
        {
            builder
                .State("AwaitingTimeout", state => state
                    .TimeoutAfter(TimeSpan.FromMilliseconds(100))
                    .TransitionTo("Expired"))
                .State("Expired", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.TimedOut = true;
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .Complete());
        }
    }

    private class RetryDrivenOrchestration : Orchestration<RuntimeOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<RuntimeOrchestrationData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.RetryCount++;

                        return Task.FromResult(context.Data.RetryCount == 1
                            ? OrchestrationOutcome.Retry("retry once")
                            : OrchestrationOutcome.Continue());
                    })
                    .TransitionTo("Done"))
                .State("Done", state => state.Complete());
        }
    }

    private class SlowExecutionOrchestration : Orchestration<RuntimeOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<RuntimeOrchestrationData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity(async (context, cancellationToken) =>
                    {
                        await Task.Delay(150, cancellationToken);
                        context.Data.Trace.Add("slow-completed");
                        return OrchestrationOutcome.Continue();
                    })
                    .TransitionTo("Done"))
                .State("Done", state => state.Complete());
        }
    }

    private class SlowSignalOrchestration : Orchestration<RuntimeOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<RuntimeOrchestrationData> builder)
        {
            builder
                .State("AwaitingApproval", state => state
                    .WaitForSignal<ApprovalSignal>("Approved", signal => signal
                        .MapToContext((context, payload) => context.Data.ApprovalUserId = payload.ApprovedBy)
                        .Activity(async (context, cancellationToken) =>
                        {
                            await Task.Delay(150, cancellationToken);
                            context.Data.Trace.Add("signal-processed");
                            return OrchestrationOutcome.Continue();
                        })
                        .TransitionTo("Done")))
                .State("Done", state => state.Complete());
        }
    }

    private class SlowTimerOrchestration : Orchestration<RuntimeOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<RuntimeOrchestrationData> builder)
        {
            builder
                .State("Waiting", state => state
                    .TimeoutAfter(TimeSpan.FromMilliseconds(100))
                    .TransitionTo("Expired"))
                .State("Expired", state => state
                    .Activity(async (context, cancellationToken) =>
                    {
                        await Task.Delay(150, cancellationToken);
                        context.Data.Trace.Add("timer-processed");
                        return OrchestrationOutcome.Continue();
                    })
                    .Complete());
        }
    }

    private class PausedTimeoutOrchestration : Orchestration<RuntimeOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<RuntimeOrchestrationData> builder)
        {
            builder
                .State("AwaitingTimeout", state => state
                    .TimeoutAfter(TimeSpan.FromMilliseconds(350))
                    .TransitionTo("Expired"))
                .State("Expired", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.TimedOut = true;
                        context.Data.Trace.Add("paused-timeout-fired");
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .Complete());
        }
    }

    private class ControllablePersistenceProvider : IOrchestrationStorageProvider
    {
        private readonly InMemoryOrchestrationStorageProvider inner;

        public ControllablePersistenceProvider()
        {
            this.inner = new InMemoryOrchestrationStorageProvider(new SystemTextJsonSerializer());
            this.LeaseController = new ControllableLeaseStore(this.inner.Leases);
        }

        public TimeSpan? ForcedLeaseDuration
        {
            get => this.LeaseController.ForcedDuration;
            set => this.LeaseController.ForcedDuration = value;
        }

        public ControllableLeaseStore LeaseController { get; }

        public IOrchestrationInstanceStore Instances => this.inner.Instances;

        public IOrchestrationLeaseStore Leases => this.LeaseController;

        public IOrchestrationHistoryStore History => this.inner.History;

        public IOrchestrationSignalStore Signals => this.inner.Signals;

        public IOrchestrationTimerStore Timers => this.inner.Timers;

        public IOrchestrationQueryStore Queries => this.inner.Queries;

        public IOrchestrationAdministrationStore Administration => this.inner.Administration;

        public ISerializer Serializer => this.inner.Serializer;
    }

    private class ControllableLeaseStore : IOrchestrationLeaseStore
    {
        private readonly IOrchestrationLeaseStore inner;

        public ControllableLeaseStore(IOrchestrationLeaseStore inner)
        {
            this.inner = inner;
        }

        public TimeSpan? ForcedDuration { get; set; }

        public Task<OrchestrationLease> AcquireAsync(Guid instanceId, string owner, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            return this.inner.AcquireAsync(instanceId, owner, this.ForcedDuration ?? duration, cancellationToken);
        }

        public Task<OrchestrationLease> RenewAsync(Guid instanceId, Guid leaseId, string owner, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            return this.inner.RenewAsync(instanceId, leaseId, owner, this.ForcedDuration ?? duration, cancellationToken);
        }

        public Task ReleaseAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
        {
            return this.inner.ReleaseAsync(instanceId, leaseId, owner, cancellationToken);
        }

        public Task<bool> VerifyAsync(Guid instanceId, Guid leaseId, string owner, CancellationToken cancellationToken = default)
        {
            return this.inner.VerifyAsync(instanceId, leaseId, owner, cancellationToken);
        }
    }
}
