// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;

public class JobSchedulerDocumentationExamplesTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task DatabaseCleanupExample_UsesBuiltInMaintenanceJobDispatch()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithBuiltInMaintenanceJobs());

        var result = await harness.DispatchAndWaitAsync<JobsPurgeHistoryJob>(new JobPurgeHistoryJobData
        {
            RetentionWindow = TimeSpan.FromDays(14),
            BatchSize = 250,
            DryRun = true,
        });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
    }

    [Fact]
    public async Task BatchReprocessingExample_UsesCurrentBatchApis()
    {
        using var harness = this.CreateHarness(
            jobs => jobs
                .WithJob<SuccessfulDocumentationJob>("success-doc-job", job => job
                    .Description("Completes successfully.")
                    .AddTrigger("manual", trigger => trigger.Manual()))
                .WithJob<FailingDocumentationJob>("fail-doc-job", job => job
                    .Description("Fails and can be retried through the batch API.")
                    .AddTrigger("manual", trigger => trigger.Manual())));

        var scheduler = harness.Scheduler;
        var dispatch = await scheduler.DispatchBatchAsync(new JobBatchDispatchRequest
        {
            BatchId = "customer-reprocess-doc-example",
            Description = "Reprocess failed customer exports.",
            Items =
            [
                new JobBatchDispatchItem { JobName = "success-doc-job", Data = Unit.Value },
                new JobBatchDispatchItem { JobName = "fail-doc-job", Data = Unit.Value },
            ],
        });

        foreach (var occurrenceId in dispatch.Value.OccurrenceIds)
        {
            await harness.ExecuteOccurrenceAsync(occurrenceId);
        }

        var retry = await scheduler.RetryBatchAsync(dispatch.Value.BatchId, "Reprocess failed exports");

        dispatch.IsSuccess.ShouldBeTrue();
        retry.IsSuccess.ShouldBeTrue();
        retry.Value.RequestedCount.ShouldBe(1);
        retry.Value.SucceededCount.ShouldBe(1);
    }

    [Fact]
    public async Task ChainingExample_UsesThenWithNormalOccurrences()
    {
        using var harness = this.CreateHarness(
            jobs => jobs
                .WithJob<ImportOrdersDocumentationJob>("import-orders", job => job
                    .Description("Imports orders from the upstream system.")
                    .AddTrigger("manual", trigger => trigger.Manual())
                    .Then("index-orders", chain => chain.WithTrigger("manual")))
                .WithJob<IndexOrdersDocumentationJob>("index-orders", job => job
                    .Description("Rebuilds the order search index.")
                    .AddTrigger("manual", trigger => trigger.Manual())));

        var execution = await harness.DispatchAndWaitAsync<ImportOrdersDocumentationJob>();
        var successor = await harness.FindOccurrenceAsync("index-orders", "manual");

        execution.IsSuccess.ShouldBeTrue();
        execution.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        successor.ShouldNotBeNull();
        successor.Status.ShouldBe(JobOccurrenceStatus.Due);
    }

    [Fact]
    public async Task OutboundIntegrationExample_UsesNotifierHelper()
    {
        var notifier = new RecordingNotifier();
        using var harness = this.CreateHarness(
            jobs => jobs.WithNotifierJob<DocumentationNotificationData, DocumentationNotification>("notify-export-complete", job => job
                .WithDescription("Publishes a customer export completion notification.")
                .WithNotification(context => new DocumentationNotification(context.Data.ExportId))
                .MapCorrelationId()
                .MapProperty("tenant")
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<INotifier>(notifier));

        var result = await harness.DispatchAndWaitAsync<NotifierJob<DocumentationNotificationData, DocumentationNotification>>(
            new DocumentationNotificationData { ExportId = "exp-42" },
            new JobDispatchOptions
            {
                CorrelationId = "corr-42",
                Properties = new PropertyBag { ["tenant"] = "alpha" },
            });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        notifier.Calls.Count.ShouldBe(1);
        notifier.Calls[0].Notification.ExportId.ShouldBe("exp-42");
        notifier.Calls[0].Options.Context.Properties["CorrelationId"].ShouldBe("corr-42");
        notifier.Calls[0].Options.Context.Properties["tenant"].ShouldBe("alpha");
    }

    private sealed class SuccessfulDocumentationJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class FailingDocumentationJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure().WithError(new ValidationError("planned failure")));
    }

    private sealed class ImportOrdersDocumentationJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class IndexOrdersDocumentationJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class DocumentationNotificationData
    {
        public string ExportId { get; set; }
    }

    private sealed class DocumentationNotification(string exportId) : NotificationBase
    {
        public string ExportId { get; } = exportId;
    }

    private sealed class RecordingNotifier : INotifier
    {
        public List<RecordedNotification> Calls { get; } = [];

        public Task<IResult> PublishAsync<TNotification>(TNotification notification, PublishOptions options = null, CancellationToken cancellationToken = default)
            where TNotification : class, INotification
        {
            this.Calls.Add(new RecordedNotification((DocumentationNotification)(object)notification, options ?? new PublishOptions()));
            return Task.FromResult<IResult>(Result.Success());
        }

        public Task<IResult> PublishDynamicAsync(INotification notification, PublishOptions options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public RegistrationInformation GetRegistrationInformation() => new(new Dictionary<string, IReadOnlyList<string>>(), []);
    }

    private sealed record RecordedNotification(DocumentationNotification Notification, PublishOptions Options);
}
