// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;

public class JobSchedulerEventAndMaintenanceTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task AcceptedNotifierEvent_MaterializesOneOccurrenceIdempotently()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<NotifierTriggeredJob>("notifier-event-job", job => job
                .Description("handles accepted notifier events")
                .WithData<TestNotification>()
                .AddTrigger("accepted", trigger => trigger.Event<TestNotification>(JobEventSourceNames.Notifier))),
            services => services.AddNotifier()
                .AddHandler<TestNotification, TestNotificationHandler>()
                .UseJobSchedulerEventTriggers());

        var notifier = harness.Services.GetRequiredService<INotifier>();
        var publish = await notifier.PublishAsync(
            new TestNotification { Payload = "alpha" },
            new PublishOptions
            {
                Context = new RequestContext
                {
                    Properties =
                    {
                        [JobEventContextPropertyNames.IdempotencyKey] = "evt-001",
                        [JobEventContextPropertyNames.CorrelationId] = "corr-001",
                    },
                },
            });

        var materialized = await harness.MaterializeAsync();
        var occurrences = await harness.Services.GetRequiredService<IJobStoreProvider>().Queries.ListOccurrencesAsync();

        publish.IsSuccess.ShouldBeTrue();
        materialized.IsSuccess.ShouldBeTrue();
        occurrences.Count.ShouldBe(1);
        occurrences[0].JobName.ShouldBe("notifier-event-job");
        occurrences[0].TriggerType.ShouldBe(JobTriggerType.Event);
        occurrences[0].IdempotencyKey.ShouldBe("evt-001");
        occurrences[0].Properties["jobs.event.correlationId"].ShouldBe("corr-001");
    }

    [Fact]
    public async Task DuplicateAcceptedEvent_DoesNotCreateDuplicateOccurrence()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<NotifierTriggeredJob>("duplicate-event-job", job => job
                .Description("deduplicates accepted notifier events")
                .WithData<TestNotification>()
                .AddTrigger("accepted", trigger => trigger.Event<TestNotification>(JobEventSourceNames.Notifier))),
            services => services.AddNotifier()
                .AddHandler<TestNotification, TestNotificationHandler>()
                .UseJobSchedulerEventTriggers());

        var notifier = harness.Services.GetRequiredService<INotifier>();
        var options = new PublishOptions
        {
            Context = new RequestContext
            {
                Properties = { [JobEventContextPropertyNames.IdempotencyKey] = "evt-dup" },
            },
        };

        (await notifier.PublishAsync(new TestNotification { Payload = "first" }, options)).IsSuccess.ShouldBeTrue();
        (await notifier.PublishAsync(new TestNotification { Payload = "second" }, options)).IsSuccess.ShouldBeTrue();

        var materialized = await harness.MaterializeAsync();
        var occurrences = await harness.Services.GetRequiredService<IJobStoreProvider>().Queries.ListOccurrencesAsync();

        materialized.IsSuccess.ShouldBeTrue();
        occurrences.Count.ShouldBe(1);
        occurrences[0].IdempotencyKey.ShouldBe("evt-dup");
    }

    [Fact]
    public async Task NotifierAdapterFailure_ReturnsFailedResult()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<NotifierTriggeredJob>("failing-notifier-event-job", job => job
                .Description("fails when the notifier adapter cannot accept the event")
                .WithData<TestNotification>()
                .AddTrigger("accepted", trigger => trigger.Event<TestNotification>(JobEventSourceNames.Notifier))),
            services =>
            {
                services.AddSingleton<IJobEventIngress>(new FailingEventIngress());
                services.AddNotifier()
                    .AddHandler<TestNotification, TestNotificationHandler>()
                    .UseJobSchedulerEventTriggers();
            });

        var notifier = harness.Services.GetRequiredService<INotifier>();
        var result = await notifier.PublishAsync(new TestNotification { Payload = "fail" });

        result.IsFailure.ShouldBeTrue();
        result.Messages.Any(x => x.Contains("planned accept failure", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    [Fact]
    public async Task MissingOptionalEventAdapter_FailsClearlyDuringMaterialization()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithJob<MissingAdapterJob>("missing-messaging-adapter", job => job
                .Description("requires optional messaging adapter")
                .WithData<TestExternalEvent>()
                .AddTrigger("accepted", trigger => trigger.Event<TestExternalEvent>(JobEventSourceNames.Messaging))));

        var materialized = await harness.MaterializeAsync();

        materialized.IsFailure.ShouldBeTrue();
        materialized.Errors.First().Message.ShouldContain("requires the 'messaging' event adapter");
    }

    [Fact]
    public async Task MaintenanceJob_RespectsBatchLimit_AndWritesHistoryDiagnostics()
    {
        using var harness = this.CreateHarness(
            jobs => jobs
                .WithJob<LeaseTargetJob>("lease-target", job => job
                    .Description("creates occurrences for lease maintenance tests")
                    .AddTrigger("manual", trigger => trigger.Manual()))
                .WithBuiltInMaintenanceJobs());

        var firstDispatch = await harness.DispatchAsync<LeaseTargetJob>();
        var secondDispatch = await harness.DispatchAsync<LeaseTargetJob>();
        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var nowUtc = harness.Clock.GetUtcNow();
        var first = await store.Occurrences.GetAsync(firstDispatch.Value.OccurrenceId);
        var second = await store.Occurrences.GetAsync(secondDispatch.Value.OccurrenceId);

        await store.Occurrences.UpdateAsync(first with { Status = JobOccurrenceStatus.Running, UpdatedDate = nowUtc.AddMinutes(-10) });
        await store.Occurrences.UpdateAsync(second with { Status = JobOccurrenceStatus.Running, UpdatedDate = nowUtc.AddMinutes(-10) });
        await store.Leases.UpsertAsync(new JobLeaseRecord
        {
            OccurrenceId = first.OccurrenceId,
            SchedulerInstanceId = "node-a",
            OwnershipToken = "lease-a",
            AcquiredUtc = nowUtc.AddMinutes(-20),
            ExpiresUtc = nowUtc.AddMinutes(-2),
            RenewalCount = 0,
            CreatedDate = nowUtc.AddMinutes(-20),
            UpdatedDate = nowUtc.AddMinutes(-2),
        });
        await store.Leases.UpsertAsync(new JobLeaseRecord
        {
            OccurrenceId = second.OccurrenceId,
            SchedulerInstanceId = "node-b",
            OwnershipToken = "lease-b",
            AcquiredUtc = nowUtc.AddMinutes(-20),
            ExpiresUtc = nowUtc.AddMinutes(-1),
            RenewalCount = 0,
            CreatedDate = nowUtc.AddMinutes(-20),
            UpdatedDate = nowUtc.AddMinutes(-1),
        });

        var result = await harness.DispatchAndWaitAsync<JobsReleaseExpiredLeasesJob>(new JobReleaseExpiredLeasesJobData { BatchSize = 1 });
        var updatedFirst = await store.Occurrences.GetAsync(first.OccurrenceId);
        var updatedSecond = await store.Occurrences.GetAsync(second.OccurrenceId);
        var remainingLeases = await store.Leases.ListAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        result.Value.Messages.Any(x => x.Contains("jobs-release-expired-leases", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        updatedFirst.Status.ShouldBe(JobOccurrenceStatus.Due);
        updatedSecond.Status.ShouldBe(JobOccurrenceStatus.Running);
        remainingLeases.Count.ShouldBe(1);
        remainingLeases[0].OccurrenceId.ShouldBe(second.OccurrenceId);
        await harness.AssertHistoryContainsAsync(first.OccurrenceId, "LeaseExpired");
    }

    [Fact]
    public async Task MaintenanceDryRun_RecordsWhatWouldChange_WithoutMutatingTargetData()
    {
        using var harness = this.CreateHarness(
            jobs => jobs
                .WithJob<LeaseTargetJob>("lease-target", job => job
                    .Description("creates occurrences for lease maintenance tests")
                    .AddTrigger("manual", trigger => trigger.Manual()))
                .WithBuiltInMaintenanceJobs());

        var dispatch = await harness.DispatchAsync<LeaseTargetJob>();
        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var nowUtc = harness.Clock.GetUtcNow();
        var occurrence = await store.Occurrences.GetAsync(dispatch.Value.OccurrenceId);
        await store.Occurrences.UpdateAsync(occurrence with { Status = JobOccurrenceStatus.Running, UpdatedDate = nowUtc.AddMinutes(-10) });
        await store.Leases.UpsertAsync(new JobLeaseRecord
        {
            OccurrenceId = occurrence.OccurrenceId,
            SchedulerInstanceId = "node-a",
            OwnershipToken = "lease-a",
            AcquiredUtc = nowUtc.AddMinutes(-20),
            ExpiresUtc = nowUtc.AddMinutes(-1),
            RenewalCount = 0,
            CreatedDate = nowUtc.AddMinutes(-20),
            UpdatedDate = nowUtc.AddMinutes(-1),
        });

        var result = await harness.DispatchAndWaitAsync<JobsReleaseExpiredLeasesJob>(new JobReleaseExpiredLeasesJobData { DryRun = true, BatchSize = 1 });
        var updatedOccurrence = await store.Occurrences.GetAsync(occurrence.OccurrenceId);
        var leases = await store.Leases.ListAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Messages.Any(x => x.Contains("would process 1 expired leases", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        updatedOccurrence.Status.ShouldBe(JobOccurrenceStatus.Running);
        leases.Count.ShouldBe(1);
        leases[0].OccurrenceId.ShouldBe(occurrence.OccurrenceId);
    }

    [Fact]
    public async Task ArchiveMaintenanceJob_ArchivesWithinBatchLimit_AndWritesDiagnostics()
    {
        using var harness = this.CreateHarness(
            jobs => jobs
                .WithJob<LeaseTargetJob>("archive-target", job => job
                    .Description("creates occurrences for archive maintenance tests")
                    .AddTrigger("manual", trigger => trigger.Manual()))
                .WithBuiltInMaintenanceJobs());

        var firstDispatch = await harness.DispatchAsync("archive-target");
        var secondDispatch = await harness.DispatchAsync("archive-target");
        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var nowUtc = harness.Clock.GetUtcNow();
        var first = await store.Occurrences.GetAsync(firstDispatch.Value.OccurrenceId);
        var second = await store.Occurrences.GetAsync(secondDispatch.Value.OccurrenceId);

        await store.Occurrences.UpdateAsync(first with { Status = JobOccurrenceStatus.Completed, UpdatedDate = nowUtc.AddDays(-10) });
        await store.Occurrences.UpdateAsync(second with { Status = JobOccurrenceStatus.Completed, UpdatedDate = nowUtc.AddHours(-2) });

        var result = await harness.DispatchAndWaitAsync<JobsArchiveOccurrencesJob>(new JobArchiveOccurrencesJobData
        {
            RetentionWindow = TimeSpan.FromDays(1),
            BatchSize = 1,
            JobName = "archive-target",
        });

        var updatedFirst = await store.Occurrences.GetAsync(first.OccurrenceId);
        var updatedSecond = await store.Occurrences.GetAsync(second.OccurrenceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        result.Value.Messages.Any(x => x.Contains("jobs-archive-occurrences", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        updatedFirst.Status.ShouldBe(JobOccurrenceStatus.Archived);
        updatedSecond.Status.ShouldBe(JobOccurrenceStatus.Completed);
        await harness.AssertHistoryContainsAsync(first.OccurrenceId, "OccurrenceArchived");
    }

    private sealed class NotifierTriggeredJob : JobBase<TestNotification>
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<TestNotification> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class LeaseTargetJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class MissingAdapterJob : JobBase<TestExternalEvent>
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<TestExternalEvent> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class TestNotification : NotificationBase
    {
        public string Payload { get; set; }
    }

    private sealed class TestExternalEvent
    {
        public string Payload { get; set; }
    }

    private sealed class TestNotificationHandler : INotificationHandler<TestNotification>
    {
        public Task<Result> HandleAsync(TestNotification notification, PublishOptions options, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success());
    }

    private sealed class FailingEventIngress : IJobEventIngress
    {
        public Task<IResult<JobAcceptedEvent>> AcceptAsync(string source, object data, Type dataType, JobAcceptedEventOptions options = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IResult<JobAcceptedEvent>>(Result<JobAcceptedEvent>.Failure().WithMessage("planned accept failure"));

        public Task<IResult<JobAcceptedEvent>> AcceptAsync<TEvent>(string source, TEvent data, JobAcceptedEventOptions options = null, CancellationToken cancellationToken = default)
            where TEvent : class
            => Task.FromResult<IResult<JobAcceptedEvent>>(Result<JobAcceptedEvent>.Failure().WithMessage("planned accept failure"));
    }
}
