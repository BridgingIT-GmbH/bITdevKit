// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

public partial class OrchestrationAdvancedWorkflowTests
{
    [Fact]
    public async Task DispatchAsync_WhenQueryActivitySucceeds_MapsResponseIntoContext()
    {
        var requester = Substitute.For<IRequester>();
        SendOptions capturedOptions = null;
        requester.SendAsync<TestQueryRequest, string>(Arg.Any<TestQueryRequest>(), Arg.Any<SendOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedOptions = call.ArgAt<SendOptions>(1);
                return Task.FromResult(Result<string>.Success("customer-42"));
            });

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => requester))
            .WithOrchestration<QueryRequestWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<QueryRequestWorkflowOrchestration, RequestActivityWorkflowData>(new RequestActivityWorkflowData
        {
            OrderId = "order-123",
            Payload = "customer-42",
        });
        var context = await sut.GetContextAsync<RequestActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.QueryResult.ShouldBe("customer-42");
        capturedOptions.ShouldNotBeNull();
        capturedOptions.Context.ShouldNotBeNull();
        capturedOptions.Context.Properties["CorrelationId"].ShouldBe(context.CorrelationId);
        capturedOptions.Context.Properties["OrderId"].ShouldBe("order-123");
    }

    [Fact]
    public async Task DispatchAsync_WhenQueryActivityRequesterIsMissing_FailsActivity()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .WithOrchestration<QueryRequestWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<QueryRequestWorkflowOrchestration, RequestActivityWorkflowData>(new RequestActivityWorkflowData
        {
            OrderId = "order-123",
            Payload = "customer-42",
        });
        var context = await sut.GetContextAsync<RequestActivityWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("IRequester is not registered");
        history.ShouldContain(item => item.EventType == "ActivityFailed");
    }

    [Fact]
    public async Task DispatchAsync_WhenCommandActivitySucceedsWithoutMapping_ContinuesWithoutMutatingContext()
    {
        var requester = Substitute.For<IRequester>();
        requester.SendAsync<TestCommandRequest, string>(Arg.Any<TestCommandRequest>(), Arg.Any<SendOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.Success("accepted")));

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => requester))
            .WithOrchestration<CommandRequestWithoutMappingWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<CommandRequestWithoutMappingWorkflowOrchestration, RequestActivityWorkflowData>(new RequestActivityWorkflowData
        {
            Payload = "accepted",
        });
        var context = await sut.GetContextAsync<RequestActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.CommandResult.ShouldBeNull();
    }

    [Fact]
    public async Task DispatchAsync_WhenCommandActivitySucceedsWithMapping_MapsResponseIntoContext()
    {
        var requester = Substitute.For<IRequester>();
        requester.SendAsync<TestCommandRequest, string>(Arg.Any<TestCommandRequest>(), Arg.Any<SendOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.Success("command-done")));

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => requester))
            .WithOrchestration<CommandRequestWithMappingWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<CommandRequestWithMappingWorkflowOrchestration, RequestActivityWorkflowData>(new RequestActivityWorkflowData
        {
            Payload = "command-done",
        });
        var context = await sut.GetContextAsync<RequestActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.CommandResult.ShouldBe("command-done");
    }

    [Fact]
    public async Task DispatchAsync_WhenCommandActivityReturnsFailedResult_FailsActivity()
    {
        var requester = Substitute.For<IRequester>();
        requester.SendAsync<TestCommandRequest, string>(Arg.Any<TestCommandRequest>(), Arg.Any<SendOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.Failure().WithError(new Error("command failed"))));

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => requester))
            .WithOrchestration<CommandRequestWithMappingWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<CommandRequestWithMappingWorkflowOrchestration, RequestActivityWorkflowData>(new RequestActivityWorkflowData
        {
            Payload = "command-done",
        });
        var context = await sut.GetContextAsync<RequestActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("command failed");
    }

    [Fact]
    public async Task DispatchAsync_WhenSendRequestActivitySucceeds_MapsResponseIntoContext()
    {
        var requester = Substitute.For<IRequester>();
        requester.SendAsync<TestSendRequest, string>(Arg.Any<TestSendRequest>(), Arg.Any<SendOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.Success("request-done")));

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => requester))
            .WithOrchestration<SendRequestWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<SendRequestWorkflowOrchestration, RequestActivityWorkflowData>(new RequestActivityWorkflowData
        {
            Payload = "request-done",
        });
        var context = await sut.GetContextAsync<RequestActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.RequestResult.ShouldBe("request-done");
    }

    [Fact]
    public async Task DispatchAsync_WhenSendRequestActivityRequesterThrows_FailsActivity()
    {
        var requester = Substitute.For<IRequester>();
        requester.SendAsync<TestSendRequest, string>(Arg.Any<TestSendRequest>(), Arg.Any<SendOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<Result<string>>(new InvalidOperationException("requester exploded")));

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => requester))
            .WithOrchestration<SendRequestWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<SendRequestWorkflowOrchestration, RequestActivityWorkflowData>(new RequestActivityWorkflowData
        {
            Payload = "request-done",
        });
        var context = await sut.GetContextAsync<RequestActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("requester exploded");
    }

    [Fact]
    public async Task DispatchAsync_WhenPublishNotificationActivitySucceeds_AppliesOptionsAndContinues()
    {
        var notifier = Substitute.For<INotifier>();
        PublishOptions capturedOptions = null;
        notifier.PublishAsync(Arg.Any<TestWorkflowNotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedOptions = call.ArgAt<PublishOptions>(1);
                return Task.FromResult<IResult>(Result.Success());
            });

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => notifier))
            .WithOrchestration<PublishNotificationWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<PublishNotificationWorkflowOrchestration, NotificationActivityWorkflowData>(new NotificationActivityWorkflowData
        {
            OrderId = "order-789",
            Payload = "notify",
        });
        var context = await sut.GetContextAsync<NotificationActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        capturedOptions.ShouldNotBeNull();
        capturedOptions.ExecutionMode.ShouldBe(ExecutionMode.FireAndForget);
        capturedOptions.Context.ShouldNotBeNull();
        capturedOptions.Context.Properties["CorrelationId"].ShouldBe(context.CorrelationId);
        capturedOptions.Context.Properties["OrderId"].ShouldBe("order-789");
    }

    [Fact]
    public async Task DispatchAsync_WhenPublishNotificationActivityNotifierIsMissing_FailsActivity()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .WithOrchestration<PublishNotificationWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<PublishNotificationWorkflowOrchestration, NotificationActivityWorkflowData>(new NotificationActivityWorkflowData
        {
            OrderId = "order-789",
            Payload = "notify",
        });
        var context = await sut.GetContextAsync<NotificationActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("INotifier is not registered");
    }

    [Fact]
    public async Task AdvanceTimeAsync_WhenQueryActivityUsesRetryPolicy_RetriesUntilRequesterSucceeds()
    {
        var attempts = 0;
        var requester = Substitute.For<IRequester>();
        requester.SendAsync<TestQueryRequest, string>(Arg.Any<TestQueryRequest>(), Arg.Any<SendOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromResult(Result<string>.Failure().WithError(new Error("transient query failure")))
                    : Task.FromResult(Result<string>.Success("retried-query"));
            });

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => requester))
            .WithOrchestration<RetryingQueryRequestWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<RetryingQueryRequestWorkflowOrchestration, RequestActivityWorkflowData>(new RequestActivityWorkflowData
        {
            Payload = "retried-query",
        });
        await sut.Assert(dispatch.Value).BeWaitingAsync("RetryingQuery");

        await sut.AdvanceTimeAsync(TimeSpan.FromMinutes(1));

        var context = await sut.GetContextAsync<RequestActivityWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.QueryResult.ShouldBe("retried-query");
        history.Count(item => item.EventType == "ActivityRetryScheduled").ShouldBe(1);
    }

    [Fact]
    public async Task AdvanceTimeAsync_WhenPublishNotificationActivityUsesRetryPolicy_RetriesUntilNotifierSucceeds()
    {
        var attempts = 0;
        var notifier = Substitute.For<INotifier>();
        notifier.PublishAsync(Arg.Any<TestWorkflowNotification>(), Arg.Any<PublishOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromResult<IResult>(Result.Failure().WithError(new Error("transient notify failure")))
                    : Task.FromResult<IResult>(Result.Success());
            });

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => notifier))
            .WithOrchestration<RetryingPublishNotificationWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<RetryingPublishNotificationWorkflowOrchestration, NotificationActivityWorkflowData>(new NotificationActivityWorkflowData
        {
            Payload = "notify",
        });
        await sut.Assert(dispatch.Value).BeWaitingAsync("RetryingNotification");

        await sut.AdvanceTimeAsync(TimeSpan.FromMinutes(1));

        var context = await sut.GetContextAsync<NotificationActivityWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        history.Count(item => item.EventType == "ActivityRetryScheduled").ShouldBe(1);
    }

    [Fact]
    public async Task DispatchAsync_WhenPublishMessageActivitySucceeds_PublishesMappedMessage()
    {
        var broker = Substitute.For<IMessageBroker>();
        TestWorkflowMessage capturedMessage = null;
        broker.Publish(Arg.Any<IMessage>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedMessage = call.ArgAt<IMessage>(0) as TestWorkflowMessage;
                return Task.CompletedTask;
            });

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => broker))
            .WithOrchestration<PublishMessageWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<PublishMessageWorkflowOrchestration, TransportActivityWorkflowData>(new TransportActivityWorkflowData
        {
            OrderId = "order-456",
            FlowId = "flow-123",
            Payload = "published",
        });
        var context = await sut.GetContextAsync<TransportActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        capturedMessage.ShouldNotBeNull();
        capturedMessage.Kind.ShouldBe("integration");
        capturedMessage.Payload.ShouldBe("published");
        capturedMessage.Properties[BridgingIT.DevKit.Application.Orchestrations.Constants.CorrelationIdKey].ShouldBe(context.CorrelationId);
        capturedMessage.Properties[BridgingIT.DevKit.Application.Orchestrations.Constants.FlowIdKey].ShouldBe("flow-123");
        capturedMessage.Properties["OrderId"].ShouldBe("order-456");
    }

    [Fact]
    public async Task DispatchAsync_WhenPublishMessageActivityBrokerIsMissing_FailsActivity()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .WithOrchestration<PublishMessageWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<PublishMessageWorkflowOrchestration, TransportActivityWorkflowData>(new TransportActivityWorkflowData
        {
            OrderId = "order-456",
            FlowId = "flow-123",
            Payload = "published",
        });
        var context = await sut.GetContextAsync<TransportActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("IMessageBroker is not registered");
    }

    [Fact]
    public async Task DispatchAsync_WhenPublishMessageActivityBrokerThrows_FailsActivity()
    {
        var broker = Substitute.For<IMessageBroker>();
        broker.Publish(Arg.Any<IMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("publish exploded")));

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => broker))
            .WithOrchestration<PublishMessageWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<PublishMessageWorkflowOrchestration, TransportActivityWorkflowData>(new TransportActivityWorkflowData
        {
            Payload = "published",
        });
        var context = await sut.GetContextAsync<TransportActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("publish exploded");
    }

    [Fact]
    public async Task AdvanceTimeAsync_WhenPublishMessageActivityUsesRetryPolicy_RetriesUntilBrokerSucceeds()
    {
        var attempts = 0;
        var broker = Substitute.For<IMessageBroker>();
        broker.Publish(Arg.Any<IMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromException(new InvalidOperationException("transient publish failure"))
                    : Task.CompletedTask;
            });

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => broker))
            .WithOrchestration<RetryingPublishMessageWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<RetryingPublishMessageWorkflowOrchestration, TransportActivityWorkflowData>(new TransportActivityWorkflowData
        {
            Payload = "published",
        });
        await sut.Assert(dispatch.Value).BeWaitingAsync("RetryingPublishMessage");

        await sut.AdvanceTimeAsync(TimeSpan.FromMinutes(1));

        var context = await sut.GetContextAsync<TransportActivityWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        history.Count(item => item.EventType == "ActivityRetryScheduled").ShouldBe(1);
    }

    [Fact]
    public async Task DispatchAsync_WhenSendQueueMessageActivitySucceeds_EnqueuesMappedMessage()
    {
        var broker = Substitute.For<IQueueBroker>();
        TestWorkflowQueueMessage capturedMessage = null;
        broker.Enqueue(Arg.Any<IQueueMessage>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedMessage = call.ArgAt<IQueueMessage>(0) as TestWorkflowQueueMessage;
                return Task.CompletedTask;
            });

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => broker))
            .WithOrchestration<SendQueueMessageWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<SendQueueMessageWorkflowOrchestration, TransportActivityWorkflowData>(new TransportActivityWorkflowData
        {
            OrderId = "order-654",
            FlowId = "flow-789",
            Payload = "queued",
        });
        var context = await sut.GetContextAsync<TransportActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        capturedMessage.ShouldNotBeNull();
        capturedMessage.Kind.ShouldBe("queue");
        capturedMessage.Payload.ShouldBe("queued");
        capturedMessage.Properties[BridgingIT.DevKit.Application.Orchestrations.Constants.CorrelationIdKey].ShouldBe(context.CorrelationId);
        capturedMessage.Properties[BridgingIT.DevKit.Application.Orchestrations.Constants.FlowIdKey].ShouldBe("flow-789");
        capturedMessage.Properties["OrderId"].ShouldBe("order-654");
        await broker.Received(1).Enqueue(Arg.Any<IQueueMessage>(), Arg.Any<CancellationToken>());
        await broker.DidNotReceive().EnqueueAndWait(Arg.Any<IQueueMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_WhenSendQueueMessageActivityBrokerIsMissing_FailsActivity()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .WithOrchestration<SendQueueMessageWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<SendQueueMessageWorkflowOrchestration, TransportActivityWorkflowData>(new TransportActivityWorkflowData
        {
            Payload = "queued",
        });
        var context = await sut.GetContextAsync<TransportActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("IQueueBroker is not registered");
    }

    [Fact]
    public async Task DispatchAsync_WhenSendQueueMessageActivityBrokerThrows_FailsActivity()
    {
        var broker = Substitute.For<IQueueBroker>();
        broker.Enqueue(Arg.Any<IQueueMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("queue exploded")));

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => broker))
            .WithOrchestration<SendQueueMessageWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<SendQueueMessageWorkflowOrchestration, TransportActivityWorkflowData>(new TransportActivityWorkflowData
        {
            Payload = "queued",
        });
        var context = await sut.GetContextAsync<TransportActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("queue exploded");
    }

    [Fact]
    public async Task AdvanceTimeAsync_WhenSendQueueMessageActivityUsesRetryPolicy_RetriesUntilBrokerSucceeds()
    {
        var attempts = 0;
        var broker = Substitute.For<IQueueBroker>();
        broker.Enqueue(Arg.Any<IQueueMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromException(new InvalidOperationException("transient queue failure"))
                    : Task.CompletedTask;
            });

        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services => services.AddScoped(_ => broker))
            .WithOrchestration<RetryingSendQueueMessageWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<RetryingSendQueueMessageWorkflowOrchestration, TransportActivityWorkflowData>(new TransportActivityWorkflowData
        {
            Payload = "queued",
        });
        await sut.Assert(dispatch.Value).BeWaitingAsync("RetryingQueueMessage");

        await sut.AdvanceTimeAsync(TimeSpan.FromMinutes(1));

        var context = await sut.GetContextAsync<TransportActivityWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        history.Count(item => item.EventType == "ActivityRetryScheduled").ShouldBe(1);
    }

    [Fact]
    public async Task DispatchAsync_WhenExecutePipelineActivitySucceeds_MapsContextInBothDirections()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddPipelines()
                    .WithPipeline<PipelineActivityContext>("orchestration-pipeline", builder => builder
                        .AddStep(execution =>
                        {
                            execution.Context.MetadataEcho = execution.Context.Pipeline.Items["OrderId"]?.ToString();
                            execution.Context.OptionsRetryAttempts = execution.Options.MaxRetryAttemptsPerStep;
                            execution.Context.Output = $"{execution.Context.Payload}:{execution.Context.MetadataEcho}";
                            execution.Context.Pipeline.Items["AfterValue"] = $"after-{execution.Context.Payload}";
                            return execution.Continue();
                        }, "PipelineStep"));
            })
            .WithOrchestration<ExecuteNamedPipelineWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ExecuteNamedPipelineWorkflowOrchestration, PipelineActivityWorkflowData>(new PipelineActivityWorkflowData
        {
            OrderId = "order-987",
            Payload = "payload",
        });
        var context = await sut.GetContextAsync<PipelineActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.Result.ShouldBe("payload:order-987");
        context.Data.MetadataEcho.ShouldBe("after-payload");
        context.Data.PipelineName.ShouldBe("orchestration-pipeline");
        context.Data.ExecutedSteps.ShouldBe(1);
        context.Data.ConfiguredRetryAttempts.ShouldBe(9);
        context.Data.BeforeMapped.ShouldBeTrue();
    }

    [Fact]
    public async Task DispatchAsync_WhenExecutePipelineActivityPipelineFactoryIsMissing_FailsActivity()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .WithOrchestration<ExecuteNamedPipelineWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ExecuteNamedPipelineWorkflowOrchestration, PipelineActivityWorkflowData>(new PipelineActivityWorkflowData
        {
            OrderId = "order-987",
            Payload = "payload",
        });
        var context = await sut.GetContextAsync<PipelineActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("IPipelineFactory is not registered");
    }

    [Fact]
    public async Task DispatchAsync_WhenExecutePipelineActivityPipelineReturnsFailedResult_FailsActivity()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddPipelines()
                    .WithPipeline<FailingPipelineWorkflow>();
            })
            .WithOrchestration<ExecuteFailingPipelineWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ExecuteFailingPipelineWorkflowOrchestration, PipelineActivityWorkflowData>(new PipelineActivityWorkflowData
        {
            Payload = "payload",
        });
        var context = await sut.GetContextAsync<PipelineActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("pipeline failed");
    }

    [Fact]
    public async Task DispatchAsync_WhenExecutePipelineActivityPipelineThrows_FailsActivity()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddPipelines()
                    .WithPipeline<ThrowingPipelineWorkflow>();
            })
            .WithOrchestration<ExecuteThrowingPipelineWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<ExecuteThrowingPipelineWorkflowOrchestration, PipelineActivityWorkflowData>(new PipelineActivityWorkflowData
        {
            Payload = "payload",
        });
        var context = await sut.GetContextAsync<PipelineActivityWorkflowData>(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Failed);
        context.FailureReason.ShouldContain("pipeline exploded");
    }

    [Fact]
    public async Task AdvanceTimeAsync_WhenExecutePipelineActivityUsesRetryPolicy_RetriesUntilPipelineSucceeds()
    {
        await using var sut = OrchestrationTestHarness.CreateBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<PipelineRetryTracker>();
                services.AddPipelines()
                    .WithPipeline<RetryingPipelineWorkflow>();
            })
            .WithOrchestration<RetryingExecutePipelineWorkflowOrchestration>()
            .Build();

        var dispatch = await sut.DispatchAsync<RetryingExecutePipelineWorkflowOrchestration, PipelineActivityWorkflowData>(new PipelineActivityWorkflowData
        {
            Payload = "payload",
        });
        await sut.Assert(dispatch.Value).BeWaitingAsync("RetryingPipeline");

        await sut.AdvanceTimeAsync(TimeSpan.FromMinutes(1));

        var context = await sut.GetContextAsync<PipelineActivityWorkflowData>(dispatch.Value);
        var history = await sut.GetHistoryAsync(dispatch.Value);

        dispatch.IsSuccess.ShouldBeTrue();
        context.Status.ShouldBe(OrchestrationStatus.Completed);
        context.Data.Result.ShouldBe("success-2");
        history.Count(item => item.EventType == "ActivityRetryScheduled").ShouldBe(1);
    }

    private sealed class RequestActivityWorkflowData : IOrchestrationData
    {
        public string OrderId { get; set; }

        public string Payload { get; set; }

        public string QueryResult { get; set; }

        public string CommandResult { get; set; }

        public string RequestResult { get; set; }
    }

    private sealed class NotificationActivityWorkflowData : IOrchestrationData
    {
        public string OrderId { get; set; }

        public string Payload { get; set; }
    }

    private sealed class TransportActivityWorkflowData : IOrchestrationData
    {
        public string OrderId { get; set; }

        public string FlowId { get; set; }

        public string Payload { get; set; }
    }

    private sealed class PipelineActivityWorkflowData : IOrchestrationData
    {
        public string OrderId { get; set; }

        public string Payload { get; set; }

        public string Result { get; set; }

        public string MetadataEcho { get; set; }

        public string PipelineName { get; set; }

        public int ExecutedSteps { get; set; }

        public int ConfiguredRetryAttempts { get; set; }

        public bool BeforeMapped { get; set; }
    }

    private sealed class PipelineActivityContext : PipelineContextBase
    {
        public string Payload { get; set; }

        public string Output { get; set; }

        public string MetadataEcho { get; set; }

        public int OptionsRetryAttempts { get; set; }

        public bool BeforeMapped { get; set; }
    }

    private sealed class TestQueryRequest(string payload) : RequestBase<string>
    {
        public string Payload { get; } = payload;
    }

    private sealed class TestCommandRequest(string payload) : RequestBase<string>
    {
        public string Payload { get; } = payload;
    }

    private sealed class TestSendRequest(string payload) : RequestBase<string>
    {
        public string Payload { get; } = payload;
    }

    private sealed class TestWorkflowNotification(string payload) : NotificationBase
    {
        public string Payload { get; } = payload;
    }

    private sealed class FailingPipelineWorkflow : PipelineDefinition<PipelineActivityContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<PipelineActivityContext> builder)
        {
            builder.AddStep(execution =>
                execution.Continue(Result.Failure().WithError(new Error("pipeline failed"))));
        }
    }

    private sealed class ThrowingPipelineWorkflow : PipelineDefinition<PipelineActivityContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<PipelineActivityContext> builder)
        {
            builder.AddStep(_ => throw new InvalidOperationException("pipeline exploded"));
        }
    }

    private sealed class RetryingPipelineWorkflow : PipelineDefinition<PipelineActivityContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<PipelineActivityContext> builder)
        {
            builder.AddStep(execution =>
            {
                var tracker = execution.Services.GetRequiredService<PipelineRetryTracker>();
                tracker.Attempts++;
                execution.Context.Output = $"success-{tracker.Attempts}";

                return tracker.Attempts == 1
                    ? execution.Continue(Result.Failure().WithError(new Error("transient pipeline failure")))
                    : execution.Continue();
            }, "RetryingStep");
        }
    }

    private sealed class PipelineRetryTracker
    {
        public int Attempts { get; set; }
    }

    private sealed class TestWorkflowMessage(string payload) : IMessage
    {
        public string Payload { get; } = payload;

        public string Kind { get; set; }

        public string MessageId { get; } = Guid.NewGuid().ToString("N");

        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        public FluentValidation.Results.ValidationResult Validate() => new();
    }

    private sealed class TestWorkflowQueueMessage(string payload) : IQueueMessage
    {
        public string Payload { get; } = payload;

        public string Kind { get; set; }

        public string MessageId { get; } = Guid.NewGuid().ToString("N");

        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        public FluentValidation.Results.ValidationResult Validate() => new();
    }

    private sealed class QueryRequestWorkflowOrchestration : Orchestration<RequestActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<RequestActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .QueryActivity<RequestActivityWorkflowData, TestQueryRequest, string>(activity => activity
                    .Request(context => new TestQueryRequest(context.Data.Payload))
                    .MapResult((context, result) => context.Data.QueryResult = result)
                    .CorrelationId(context => context.CorrelationId)
                    .ContextProperty("OrderId", context => context.Data.OrderId))
                .Complete());
        }
    }

    private sealed class CommandRequestWithoutMappingWorkflowOrchestration : Orchestration<RequestActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<RequestActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .CommandActivity<RequestActivityWorkflowData, TestCommandRequest, string>(activity => activity
                    .Request(context => new TestCommandRequest(context.Data.Payload)))
                .Complete());
        }
    }

    private sealed class CommandRequestWithMappingWorkflowOrchestration : Orchestration<RequestActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<RequestActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .CommandActivity<RequestActivityWorkflowData, TestCommandRequest, string>(activity => activity
                    .Request(context => new TestCommandRequest(context.Data.Payload))
                    .MapResult((context, result) => context.Data.CommandResult = result))
                .Complete());
        }
    }

    private sealed class SendRequestWorkflowOrchestration : Orchestration<RequestActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<RequestActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .SendRequestActivity<RequestActivityWorkflowData, TestSendRequest, string>(activity => activity
                    .Request(context => new TestSendRequest(context.Data.Payload))
                    .MapResult((context, result) => context.Data.RequestResult = result))
                .Complete());
        }
    }

    private sealed class PublishNotificationWorkflowOrchestration : Orchestration<NotificationActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<NotificationActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .PublishNotificationActivity<NotificationActivityWorkflowData, TestWorkflowNotification>(activity => activity
                    .Notification(context => new TestWorkflowNotification(context.Data.Payload))
                    .ExecutionMode(ExecutionMode.FireAndForget)
                    .CorrelationId(context => context.CorrelationId)
                    .ContextProperty("OrderId", context => context.Data.OrderId))
                .Complete());
        }
    }

    private sealed class RetryingQueryRequestWorkflowOrchestration : Orchestration<RequestActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<RequestActivityWorkflowData> builder)
        {
            builder.State("RetryingQuery", state => state
                .QueryActivity<RequestActivityWorkflowData, TestQueryRequest, string>(activity => activity
                    .Request(context => new TestQueryRequest(context.Data.Payload))
                    .MapResult((context, result) => context.Data.QueryResult = result)
                    .Retry(new OrchestrationRetryPolicy
                    {
                        MaxAttempts = 2,
                        Delay = TimeSpan.FromMinutes(1),
                        BackoffMode = OrchestrationRetryBackoffMode.FixedDelay,
                    }))
                .Complete());
        }
    }

    private sealed class RetryingPublishNotificationWorkflowOrchestration : Orchestration<NotificationActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<NotificationActivityWorkflowData> builder)
        {
            builder.State("RetryingNotification", state => state
                .PublishNotificationActivity<NotificationActivityWorkflowData, TestWorkflowNotification>(activity => activity
                    .Notification(context => new TestWorkflowNotification(context.Data.Payload))
                    .Retry(new OrchestrationRetryPolicy
                    {
                        MaxAttempts = 2,
                        Delay = TimeSpan.FromMinutes(1),
                        BackoffMode = OrchestrationRetryBackoffMode.FixedDelay,
                    }))
                .Complete());
        }
    }

    private sealed class PublishMessageWorkflowOrchestration : Orchestration<TransportActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<TransportActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .PublishMessageActivity<TransportActivityWorkflowData, TestWorkflowMessage>(activity => activity
                    .Message(context => new TestWorkflowMessage(context.Data.Payload))
                    .ConfigureMessage((context, message) => message.Kind = "integration")
                    .CorrelationId(context => context.CorrelationId)
                    .FlowId(context => context.Data.FlowId)
                    .Property("OrderId", context => context.Data.OrderId))
                .Complete());
        }
    }

    private sealed class RetryingPublishMessageWorkflowOrchestration : Orchestration<TransportActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<TransportActivityWorkflowData> builder)
        {
            builder.State("RetryingPublishMessage", state => state
                .PublishMessageActivity<TransportActivityWorkflowData, TestWorkflowMessage>(activity => activity
                    .Message(context => new TestWorkflowMessage(context.Data.Payload))
                    .Retry(new OrchestrationRetryPolicy
                    {
                        MaxAttempts = 2,
                        Delay = TimeSpan.FromMinutes(1),
                        BackoffMode = OrchestrationRetryBackoffMode.FixedDelay,
                    }))
                .Complete());
        }
    }

    private sealed class SendQueueMessageWorkflowOrchestration : Orchestration<TransportActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<TransportActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .SendQueueMessageActivity<TransportActivityWorkflowData, TestWorkflowQueueMessage>(activity => activity
                    .Message(context => new TestWorkflowQueueMessage(context.Data.Payload))
                    .ConfigureMessage((context, message) => message.Kind = "queue")
                    .CorrelationId(context => context.CorrelationId)
                    .FlowId(context => context.Data.FlowId)
                    .Property("OrderId", context => context.Data.OrderId))
                .Complete());
        }
    }

    private sealed class RetryingSendQueueMessageWorkflowOrchestration : Orchestration<TransportActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<TransportActivityWorkflowData> builder)
        {
            builder.State("RetryingQueueMessage", state => state
                .SendQueueMessageActivity<TransportActivityWorkflowData, TestWorkflowQueueMessage>(activity => activity
                    .Message(context => new TestWorkflowQueueMessage(context.Data.Payload))
                    .Retry(new OrchestrationRetryPolicy
                    {
                        MaxAttempts = 2,
                        Delay = TimeSpan.FromMinutes(1),
                        BackoffMode = OrchestrationRetryBackoffMode.FixedDelay,
                    }))
                .Complete());
        }
    }

    private sealed class ExecuteNamedPipelineWorkflowOrchestration : Orchestration<PipelineActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<PipelineActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .ExecutePipelineActivity<PipelineActivityWorkflowData, PipelineActivityContext>(activity => activity
                    .Pipeline("orchestration-pipeline")
                    .Context(context => new PipelineActivityContext
                    {
                        Payload = context.Data.Payload,
                    })
                    .MapToContext((context, pipelineContext) => pipelineContext.BeforeMapped = true)
                    .MapFromContext((context, pipelineContext) =>
                    {
                        context.Data.Result = pipelineContext.Output;
                        context.Data.MetadataEcho = pipelineContext.Pipeline.Items["AfterValue"]?.ToString();
                        context.Data.PipelineName = pipelineContext.Pipeline.Name;
                        context.Data.ExecutedSteps = pipelineContext.Pipeline.ExecutedStepCount;
                        context.Data.ConfiguredRetryAttempts = pipelineContext.OptionsRetryAttempts;
                        context.Data.BeforeMapped = pipelineContext.BeforeMapped;
                    })
                    .ConfigureOptions((context, options) => options.MaxRetryAttemptsPerStep = 9)
                    .Item("OrderId", context => context.Data.OrderId))
                .Complete());
        }
    }

    private sealed class ExecuteFailingPipelineWorkflowOrchestration : Orchestration<PipelineActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<PipelineActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .ExecutePipelineActivity<PipelineActivityWorkflowData, FailingPipelineWorkflow, PipelineActivityContext>(activity => activity
                    .Context(context => new PipelineActivityContext
                    {
                        Payload = context.Data.Payload,
                    }))
                .Complete());
        }
    }

    private sealed class ExecuteThrowingPipelineWorkflowOrchestration : Orchestration<PipelineActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<PipelineActivityWorkflowData> builder)
        {
            builder.State("Created", state => state
                .ExecutePipelineActivity<PipelineActivityWorkflowData, ThrowingPipelineWorkflow, PipelineActivityContext>(activity => activity
                    .Context(context => new PipelineActivityContext
                    {
                        Payload = context.Data.Payload,
                    }))
                .Complete());
        }
    }

    private sealed class RetryingExecutePipelineWorkflowOrchestration : Orchestration<PipelineActivityWorkflowData>
    {
        protected override void Define(IOrchestrationBuilder<PipelineActivityWorkflowData> builder)
        {
            builder.State("RetryingPipeline", state => state
                .ExecutePipelineActivity<PipelineActivityWorkflowData, RetryingPipelineWorkflow, PipelineActivityContext>(activity => activity
                    .Context(context => new PipelineActivityContext
                    {
                        Payload = context.Data.Payload,
                    })
                    .MapFromContext((context, pipelineContext) => context.Data.Result = pipelineContext.Output)
                    .Retry(new OrchestrationRetryPolicy
                    {
                        MaxAttempts = 2,
                        Delay = TimeSpan.FromMinutes(1),
                        BackoffMode = OrchestrationRetryBackoffMode.FixedDelay,
                    }))
                .Complete());
        }
    }
}
