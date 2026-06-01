// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.DependencyInjection;

public class JobSchedulerOptionalIntegrationTests(ITestOutputHelper output) : JobSchedulerTestBase(output)
{
    [Fact]
    public async Task MessagingPublishJob_AliasPublishesMappedMessage()
    {
        var broker = new RecordingMessageBroker();

        using var harness = this.CreateHarness(
            jobs => jobs.WithMessagingPublishJob<OptionalIntegrationData, OptionalIntegrationMessage>("publish-message-alias", job => job
                .WithDescription("Publishes a broker message through alias.")
                .WithMessage(context => new OptionalIntegrationMessage { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IMessageBroker>(broker));

        var result = await harness.DispatchAndWaitAsync<MessagePublishJob<OptionalIntegrationData, OptionalIntegrationMessage>>(
            new OptionalIntegrationData { Payload = "msg-alias" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        broker.Messages.Count.ShouldBe(1);
        broker.Messages[0].Payload.ShouldBe("msg-alias");
    }

    [Fact]
    public async Task QueueingSendJob_AliasSendsMappedQueueMessage()
    {
        var broker = new RecordingQueueBroker();

        using var harness = this.CreateHarness(
            jobs => jobs.WithQueueingSendJob<OptionalIntegrationData, OptionalQueueMessage>("send-queue-message-alias", job => job
                .WithDescription("Sends a queue message through alias.")
                .WithMessage(context => new OptionalQueueMessage { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IQueueBroker>(broker));

        var result = await harness.DispatchAndWaitAsync<QueueSendJob<OptionalIntegrationData, OptionalQueueMessage>>(
            new OptionalIntegrationData { Payload = "queue-alias" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        broker.EnqueueCalls.Count.ShouldBe(1);
        broker.EnqueueCalls[0].Payload.ShouldBe("queue-alias");
    }

    [Fact]
    public async Task MessagingPublishJob_AliasWithoutRegisteredBroker_FailsClearly()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithMessagingPublishJob<OptionalIntegrationData, OptionalIntegrationMessage>("missing-message-alias", job => job
                .WithDescription("missing message broker alias")
                .WithMessage(context => new OptionalIntegrationMessage { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())));

        var result = await harness.DispatchAndWaitAsync<MessagePublishJob<OptionalIntegrationData, OptionalIntegrationMessage>>(
            new OptionalIntegrationData { Payload = "missing" });
        var execution = (await harness.GetExecutionsAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        execution.Message.ShouldContain("IMessageBroker is not registered");
    }

    [Fact]
    public async Task QueueingSendJob_AliasWithoutRegisteredBroker_FailsClearly()
    {
        using var harness = this.CreateHarness(
            jobs => jobs.WithQueueingSendJob<OptionalIntegrationData, OptionalQueueMessage>("missing-queue-alias", job => job
                .WithDescription("missing queue broker alias")
                .WithMessage(context => new OptionalQueueMessage { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())));

        var result = await harness.DispatchAndWaitAsync<QueueSendJob<OptionalIntegrationData, OptionalQueueMessage>>(
            new OptionalIntegrationData { Payload = "missing" });
        var execution = (await harness.GetExecutionsAsync(result.Value.OccurrenceId)).Single();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Failed);
        execution.Message.ShouldContain("IQueueBroker is not registered");
    }

    [Fact]
    public async Task MessagePublishJob_PublishesMappedMessage_AndMapsExecutionMetadata()
    {
        var broker = new RecordingMessageBroker();

        using var harness = this.CreateHarness(
            jobs => jobs.WithMessagePublishJob<OptionalIntegrationData, OptionalIntegrationMessage>("publish-message", job => job
                .WithDescription("Publishes a broker message.")
                .WithMessage(context => new OptionalIntegrationMessage { Payload = context.Data.Payload })
                .MapCorrelationId()
                .MapContextProperty("tenant")
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IMessageBroker>(broker));

        var result = await harness.DispatchAndWaitAsync<MessagePublishJob<OptionalIntegrationData, OptionalIntegrationMessage>>(
            new OptionalIntegrationData { Payload = "msg-42" },
            new JobDispatchOptions
            {
                CorrelationId = "corr-msg",
                Properties = new PropertyBag { ["tenant"] = "alpha" },
            });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        broker.Messages.Count.ShouldBe(1);
        broker.Messages[0].Payload.ShouldBe("msg-42");
        broker.Messages[0].Properties["CorrelationId"].ShouldBe("corr-msg");
        broker.Messages[0].Properties["tenant"].ShouldBe("alpha");
    }

    [Fact]
    public async Task QueueSendJob_WaitForPersistence_UsesConfirmedEnqueuePath()
    {
        var broker = new RecordingQueueBroker();

        using var harness = this.CreateHarness(
            jobs => jobs.WithQueueSendJob<OptionalIntegrationData, OptionalQueueMessage>("send-queue-message", job => job
                .WithDescription("Sends one queue message.")
                .WithMessage(context => new OptionalQueueMessage { Payload = context.Data.Payload })
                .MapCorrelationId()
                .MapContextProperty("tenant")
                .WaitForPersistence()
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IQueueBroker>(broker));

        var result = await harness.DispatchAndWaitAsync<QueueSendJob<OptionalIntegrationData, OptionalQueueMessage>>(
            new OptionalIntegrationData { Payload = "queue-42" },
            new JobDispatchOptions
            {
                CorrelationId = "corr-queue",
                Properties = new PropertyBag { ["tenant"] = "beta" },
            });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        broker.EnqueueCalls.Count.ShouldBe(0);
        broker.EnqueueAndWaitCalls.Count.ShouldBe(1);
        broker.EnqueueAndWaitCalls[0].Payload.ShouldBe("queue-42");
        broker.EnqueueAndWaitCalls[0].Properties["CorrelationId"].ShouldBe("corr-queue");
        broker.EnqueueAndWaitCalls[0].Properties["tenant"].ShouldBe("beta");
    }

    [Fact]
    public async Task PipelineExecuteJob_ExecutesMappedPipelineContext()
    {
        var factory = new RecordingPipelineFactory();

        using var harness = this.CreateHarness(
            jobs => jobs.WithPipelineExecuteJob<OptionalIntegrationData, OptionalPipelineDefinition, OptionalPipelineContext>("execute-pipeline", job => job
                .WithDescription("Executes a packaged pipeline.")
                .WithContext(context => new OptionalPipelineContext
                {
                    Payload = context.Data.Payload,
                    CorrelationId = context.CorrelationId,
                })
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IPipelineFactory>(factory));

        var result = await harness.DispatchAndWaitAsync<PipelineExecuteJob<OptionalIntegrationData, OptionalPipelineDefinition, OptionalPipelineContext>>(
            new OptionalIntegrationData { Payload = "pipe-42" },
            new JobDispatchOptions { CorrelationId = "corr-pipe" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        factory.LastContext.ShouldNotBeNull();
        factory.LastContext.Payload.ShouldBe("pipe-42");
        factory.LastContext.CorrelationId.ShouldBe("corr-pipe");
    }

    [Fact]
    public async Task OrchestrationExecuteJob_DefaultMode_UsesInlineExecutionCall()
    {
        var service = new RecordingOrchestrationService();

        using var harness = this.CreateHarness(
            jobs => jobs.WithOrchestrationExecuteJob<OptionalIntegrationData, OptionalOrchestration, OptionalOrchestrationData>("execute-orchestration", job => job
                .WithDescription("Executes an orchestration inline.")
                .WithInput(context => new OptionalOrchestrationData { Payload = context.Data.Payload })
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IOrchestrationService>(service));

        var result = await harness.DispatchAndWaitAsync<OrchestrationExecuteJob<OptionalIntegrationData, OptionalOrchestration, OptionalOrchestrationData>>(
            new OptionalIntegrationData { Payload = "orch-inline" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        service.ExecuteCalls.ShouldBe(1);
        service.DispatchCalls.ShouldBe(0);
        service.LastData.ShouldBeOfType<OptionalOrchestrationData>().Payload.ShouldBe("orch-inline");
    }

    [Fact]
    public async Task OrchestrationExecuteJob_DispatchMode_UsesDispatchCall()
    {
        var service = new RecordingOrchestrationService();

        using var harness = this.CreateHarness(
            jobs => jobs.WithOrchestrationExecuteJob<OptionalIntegrationData, OptionalOrchestration, OptionalOrchestrationData>("dispatch-orchestration", job => job
                .WithDescription("Dispatches an orchestration.")
                .WithInput(context => new OptionalOrchestrationData { Payload = context.Data.Payload })
                .Dispatch()
                .AddTrigger("manual", trigger => trigger.Manual())),
            services => services.AddSingleton<IOrchestrationService>(service));

        var result = await harness.DispatchAndWaitAsync<OrchestrationExecuteJob<OptionalIntegrationData, OptionalOrchestration, OptionalOrchestrationData>>(
            new OptionalIntegrationData { Payload = "orch-dispatch" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        service.DispatchCalls.ShouldBe(1);
        service.ExecuteCalls.ShouldBe(0);
        service.LastData.ShouldBeOfType<OptionalOrchestrationData>().Payload.ShouldBe("orch-dispatch");
    }

    private sealed class OptionalIntegrationData
    {
        public string Payload { get; set; }
    }

    private sealed class OptionalIntegrationMessage : MessageBase
    {
        public string Payload { get; set; }
    }

    private sealed class OptionalQueueMessage : QueueMessageBase
    {
        public string Payload { get; set; }
    }

    private sealed class OptionalPipelineContext : PipelineContextBase
    {
        public string Payload { get; init; }

        public string CorrelationId { get; init; }
    }

    private sealed class OptionalPipelineDefinition : PipelineDefinition<OptionalPipelineContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<OptionalPipelineContext> builder)
        {
        }
    }

    private sealed class OptionalOrchestrationData : IOrchestrationData
    {
        public string Payload { get; set; }
    }

    private sealed class OptionalOrchestration : Orchestration<OptionalOrchestrationData>
    {
        protected override void Define(IOrchestrationBuilder<OptionalOrchestrationData> builder)
            => builder.State("done", state => state.Complete());
    }

    private sealed class RecordingMessageBroker : IMessageBroker
    {
        public List<OptionalIntegrationMessage> Messages { get; } = [];

        public Task Publish(IMessage message, CancellationToken cancellationToken = default)
        {
            this.Messages.Add((OptionalIntegrationMessage)message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingQueueBroker : IQueueBroker
    {
        public List<OptionalQueueMessage> EnqueueCalls { get; } = [];

        public List<OptionalQueueMessage> EnqueueAndWaitCalls { get; } = [];

        public Task Enqueue(IQueueMessage message, CancellationToken cancellationToken = default)
        {
            this.EnqueueCalls.Add((OptionalQueueMessage)message);
            return Task.CompletedTask;
        }

        public Task EnqueueAndWait(IQueueMessage message, CancellationToken cancellationToken = default)
        {
            this.EnqueueAndWaitCalls.Add((OptionalQueueMessage)message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPipelineFactory : IPipelineFactory
    {
        public OptionalPipelineContext LastContext { get; private set; }

        public IPipeline Create(string name) => throw new NotSupportedException();

        public IPipeline Create<TPipelineDefinition>() where TPipelineDefinition : class, IPipelineDefinitionSource
            => throw new NotSupportedException();

        public IPipeline<TContext> Create<TContext>(string name) where TContext : PipelineContextBase
            => throw new NotSupportedException();

        public IPipeline<TContext> Create<TPipelineDefinition, TContext>()
            where TPipelineDefinition : class, IPipelineDefinitionSource<TContext>
            where TContext : PipelineContextBase
            => (IPipeline<TContext>)(object)new RecordingPipeline(context => this.LastContext = (OptionalPipelineContext)(object)context);
    }

    private sealed class RecordingPipeline : IPipeline<OptionalPipelineContext>
    {
        private readonly Action<OptionalPipelineContext> onExecute;

        public RecordingPipeline(Action<OptionalPipelineContext> onExecute)
        {
            this.onExecute = onExecute;
        }

        public Task<Result> ExecuteAsync(PipelineExecutionOptions options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> ExecuteAsync(Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> ExecuteAsync(PipelineContextBase context, PipelineExecutionOptions options = null, CancellationToken cancellationToken = default)
            => this.ExecuteAsync((OptionalPipelineContext)context, options, cancellationToken);

        public Task<Result> ExecuteAsync(PipelineContextBase context, Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default)
            => this.ExecuteAsync((OptionalPipelineContext)context, new PipelineExecutionOptions(), cancellationToken);

        public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(PipelineExecutionOptions options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(PipelineContextBase context, PipelineExecutionOptions options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(PipelineContextBase context, Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> ExecuteAsync(OptionalPipelineContext context, PipelineExecutionOptions options = null, CancellationToken cancellationToken = default)
        {
            this.onExecute(context);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> ExecuteAsync(OptionalPipelineContext context, Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default)
            => this.ExecuteAsync(context, new PipelineExecutionOptions(), cancellationToken);

        public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(OptionalPipelineContext context, PipelineExecutionOptions options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(OptionalPipelineContext context, Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingOrchestrationService : IOrchestrationService
    {
        public int ExecuteCalls { get; private set; }

        public int DispatchCalls { get; private set; }

        public object LastData { get; private set; }

        public Task<Result<OrchestrationExecuteResult>> ExecuteAsync<TOrchestration, TData>(TData data, CancellationToken cancellationToken = default)
            where TOrchestration : class, IOrchestration<TData>
            where TData : class, IOrchestrationData
        {
            this.ExecuteCalls++;
            this.LastData = data;
            return Task.FromResult(Result<OrchestrationExecuteResult>.Success(new OrchestrationExecuteResult
            {
                InstanceId = Guid.NewGuid(),
                Status = "Completed",
                CurrentState = "done",
                Outcome = "Complete",
            }));
        }

        public Task<Result<Guid>> DispatchAsync<TOrchestration, TData>(TData data, CancellationToken cancellationToken = default)
            where TOrchestration : class, IOrchestration<TData>
            where TData : class, IOrchestrationData
        {
            this.DispatchCalls++;
            this.LastData = data;
            return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        }

        public Task<Result<OrchestrationWaitResult>> DispatchAndWaitAsync<TOrchestration, TData>(TData data, OrchestrationWaitFor waitFor = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            where TOrchestration : class, IOrchestration<TData>
            where TData : class, IOrchestrationData
            => throw new NotSupportedException();

        public Task<Result> SignalAsync(Guid instanceId, string signalName, object payload = null, string idempotencyKey = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> PauseAsync(Guid instanceId, string reason = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> ResumeAsync(Guid instanceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> CancelAsync(Guid instanceId, string reason = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> TerminateAsync(Guid instanceId, string reason = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
