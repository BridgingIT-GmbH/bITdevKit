// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;

public class JobSchedulerIntegrationTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task RequestSendJob_AliasDispatchesMappedRequest()
    {
        var requester = new RecordingRequester();

        using var harness = this.CreateHarness(
            jobs => jobs.WithRequestSendJob<IntegrationJobData, IntegrationRequest, string>("request-job-alias", job => job
                .WithDescription("dispatch request through alias")
                .WithRequest(context => new IntegrationRequest { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IRequester>(requester));

        var result = await harness.DispatchAndWaitAsync<RequesterJob<IntegrationJobData, IntegrationRequest, string>>(
            new IntegrationJobData { Payload = "alias-request" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        requester.Calls.Count.ShouldBe(1);
        requester.Calls[0].Request.Payload.ShouldBe("alias-request");
    }

    [Fact]
    public async Task CommandJob_AliasDispatchesResultCommand()
    {
        var requester = new RecordingCommandRequester();

        using var harness = this.CreateHarness(
            jobs => jobs.WithCommandJob<IntegrationJobData, IntegrationCommand>("command-job-alias", job => job
                .WithDescription("dispatch command through alias")
                .WithRequest(context => new IntegrationCommand { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IRequester>(requester));

        var result = await harness.DispatchAndWaitAsync<RequesterJob<IntegrationJobData, IntegrationCommand, Result>>(
            new IntegrationJobData { Payload = "alias-command" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        requester.Payloads.ShouldBe(["alias-command"]);
    }

    [Fact]
    public async Task NotificationPublishJob_AliasPublishesMappedNotification()
    {
        var notifier = new RecordingNotifier();

        using var harness = this.CreateHarness(
            jobs => jobs.WithNotificationPublishJob<IntegrationJobData, IntegrationNotification>("notify-job-alias", job => job
                .WithDescription("publish notification through alias")
                .WithNotification(context => new IntegrationNotification { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<INotifier>(notifier));

        var result = await harness.DispatchAndWaitAsync<NotifierJob<IntegrationJobData, IntegrationNotification>>(
            new IntegrationJobData { Payload = "alias-notification" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        notifier.Calls.Count.ShouldBe(1);
        notifier.Calls[0].Notification.Payload.ShouldBe("alias-notification");
    }

    [Fact]
    public async Task RequestSendJob_AliasWithoutRegisteredRequester_FailsClearly()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithRequestSendJob<IntegrationJobData, IntegrationRequest, string>("missing-requester-alias", job => job
                .WithDescription("missing requester alias")
                .WithRequest(context => new IntegrationRequest { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())));

        var result = await harness.DispatchAndWaitAsync<RequesterJob<IntegrationJobData, IntegrationRequest, string>>(
            new IntegrationJobData { Payload = "missing" });
        var execution = (await harness.GetExecutionsAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        execution.Message.ShouldContain("IRequester is not registered");
    }

    [Fact]
    public async Task CommandJob_AliasWithoutRegisteredRequester_FailsClearly()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithCommandJob<IntegrationJobData, IntegrationCommand>("missing-command-alias", job => job
                .WithDescription("missing command requester alias")
                .WithRequest(context => new IntegrationCommand { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())));

        var result = await harness.DispatchAndWaitAsync<RequesterJob<IntegrationJobData, IntegrationCommand, Result>>(
            new IntegrationJobData { Payload = "missing" });
        var execution = (await harness.GetExecutionsAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        execution.Message.ShouldContain("IRequester is not registered");
    }

    [Fact]
    public async Task NotificationPublishJob_AliasWithoutRegisteredNotifier_FailsClearly()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithNotificationPublishJob<IntegrationJobData, IntegrationNotification>("missing-notifier-alias", job => job
                .WithDescription("missing notifier alias")
                .WithNotification(context => new IntegrationNotification { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())));

        var result = await harness.DispatchAndWaitAsync<NotifierJob<IntegrationJobData, IntegrationNotification>>(
            new IntegrationJobData { Payload = "missing" });
        var execution = (await harness.GetExecutionsAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        execution.Message.ShouldContain("INotifier is not registered");
    }

    [Fact]
    public async Task RequesterJob_SendsMappedRequest_AndMapsExecutionContextIntoSendOptions()
    {
        var requester = new RecordingRequester();
        using var harness = this.CreateHarness(
            jobs => jobs.WithRequesterJob<IntegrationJobData, IntegrationRequest, string>("request-job", job => job
                .WithDescription("dispatch request")
                .WithRequest(context => new IntegrationRequest
                {
                    Payload = context.Data.Payload,
                    PreviousSuccessfulAttemptNumber = context.PreviousSuccessfulExecution?.AttemptNumber,
                    PreviousSuccessfulStatus = context.PreviousSuccessfulExecution?.Status.ToString(),
                })
                .MapCorrelationId()
                .MapProperty("tenant")
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IRequester>(requester));

        var first = await harness.DispatchAndWaitAsync<RequesterJob<IntegrationJobData, IntegrationRequest, string>>(
            new IntegrationJobData { Payload = "first" },
            new JobDispatchOptions
            {
                CorrelationId = "corr-1",
                Properties = new PropertyBag { ["tenant"] = "alpha" },
            });

        harness.Advance(TimeSpan.FromSeconds(1));

        var second = await harness.DispatchAndWaitAsync<RequesterJob<IntegrationJobData, IntegrationRequest, string>>(
            new IntegrationJobData { Payload = "second" },
            new JobDispatchOptions
            {
                CorrelationId = "corr-2",
                Properties = new PropertyBag { ["tenant"] = "beta" },
            });

        first.IsSuccess.ShouldBeTrue();
        first.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        second.IsSuccess.ShouldBeTrue();
        second.Value.Status.ShouldBe(JobExecutionStatus.Completed);

        requester.Calls.Count.ShouldBe(2);
        requester.Calls[0].Request.Payload.ShouldBe("first");
        requester.Calls[0].Request.PreviousSuccessfulAttemptNumber.ShouldBeNull();
        requester.Calls[1].Request.Payload.ShouldBe("second");
        requester.Calls[1].Request.PreviousSuccessfulAttemptNumber.ShouldBe(1);
        requester.Calls[1].Request.PreviousSuccessfulStatus.ShouldBe(nameof(JobExecutionStatus.Completed));
        requester.Calls[1].Options.Context.ShouldNotBeNull();
        requester.Calls[1].Options.Context.Properties["CorrelationId"].ShouldBe("corr-2");
        requester.Calls[1].Options.Context.Properties["tenant"].ShouldBe("beta");
    }

    [Fact]
    public async Task NotifierJob_PublishesMappedNotification_AndMapsMetadataIntoPublishOptions()
    {
        var notifier = new RecordingNotifier();
        using var harness = this.CreateHarness(
            jobs => jobs.WithNotifierJob<IntegrationJobData, IntegrationNotification>("notify-job", job => job
                .WithDescription("publish notification")
                .WithNotification(context => new IntegrationNotification
                {
                    Payload = context.Data.Payload,
                })
                .MapCorrelationId()
                .MapProperty("tenant", "Tenant")
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<INotifier>(notifier));

        var result = await harness.DispatchAndWaitAsync<NotifierJob<IntegrationJobData, IntegrationNotification>>(
            new IntegrationJobData { Payload = "hello" },
            new JobDispatchOptions
            {
                CorrelationId = "corr-9",
                Properties = new PropertyBag { ["tenant"] = "omega" },
            });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        notifier.Calls.Count.ShouldBe(1);
        notifier.Calls[0].Notification.Payload.ShouldBe("hello");
        notifier.Calls[0].Options.Context.ShouldNotBeNull();
        notifier.Calls[0].Options.Context.Properties["CorrelationId"].ShouldBe("corr-9");
        notifier.Calls[0].Options.Context.Properties["Tenant"].ShouldBe("omega");
    }

    [Fact]
    public async Task RequesterJob_WithoutRegisteredRequester_FailsClearly()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithRequesterJob<IntegrationJobData, IntegrationRequest, string>("missing-requester", job => job
                .WithDescription("missing requester")
                .WithRequest(context => new IntegrationRequest { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())));

        var result = await harness.DispatchAndWaitAsync<RequesterJob<IntegrationJobData, IntegrationRequest, string>>(
            new IntegrationJobData { Payload = "missing" });
        var occurrence = await harness.GetOccurrenceAsync(result.Value.OccurrenceId);
        var execution = (await harness.GetExecutionsAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Failed);
        execution.Message.ShouldContain("IRequester is not registered");
    }

    [Fact]
    public async Task NotifierJob_WithoutRegisteredNotifier_FailsClearly()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithNotifierJob<IntegrationJobData, IntegrationNotification>("missing-notifier", job => job
                .WithDescription("missing notifier")
                .WithNotification(context => new IntegrationNotification { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())));

        var result = await harness.DispatchAndWaitAsync<NotifierJob<IntegrationJobData, IntegrationNotification>>(
            new IntegrationJobData { Payload = "missing" });
        var occurrence = await harness.GetOccurrenceAsync(result.Value.OccurrenceId);
        var execution = (await harness.GetExecutionsAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        occurrence.Status.ShouldBe(JobOccurrenceStatus.Failed);
        execution.Message.ShouldContain("INotifier is not registered");
    }

    [Fact]
    public async Task RequesterJob_UsesNormalRetryHistoryAndCompletionFlow()
    {
        var requester = new RecordingRequester(failuresBeforeSuccess: 1);
        using var harness = this.CreateHarness(
            jobs => jobs.WithRequesterJob<IntegrationJobData, IntegrationRequest, string>("retry-request-job", job => job
                .WithDescription("retry request")
                .WithRequest(context => new IntegrationRequest { Payload = context.Data.Payload })
                .WithRetry(retry => retry.MaxAttempts(2).FixedDelay(TimeSpan.FromMinutes(5)))
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IRequester>(requester));

        var dispatch = await harness.DispatchAsync<RequesterJob<IntegrationJobData, IntegrationRequest, string>>(
            new IntegrationJobData { Payload = "retry-me" });

        dispatch.IsSuccess.ShouldBeTrue();
        var firstAttempt = await harness.ExecuteOccurrenceAsync(dispatch.Value.OccurrenceId);
        firstAttempt.IsSuccess.ShouldBeTrue();
        firstAttempt.Value.Status.ShouldBe(JobExecutionStatus.Retried);
        await harness.AssertRetryAttemptsAsync(dispatch.Value.OccurrenceId, 1, JobOccurrenceStatus.RetryScheduled);
        requester.Calls.Count.ShouldBe(1);

        harness.Advance(TimeSpan.FromMinutes(5));
        await harness.SweepAsync();

        await harness.AssertRetryAttemptsAsync(dispatch.Value.OccurrenceId, 2, JobOccurrenceStatus.Completed);
        await harness.AssertHistoryContainsAsync(dispatch.Value.OccurrenceId, "ExecutionRetried");
        requester.Calls.Count.ShouldBe(2);
    }

    private sealed class RecordingRequester(int failuresBeforeSuccess = 0) : IRequester
    {
        private int remainingFailures = failuresBeforeSuccess;

        public List<RecordedRequest> Calls { get; } = [];

        public Task<Result<TValue>> SendAsync<TRequest, TValue>(
            TRequest request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            where TRequest : class, IRequest<TValue>
        {
            var typedRequest = request as IntegrationRequest ?? throw new NotSupportedException($"Unsupported request type '{typeof(TRequest).FullName}'.");
            this.Calls.Add(new RecordedRequest(typedRequest, options ?? new SendOptions()));

            if (this.remainingFailures > 0)
            {
                this.remainingFailures--;
                return Task.FromResult((Result<TValue>)(object)Result<string>.Failure().WithError(new ValidationError("planned requester failure")));
            }

            return Task.FromResult((Result<TValue>)(object)Result<string>.Success("ok"));
        }

        public Task<Result<TValue>> SendAsync<TValue>(
            IRequest<TValue> request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<TValue>> SendAsync<TValue>(
            RequestBase<TValue> request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SendDynamicAsync(
            IRequest request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<TValue>> SendDynamicAsync<TValue>(
            IRequest<TValue> request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public RegistrationInformation GetRegistrationInformation() => new(new Dictionary<string, IReadOnlyList<string>>(), []);
    }

    private sealed class RecordingCommandRequester : IRequester
    {
        public List<string> Payloads { get; } = [];

        public Task<Result<TValue>> SendAsync<TRequest, TValue>(
            TRequest request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            where TRequest : class, IRequest<TValue>
        {
            var typedRequest = request as IntegrationCommand ?? throw new NotSupportedException($"Unsupported request type '{typeof(TRequest).FullName}'.");
            this.Payloads.Add(typedRequest.Payload);
            return Task.FromResult((Result<TValue>)(object)Result<Result>.Success(Result.Success()));
        }

        public Task<Result<TValue>> SendAsync<TValue>(
            IRequest<TValue> request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<TValue>> SendAsync<TValue>(
            RequestBase<TValue> request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> SendDynamicAsync(
            IRequest request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<TValue>> SendDynamicAsync<TValue>(
            IRequest<TValue> request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public RegistrationInformation GetRegistrationInformation() => new(new Dictionary<string, IReadOnlyList<string>>(), []);
    }

    private sealed class RecordingNotifier : INotifier
    {
        public List<RecordedNotification> Calls { get; } = [];

        public Task<IResult> PublishAsync<TNotification>(
            TNotification notification,
            PublishOptions options = null,
            CancellationToken cancellationToken = default)
            where TNotification : class, INotification
        {
            var typedNotification = notification as IntegrationNotification ?? throw new NotSupportedException($"Unsupported notification type '{typeof(TNotification).FullName}'.");
            this.Calls.Add(new RecordedNotification(typedNotification, options ?? new PublishOptions()));
            return Task.FromResult<IResult>(Result.Success());
        }

        public Task<IResult> PublishDynamicAsync(
            INotification notification,
            PublishOptions options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public RegistrationInformation GetRegistrationInformation() => new(new Dictionary<string, IReadOnlyList<string>>(), []);
    }

    private sealed record RecordedRequest(IntegrationRequest Request, SendOptions Options);

    private sealed record RecordedNotification(IntegrationNotification Notification, PublishOptions Options);

    private sealed class IntegrationJobData
    {
        public string Payload { get; set; }
    }

    private sealed class IntegrationRequest : RequestBase<string>
    {
        public string Payload { get; set; }

        public int? PreviousSuccessfulAttemptNumber { get; set; }

        public string PreviousSuccessfulStatus { get; set; }
    }

    private sealed class IntegrationCommand : RequestBase<Result>
    {
        public string Payload { get; set; }
    }

    private sealed class IntegrationNotification : NotificationBase
    {
        public string Payload { get; set; }
    }
}
