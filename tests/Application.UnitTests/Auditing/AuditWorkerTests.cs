// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using Application.Auditing;
using Application.Jobs;
using Application.Messaging;
using Application.Queueing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

public sealed class AuditWorkerTests
{
    [Fact]
    public void Handoff_RejectsWrongOperationFeatureRingAndTampering()
    {
        var sut = new AuditExecutionLinkProtector(new EphemeralDataProtectionProvider());
        var link = new AuditExecutionLink { OriginAuditId = Guid.NewGuid(), CausationAuditId = Guid.NewGuid(), CausationActionId = Guid.NewGuid(),
            CorrelationId = "customer-run", InitiatingActor = new() { Kind = "User", UserId = "alice", DisplayName = "Alice", IsAuthenticated = true } };
        var token = sut.Protect(link, "Jobs", "occurrence-1");
        sut.Unprotect(token, "Jobs", "occurrence-1").ShouldBe(link);
        sut.Unprotect(token, "Jobs", "occurrence-2").ShouldBeNull();
        sut.Unprotect(token, "Messaging", "occurrence-1").ShouldBeNull();
        sut.Unprotect(token[..^2] + "xx", "Jobs", "occurrence-1").ShouldBeNull();
        new AuditExecutionLinkProtector(new EphemeralDataProtectionProvider()).Unprotect(token, "Jobs", "occurrence-1").ShouldBeNull();
        sut.Unprotect(new string('x', 16_385), "Jobs", "occurrence-1").ShouldBeNull();
        token.ShouldNotContain("alice");
    }

    [Fact]
    public async Task MessageWorker_IsIndependentBeforeHandlerResolutionAndCanPersistBeforeItsParent()
    {
        await using var host = Host();
        await using var scope = host.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = audit.BeginScope(builder => builder.Key("customers.submit"));
        var broker = host.GetRequiredService<IMessageBrokerRuntime>();
        await broker.Subscribe<CustomerMessage, CustomerHandler>();
        var message = new CustomerMessage();
        await broker.Publish(message);
        await host.GetRequiredService<State>().WorkerStored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var worker = (await Records(host)).ShouldHaveSingleItem();
        worker.Kind.ShouldBe("MessageConsumer");
        worker.Actor.Kind.ShouldBe("Service");
        worker.InitiatingActor.UserId.ShouldBe("alice");
        worker.OriginAuditId.ShouldBe(root.AuditId);
        worker.CausationAuditId.ShouldBe(root.AuditId);
        worker.CausationActionId.ShouldNotBeNull();
        var state = host.GetRequiredService<State>();
        state.ConstructorAuditId.ShouldBe(worker.AuditId);
        state.HandlerCalls.ShouldBe(1);
        state.Disposals.ShouldBe(1);
        root.Complete();
        await root.DisposeAsync();
        var parent = (await Records(host)).Single(record => record.AuditId == root.AuditId);
        parent.Actions.ShouldHaveSingleItem().Id.ShouldBe(worker.CausationActionId.Value);
        parent.Actions.Single().ExecutionStatus.ShouldBe("Accepted");
        worker.Actions.Single(action => action.Kind == "MessageHandler").Attempt.ShouldBe(1);
        AssertNoSecrets(await Records(host));
    }

    [Fact]
    public async Task MessageFailure_DoesNotTurnAdmissionIntoDeliveryOrInvokeRemainingHandlers()
    {
        await using var host = Host();
        var broker = host.GetRequiredService<IMessageBrokerRuntime>();
        await broker.Subscribe<CustomerMessage, FailingHandler>();
        await broker.Subscribe<CustomerMessage, CustomerHandler>();
        await broker.Publish(new CustomerMessage());
        await host.GetRequiredService<State>().WorkerStored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var records = await Records(host);
        records.Single(record => record.Kind == "MessagePublisher").Outcome.ShouldBe(AuditOutcome.Succeeded);
        var failed = records.Single(record => record.Kind == "MessageConsumer");
        failed.Outcome.ShouldBe(AuditOutcome.Failed);
        failed.Actions.ShouldHaveSingleItem().Outcome.ShouldBe(AuditOutcome.Failed);
        host.GetRequiredService<State>().HandlerCalls.ShouldBe(0);
        AssertNoSecrets(records);
    }

    [Fact]
    public async Task SuppressedProducer_ClearsHandoffButIndependentWorkerStillCapturesAsService()
    {
        await using var host = Host();
        await using var scope = host.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var broker = host.GetRequiredService<IMessageBrokerRuntime>();
        await broker.Subscribe<CustomerMessage, CustomerHandler>();
        var message = new CustomerMessage();
        message.Properties[AuditHandoffMetadata.PropertyName] = "forged-metadata";
        message.Properties["InitiatingActor"] = "administrator";
        using (audit.Suppress()) await broker.Publish(message);
        await host.GetRequiredService<State>().WorkerStored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        message.Properties.ContainsKey(AuditHandoffMetadata.PropertyName).ShouldBeFalse();
        var worker = (await Records(host)).ShouldHaveSingleItem();
        worker.Actor.Kind.ShouldBe("Service");
        worker.InitiatingActor.ShouldBeNull();
        worker.OriginAuditId.ShouldBe(worker.AuditId);
        worker.CausationAuditId.ShouldBeNull();
    }

    [Fact]
    public async Task ForgedInboundMetadata_DoesNotSupplyAttribution()
    {
        await using var host = Host();
        var broker = host.GetRequiredService<IMessageBrokerRuntime>();
        await broker.Subscribe<CustomerMessage, CustomerHandler>();
        var message = new CustomerMessage();
        message.Properties[AuditHandoffMetadata.PropertyName] = "untrusted";
        message.Properties["OriginAuditId"] = Guid.NewGuid().ToString();
        message.Properties["UserId"] = "administrator";
        await broker.Process(new(message, CancellationToken.None));
        var worker = (await Records(host)).ShouldHaveSingleItem();
        worker.OriginAuditId.ShouldBe(worker.AuditId);
        worker.InitiatingActor.ShouldBeNull();
        worker.CausationAuditId.ShouldBeNull();
    }

    [Fact]
    public async Task ActualQueueWorker_CapturesIndependentDeliveryAndNeverCopiesPayload()
    {
        await using var host = Host();
        var broker = host.GetRequiredService<IQueueBrokerRuntime>();
        await broker.Subscribe<CustomerQueueMessage, CustomerQueueHandler>();
        await using var scope = host.CreateAsyncScope();
        var root = scope.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.enqueue"));
        await broker.Enqueue(new CustomerQueueMessage()).WaitAsync(TimeSpan.FromSeconds(10));
        await host.GetRequiredService<State>().WorkerStored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // The storage signal follows the real memory provider's confirmed append.
        root.Complete();
        await root.DisposeAsync();
        await host.GetRequiredService<AuditRuntime>().WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var records = await Records(host);
        var worker = records.Single(record => record.Kind == "MessageConsumer");
        worker.Actor.Kind.ShouldBe("Service");
        worker.OriginAuditId.ShouldBe(root.AuditId);
        worker.InitiatingActor.UserId.ShouldBe("alice");
        worker.Actions.ShouldHaveSingleItem().Feature.ShouldBe("Queueing");
        AssertNoSecrets(records);
    }

    [Fact]
    public async Task Jobs_ChainUsesImmediatePredecessorAndOriginalInitiator()
    {
        await using var host = Host();
        await using var services = host.CreateAsyncScope();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.export"));
        var scheduler = host.GetRequiredService<JobSchedulerService>();
        var accepted = await scheduler.DispatchAsync("customer-export");
        accepted.IsSuccess.ShouldBeTrue();
        (await scheduler.ExecuteStoredOccurrenceAsync(accepted.Value.OccurrenceId)).IsSuccess.ShouldBeTrue();
        var store = host.GetRequiredService<IJobStoreProvider>();
        var successor = (await store.Queries.ListOccurrencesAsync()).Single(occurrence => occurrence.JobName == "customer-confirm");
        (await scheduler.ExecuteStoredOccurrenceAsync(successor.OccurrenceId)).IsSuccess.ShouldBeTrue();
        var workers = await Records(host);
        workers.Length.ShouldBe(2);
        workers.All(record => record.Kind == "Job" && record.Actor.Kind == "Service" && record.InitiatingActor.UserId == "alice").ShouldBeTrue();
        var first = workers.Single(record => record.JobName == "customer-export");
        var second = workers.Single(record => record.JobName == "customer-confirm");
        first.OriginAuditId.ShouldBe(root.AuditId);
        first.CausationAuditId.ShouldBe(root.AuditId);
        second.OriginAuditId.ShouldBe(root.AuditId);
        second.CausationAuditId.ShouldBe(first.AuditId);
        second.CausationActionId.ShouldBe(first.Actions.Single().Id);
        first.JobExecutionId.ShouldNotBeNullOrWhiteSpace();
        second.JobExecutionId.ShouldNotBeNullOrWhiteSpace();
        var queries = services.ServiceProvider.GetRequiredService<IAuditQueryService>();
        var beforeParent = await queries.FindRelatedAsync(first.AuditId);
        beforeParent.IsSuccess.ShouldBeTrue();
        beforeParent.Value.Select(item => item.AuditId).ShouldBe([second.AuditId]);
        (await queries.FindAsync(root.AuditId)).IsFailure.ShouldBeTrue();
        root.Complete();
        await root.DisposeAsync();
        (await Records(host)).Single(record => record.AuditId == root.AuditId).Actions.Single().ExecutionStatus.ShouldBe("Accepted");
        var afterParent = await queries.FindRelatedAsync(first.AuditId);
        afterParent.IsSuccess.ShouldBeTrue();
        afterParent.Value.Select(item => item.AuditId).Order().ShouldBe(new[] { root.AuditId, second.AuditId }.Order());
    }

    [Fact]
    public async Task UnknownJobAndFailedWorker_KeepDispatchRejectionAndActualFailureDistinct()
    {
        await using var host = Host();
        var scheduler = host.GetRequiredService<IJobSchedulerService>();
        (await scheduler.DispatchAsync("unregistered")).IsFailure.ShouldBeTrue();
        (await scheduler.DispatchAndWaitAsync("customer-fail")).Value.Status.ShouldBe(JobExecutionStatus.Failed);
        var records = await Records(host);
        records.Single(record => record.Kind == "JobDispatch" && record.JobName == "unregistered").Outcome.ShouldBe(AuditOutcome.Rejected);
        records.Single(record => record.Kind == "JobDispatch" && record.JobName == "customer-fail").Outcome.ShouldBe(AuditOutcome.Succeeded);
        var worker = records.Single(record => record.Kind == "Job");
        worker.Outcome.ShouldBe(AuditOutcome.Failed);
        worker.Actions.Single().Outcome.ShouldBe(AuditOutcome.Failed);
        AssertNoSecrets(records);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SourceSelection_IsIdempotentAndIndependentOfCoreRegistrationOrder(bool auditFirst)
    {
        var services = Collection();
        if (auditFirst) services.AddAuditing();
        services.AddMessaging().WithAuditing().WithAuditing();
        services.AddQueueing().WithAuditing().WithAuditing();
        services.AddJobScheduler().WithAuditing().WithAuditing();
        if (!auditFirst) services.AddAuditing();
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        host.GetServices<IMessagePublisherBehavior>().OfType<AuditingMessagePublisherBehavior>().Count().ShouldBe(1);
        host.GetServices<IMessageHandlerBehavior>().OfType<AuditingMessageHandlerBehavior>().Count().ShouldBe(1);
        host.GetServices<IQueueEnqueuerBehavior>().OfType<AuditingQueueEnqueuerBehavior>().Count().ShouldBe(1);
        host.GetServices<IQueueHandlerBehavior>().OfType<AuditingQueueHandlerBehavior>().Count().ShouldBe(1);
        host.GetRequiredService<JobRegistrationStore>().GetDispatchBehaviorTypes().Count(type => type == typeof(AuditingJobDispatchBehavior)).ShouldBe(1);
        (await Records(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task OmittedConsumer_StillIsolatesFirstNestedCaptureAndPreservesSourceCorrelation()
    {
        await using var host = Host(messageCapture: false);
        await using var services = host.CreateAsyncScope();
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var parent = audit.BeginScope(builder => builder.Key("customers.unselected-producer"));
        var broker = host.GetRequiredService<IMessageBrokerRuntime>();
        await broker.Subscribe<CustomerMessage, NestedCustomerHandler>();
        var message = new CustomerMessage();
        using (audit.Suppress()) await broker.Publish(message);
        var nested = await host.GetRequiredService<State>().NestedStored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        nested.Kind.ShouldBe("Business");
        nested.Actor.Kind.ShouldBe("Service");
        nested.InitiatingActor.ShouldBeNull();
        nested.OriginAuditId.ShouldBe(nested.AuditId);
        nested.CorrelationId.ShouldBe(message.Properties[Application.Messaging.Constants.CorrelationIdKey].ToString());
        (await Records(host)).ShouldHaveSingleItem();
        audit.Current.AuditId.ShouldBe(parent.AuditId);
        parent.Complete();
        await parent.DisposeAsync();
    }

    [Fact]
    public async Task OmittedJobCapture_StillIsolatesNestedBusinessScopeFromSuppressedCaller()
    {
        await using var host = Host(jobCapture: false);
        await using var services = host.CreateAsyncScope();
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var parent = audit.BeginScope(builder => builder.Key("customers.unselected-job"));
        var scheduler = host.GetRequiredService<IJobSchedulerService>();
        using (audit.Suppress()) (await scheduler.DispatchAndWaitAsync("customer-nested", options: new() { CorrelationId = "source-occurrence-correlation" })).IsSuccess.ShouldBeTrue();
        var nested = (await Records(host)).ShouldHaveSingleItem();
        nested.Kind.ShouldBe("Business");
        nested.Actor.Kind.ShouldBe("Service");
        nested.InitiatingActor.ShouldBeNull();
        nested.CorrelationId.ShouldBe("source-occurrence-correlation");
        nested.Actions.ShouldBeEmpty();
        parent.Complete();
        await parent.DisposeAsync();
    }

    [Fact]
    public async Task BrokenHandoffProtection_CannotRestoreInheritedSuppressionOrAttribution()
    {
        await using var host = Host(configure: services => services.AddSingleton<IAuditExecutionLinkProtector, BrokenProtector>());
        await using var services = host.CreateAsyncScope();
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var broker = host.GetRequiredService<IMessageBrokerRuntime>();
        await broker.Subscribe<CustomerMessage, CustomerHandler>();
        var message = new CustomerMessage();
        message.Properties[AuditHandoffMetadata.PropertyName] = "invalid";
        message.Properties[Application.Messaging.Constants.CorrelationIdKey] = "unlinked-message-correlation";
        using (audit.Suppress()) await broker.Process(new(message, CancellationToken.None));
        var worker = (await Records(host)).ShouldHaveSingleItem();
        worker.Actor.Kind.ShouldBe("Service");
        worker.InitiatingActor.ShouldBeNull();
        worker.CorrelationId.ShouldBe("unlinked-message-correlation");
    }

    [Fact]
    public async Task ValidationRejection_IsObservedBeforeAdmissionAndCreatesNoWorker()
    {
        await using var host = Host();
        await Should.ThrowAsync<FluentValidation.ValidationException>(() => host.GetRequiredService<IMessageBrokerRuntime>().Publish(new InvalidMessage()));
        await Should.ThrowAsync<FluentValidation.ValidationException>(() => host.GetRequiredService<IQueueBrokerRuntime>().Enqueue(new InvalidQueueMessage()));
        var records = await Records(host);
        records.Length.ShouldBe(2);
        records.All(record => record.Outcome == AuditOutcome.Rejected).ShouldBeTrue();
        records.All(record => record.Actions.Length == 0).ShouldBeTrue();
        AssertNoSecrets(records);
    }

    [Fact]
    public async Task HandlerConstructionFailure_DisposesAsynchronousDependenciesAndInventsNoInvocation()
    {
        await using var host = Host();
        var broker = host.GetRequiredService<IMessageBrokerRuntime>();
        await broker.Subscribe<CustomerMessage, ThrowingConstructorHandler>();
        await broker.Publish(new CustomerMessage());
        var worker = await host.GetRequiredService<State>().WorkerStored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Outcome.ShouldBe(AuditOutcome.Failed);
        worker.Actions.ShouldBeEmpty();
        host.GetRequiredService<State>().Disposals.ShouldBe(1);
        AssertNoSecrets(await Records(host));
    }

    [Fact]
    public async Task MessageRetry_RecordsActualAttemptsInsideTheRetryBoundary()
    {
        await using var host = Host(configure: services => services.AddMessaging().WithBehavior<RetryMessageBehavior>((IMessageHandlerBehavior)null));
        var broker = host.GetRequiredService<IMessageBrokerRuntime>();
        await broker.Subscribe<CustomerMessage, FlakyHandler>();
        await broker.Publish(new CustomerMessage());
        var worker = await host.GetRequiredService<State>().WorkerStored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Outcome.ShouldBe(AuditOutcome.Succeeded);
        worker.Actions.Length.ShouldBe(2);
        worker.Actions.Select(action => action.Attempt).ShouldBe([1, 2]);
        worker.Actions.Select(action => action.Outcome).ShouldBe([AuditOutcome.Failed, AuditOutcome.Succeeded]);
        worker.Details["message.handlerAttempts"].Data.GetInt64().ShouldBe(2);
        host.GetRequiredService<State>().HandlerCalls.ShouldBe(2);
    }

    [Fact]
    public async Task LaterPolicyShortCircuit_DoesNotCreateAnInvokedJobHandler()
    {
        await using var host = Host(configure: services => services.AddJobScheduler().WithBehavior<RejectBeforeJobBehavior>());
        var result = await host.GetRequiredService<IJobSchedulerService>().DispatchAndWaitAsync("customer-export");
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        var worker = (await Records(host)).Single(record => record.Kind == "Job");
        worker.Actions.ShouldBeEmpty();
        worker.Outcome.ShouldBe(AuditOutcome.Failed);
    }

    [Fact]
    public async Task JobRetry_KeepsOccurrenceAndOriginWithDistinctExecutionIdsAndActualAttemptNumbers()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = Host(configure: services =>
        {
            services.AddSingleton<TimeProvider>(time);
            services.AddJobScheduler().WithJob<FlakyCustomerJob>("customer-retry", job => job.Description("Retries a failed attempt.")
                .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromSeconds(1))).AddTrigger("manual", trigger => trigger.Manual()));
        });
        await using var services = host.CreateAsyncScope();
        var parent = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.retry"));
        var scheduler = host.GetRequiredService<JobSchedulerService>();
        var admitted = await scheduler.DispatchAsync("customer-retry");
        (await scheduler.ExecuteStoredOccurrenceAsync(admitted.Value.OccurrenceId)).Value.Status.ShouldBe(JobExecutionStatus.Retried);
        time.Advance(TimeSpan.FromSeconds(2));
        (await scheduler.ExecuteStoredOccurrenceAsync(admitted.Value.OccurrenceId)).Value.Status.ShouldBe(JobExecutionStatus.Completed);
        var attempts = (await Records(host)).OrderBy(record => record.StartedUtc).ToArray();
        attempts.Length.ShouldBe(2);
        attempts.Select(record => record.Outcome).ShouldBe([AuditOutcome.Failed, AuditOutcome.Succeeded]);
        attempts.Select(record => record.JobExecutionId).Distinct().Count().ShouldBe(2);
        attempts.All(record => record.JobOccurrenceId == admitted.Value.OccurrenceId.ToString("D") && record.OriginAuditId == parent.AuditId).ShouldBeTrue();
        attempts.Select(record => record.Actions.Single().Attempt).ShouldBe([1, 2]);
        parent.Complete();
        await parent.DisposeAsync();
    }

    [Fact]
    public async Task CallerWaitCancellation_PreservesAcceptanceAndRecordsTheWorkersActualCanceledOutcome()
    {
        await using var host = Host(configure: services => services.AddJobScheduler().WithJob<CancelableCustomerJob>("customer-wait", job => job.Description("Observes cancellation.").AddTrigger("manual", trigger => trigger.Manual())));
        var result = await host.GetRequiredService<IJobSchedulerService>().DispatchAndWaitAsync("customer-wait", timeout: TimeSpan.FromMilliseconds(250));
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Cancelled);
        var records = await Records(host);
        records.Single(record => record.Kind == "JobDispatch").Outcome.ShouldBe(AuditOutcome.Succeeded);
        records.Single(record => record.Kind == "Job").Outcome.ShouldBe(AuditOutcome.Canceled);
    }

    public sealed class FlakyCustomerJob(State state) : IJob
    {
        public Task<IResult> ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<IResult>(++state.HandlerCalls == 1 ? Result.Failure("private-payload-secret") : Result.Success());
    }
    public sealed class CancelableCustomerJob : IJob
    {
        public async Task<IResult> ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Result.Success();
        }
    }

    [Fact]
    public async Task LinkedWorkers_PreserveNativeCorrelationWhenItDiffersFromTheirInitiator()
    {
        await using var host = Host();
        await using var services = host.CreateAsyncScope();
        using var correlation = CorrelationId.BeginScope("initiator-correlation");
        var parent = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.distinct-correlation"));
        var message = new CustomerMessage();
        message.Properties[Application.Messaging.Constants.CorrelationIdKey] = "message-native-correlation";
        var broker = host.GetRequiredService<IMessageBrokerRuntime>();
        await broker.Subscribe<CustomerMessage, CustomerHandler>();
        await broker.Publish(message);
        var consumer = await host.GetRequiredService<State>().WorkerStored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        consumer.CorrelationId.ShouldBe("message-native-correlation");
        consumer.OriginAuditId.ShouldBe(parent.AuditId);
        consumer.InitiatingActor.UserId.ShouldBe("alice");
        (await host.GetRequiredService<IJobSchedulerService>().DispatchAndWaitAsync("customer-confirm", options: new() { CorrelationId = "job-native-correlation" })).IsSuccess.ShouldBeTrue();
        var job = (await Records(host)).Single(record => record.Kind == "Job");
        job.CorrelationId.ShouldBe("job-native-correlation");
        job.OriginAuditId.ShouldBe(parent.AuditId);
        job.InitiatingActor.UserId.ShouldBe("alice");
        parent.Complete();
        await parent.DisposeAsync();
        (await Records(host)).Single(record => record.AuditId == parent.AuditId).CorrelationId.ShouldBe("initiator-correlation");
    }

    [Fact]
    public async Task TimerOccurrence_HasServiceActorAndNoInventedUserOrOrigin()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = Host(configure: services =>
        {
            services.AddSingleton<TimeProvider>(time);
            services.AddJobScheduler().WithJob<CustomerJob>("customer-timer", job => job.Description("Scheduled service work.").AddTrigger("once", trigger => trigger.At(time.GetUtcNow().AddSeconds(1))));
        });
        await using var services = host.CreateAsyncScope();
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var parent = audit.BeginScope(builder => builder.Key("customers.timer-management"));
        var scheduler = host.GetRequiredService<JobSchedulerService>();
        using (audit.Suppress())
        {
            time.Advance(TimeSpan.FromSeconds(2));
            await scheduler.MaterializeScheduledOccurrencesAsync(time.GetUtcNow(), 100);
            var occurrence = (await host.GetRequiredService<IJobStoreProvider>().Queries.ListOccurrencesAsync()).Single(item => item.JobName == "customer-timer");
            (await scheduler.ExecuteStoredOccurrenceAsync(occurrence.OccurrenceId)).IsSuccess.ShouldBeTrue();
            var worker = (await Records(host)).ShouldHaveSingleItem();
            worker.Actor.Kind.ShouldBe("Service");
            worker.InitiatingActor.ShouldBeNull();
            worker.CausationAuditId.ShouldBeNull();
            worker.OriginAuditId.ShouldBe(worker.AuditId);
            worker.CorrelationId.ShouldBe(occurrence.CorrelationId);
        }

        parent.Complete();
        await parent.DisposeAsync();
    }

    [Fact]
    public async Task WaitingQueueMessage_DoesNotInventAConsumerOrHandler()
    {
        await using var host = Host();
        var outcome = QueueProcessingResult.Failed;
        var request = new QueueMessageRequest(new CustomerQueueMessage(), value => outcome = value, CancellationToken.None);
        await host.GetRequiredService<IQueueBrokerRuntime>().Process(request);
        outcome.ShouldBe(QueueProcessingResult.WaitingForHandler);
        (await Records(host)).ShouldBeEmpty();
    }

    [Fact]
    public void HandoffKeyRing_CanBeRetainedAcrossPlainWorkerProviderRestarts()
    {
        var path = Path.Combine(Path.GetTempPath(), "audit-handoff-keys-" + Guid.NewGuid().ToString("N"));
        var application = "audit-handoff-" + Guid.NewGuid().ToString("N");
        var link = new AuditExecutionLink { OriginAuditId = Guid.NewGuid(), CausationAuditId = Guid.NewGuid(), CorrelationId = "retained-correlation" };
        try
        {
            string token;
            using (var first = Worker()) token = first.GetRequiredService<IAuditExecutionLinkProtector>().Protect(link, "Jobs", "retained-occurrence");
            using var reopened = Worker();
            reopened.GetRequiredService<IAuditExecutionLinkProtector>().Unprotect(token, "Jobs", "retained-occurrence").ShouldBe(link);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }

        ServiceProvider Worker()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(path)).SetApplicationName(application);
            services.AddAuditing();
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }
    }

    public sealed class BrokenProtector : IAuditExecutionLinkProtector
    {
        public string Protect(AuditExecutionLink link, string feature, string operationId) => throw new InvalidOperationException("private-payload-secret");
        public AuditExecutionLink Unprotect(string token, string feature, string operationId) => throw new InvalidOperationException("private-payload-secret");
    }
    public sealed class InvalidMessage : MessageBase
    {
        public override FluentValidation.Results.ValidationResult Validate() => new([new FluentValidation.Results.ValidationFailure("Name", "private-payload-secret")]);
    }
    public sealed class InvalidQueueMessage : QueueMessageBase
    {
        public override FluentValidation.Results.ValidationResult Validate() => new([new FluentValidation.Results.ValidationFailure("Name", "private-payload-secret")]);
    }
    public sealed class ThrowingConstructorHandler : IMessageHandler<CustomerMessage>
    {
        public ThrowingConstructorHandler(ScopeProbe probe) => throw new InvalidOperationException("private-payload-secret");
        public Task Handle(CustomerMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    public sealed class NestedCustomerHandler(IAuditContext audit) : IMessageHandler<CustomerMessage>
    {
        public async Task Handle(CustomerMessage message, CancellationToken cancellationToken)
        {
            await using var scope = audit.BeginScope(builder => builder.Key("customers.nested"));
            scope.Complete();
        }
    }
    public sealed class NestedCustomerJob(IAuditContext audit) : IJob
    {
        public async Task<IResult> ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            await using var scope = audit.BeginScope(builder => builder.Key("customers.nested"));
            scope.Complete();
            return Result.Success();
        }
    }
    public sealed class FlakyHandler(State state) : IMessageHandler<CustomerMessage>
    {
        public async Task Handle(CustomerMessage message, CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (++state.HandlerCalls == 1) throw new InvalidOperationException("private-payload-secret");
        }
    }
    public sealed class RetryMessageBehavior : IMessageHandlerBehavior
    {
        public async Task Handle<TMessage>(TMessage message, CancellationToken cancellationToken, object handler, MessageHandlerDelegate next) where TMessage : IMessage
        {
            try { await next(); }
            catch (InvalidOperationException) { await next(); }
        }
    }
    public sealed class RejectBeforeJobBehavior : IJobBehavior
    {
        public Task<IResult<JobExecutionResult>> HandleAsync(JobBehaviorContext context, JobBehaviorDelegate next, CancellationToken cancellationToken = default) => throw new InvalidOperationException("private-payload-secret");
    }

    private static ServiceCollection Collection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostApplicationLifetime>());
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        var actor = Substitute.For<ICurrentUserAccessor>();
        actor.IsAuthenticated.Returns(true);
        actor.UserId.Returns("alice");
        actor.UserName.Returns("Alice");
        services.AddSingleton(actor);
        services.AddSingleton<State>();
        services.AddScoped<ScopeProbe>();
        return services;
    }
    private static ServiceProvider Host(bool messageCapture = true, bool jobCapture = true, Action<IServiceCollection> configure = null)
    {
        var services = Collection();
        services.AddAuditing().WithStorageProvider<ObservedStorage>();
        services.AddMessaging().WithInProcessBroker(new InProcessMessageBrokerConfiguration { ProcessDelay = 0 }).WithAuditing(producing: messageCapture, consuming: messageCapture);
        services.AddQueueing().WithInProcessBroker(new InProcessQueueBrokerConfiguration { ProcessDelay = 0 }).WithAuditing(producing: messageCapture, consuming: messageCapture);
        services.AddJobScheduler().WithAuditing(dispatch: jobCapture, execution: jobCapture)
            .WithJob<CustomerJob>("customer-export", job => job.Description("Exports customers.").AddTrigger("manual", trigger => trigger.Manual()).Then("customer-confirm", chain => chain.WithTrigger("manual")))
            .WithJob<CustomerJob>("customer-confirm", job => job.Description("Confirms processing.").AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob<FailedCustomerJob>("customer-fail", job => job.Description("Fails processing.").AddTrigger("manual", trigger => trigger.Manual()));
        services.AddJobScheduler().WithJob<NestedCustomerJob>("customer-nested", job => job.Description("Uses selected nested business capture.").AddTrigger("manual", trigger => trigger.Manual()));
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
    private static async Task<AuditRecord[]> Records(ServiceProvider host)
    {
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var selected = host.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        return await Task.WhenAll((await provider.SearchAsync(selected)).Value.Select(async item => (await provider.FindAsync(item.AuditId)).Value));
    }
    private static void AssertNoSecrets(AuditRecord[] records)
    {
        foreach (var record in records) System.Text.Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("private-payload-secret");
    }
    public sealed class ObservedStorage(AuditingOptions options, State state) : IAuditingStorageProvider
    {
        private readonly InMemoryAuditStorageProvider inner = new(options);
        public async Task<Result<AuditAppendReceipt>> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            var receipt = await this.inner.AppendAsync(record, cancellationToken);
            if (receipt.IsSuccess && record.Kind == "MessageConsumer") state.WorkerStored.TrySetResult(record);
            if (receipt.IsSuccess && record.Key == "customers.nested") state.NestedStored.TrySetResult(record);
            return receipt;
        }
        public Task<Result<AuditRecord>> FindAsync(Guid id, CancellationToken cancellationToken = default) => this.inner.FindAsync(id, cancellationToken);
        public Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default) => this.inner.FlushAsync(cancellationToken);
        public Task<Result<AuditRecord>> FindAsync(Guid id, AuditPredicate scope, CancellationToken cancellationToken = default) => this.inner.FindAsync(id, scope, cancellationToken);
        public Task<Result<AuditExtractionItem>> FindStoredAsync(Guid id, AuditPredicate scope, CancellationToken cancellationToken = default) => this.inner.FindStoredAsync(id, scope, cancellationToken);
        public Task<ResultPaged<AuditRecordSummary>> FindRelatedAsync(AuditRelatedExecutionsQuery query, CancellationToken cancellationToken = default) => this.inner.FindRelatedAsync(query, cancellationToken);
        public Task<ResultPaged<AuditRecordSummary>> SearchAsync(AuditSearchPlan plan, CancellationToken cancellationToken = default) => this.inner.SearchAsync(plan, cancellationToken);
    }
    public sealed class State
    {
        public TaskCompletionSource<AuditRecord> NestedStored { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<AuditRecord> WorkerStored { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Guid? ConstructorAuditId { get; set; }
        public int HandlerCalls { get; set; }
        public int Disposals { get; set; }
    }
    public sealed class ScopeProbe(State state) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { state.Disposals++; return ValueTask.CompletedTask; }
    }
    public sealed class CustomerMessage : MessageBase
    {
        public string Email { get; set; } = "private-payload-secret@example.test";
    }
    public sealed class CustomerQueueMessage : QueueMessageBase
    {
        public string Body { get; set; } = "private-payload-secret";
    }
    public sealed class CustomerHandler : IMessageHandler<CustomerMessage>
    {
        private readonly State state;
        public CustomerHandler(State state, ScopeProbe probe, IAuditContext audit)
        {
            this.state = state;
            state.ConstructorAuditId = audit.Current?.AuditId;
        }
        public Task Handle(CustomerMessage message, CancellationToken cancellationToken) { this.state.HandlerCalls++; return Task.CompletedTask; }
    }
    public sealed class FailingHandler : IMessageHandler<CustomerMessage>
    {
        public Task Handle(CustomerMessage message, CancellationToken cancellationToken) => throw new InvalidOperationException("private-payload-secret");
    }
    public sealed class CustomerQueueHandler(State state) : IQueueMessageHandler<CustomerQueueMessage>
    {
        public Task Handle(CustomerQueueMessage message, CancellationToken cancellationToken) { state.HandlerCalls++; return Task.CompletedTask; }
    }
    public sealed class CustomerJob : IJob
    {
        public Task<IResult> ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken = default) => Task.FromResult<IResult>(Result.Success());
    }
    public sealed class FailedCustomerJob : IJob
    {
        public Task<IResult> ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken = default) => Task.FromResult<IResult>(Result.Failure("private-payload-secret"));
    }
}
