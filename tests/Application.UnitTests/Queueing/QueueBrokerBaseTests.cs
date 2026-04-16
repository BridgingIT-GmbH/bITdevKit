// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;

public class QueueBrokerBaseTests
{
    [Fact]
    public async Task Enqueue_WhenMessageValidationFails_ThrowsValidationException()
    {
        // Arrange
        var sut = new TestQueueBroker(new StaticHandlerFactory(new TestQueueMessageHandler(new List<string>())));

        // Act
        var act = () => sut.Enqueue(new InvalidQueueMessage());

        // Assert
        await act.ShouldThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Enqueue_WhenEnqueuerBehaviorExists_InvokesBehaviorAndBrokerInOrder()
    {
        // Arrange
        var trace = new List<string>();
        var sut = new TestQueueBroker(
            new StaticHandlerFactory(new TestQueueMessageHandler(trace)),
            enqueuerBehaviors: [new RecordingEnqueuerBehavior(trace)]);
        var message = new TestQueueMessage("msg-1", trace);

        // Act
        await sut.Enqueue(message);

        // Assert
        trace.ShouldBe(["enqueue-before", "broker-enqueue", "enqueue-after"]);
    }

    [Fact]
    public async Task Process_WhenNoSubscriptionExists_CompletesAsWaitingForHandler()
    {
        // Arrange
        var sut = new TestQueueBroker(new StaticHandlerFactory(new TestQueueMessageHandler(new List<string>())));
        QueueProcessingResult? result = null;

        // Act
        await sut.Process(new QueueMessageRequest(new TestQueueMessage("msg-1"), value => result = value, CancellationToken.None));

        // Assert
        result.ShouldBe(QueueProcessingResult.WaitingForHandler);
        sut.OnProcessCount.ShouldBe(0);
    }

    [Fact]
    public async Task Process_WhenSubscriptionExists_InvokesHandlerPipelineAndCompletesSucceeded()
    {
        // Arrange
        var trace = new List<string>();
        var handler = new TestQueueMessageHandler(trace);
        var sut = new TestQueueBroker(
            new StaticHandlerFactory(handler),
            handlerBehaviors: [new RecordingHandlerBehavior(trace)]);
        QueueProcessingResult? result = null;
        var message = new TestQueueMessage("msg-1", trace);
        await sut.Subscribe<TestQueueMessage, TestQueueMessageHandler>();

        // Act
        await sut.Process(new QueueMessageRequest(message, value => result = value, CancellationToken.None));

        // Assert
        result.ShouldBe(QueueProcessingResult.Succeeded);
        sut.OnProcessCount.ShouldBe(1);
        trace.ShouldBe(["broker-process", "handler-before", "handler", "handler-after"]);
    }

    private sealed class TestQueueBroker(
        IQueueMessageHandlerFactory handlerFactory,
        IEnumerable<IQueueEnqueuerBehavior> enqueuerBehaviors = null,
        IEnumerable<IQueueHandlerBehavior> handlerBehaviors = null)
        : QueueBrokerBase(NullLoggerFactory.Instance, handlerFactory, enqueuerBehaviors: enqueuerBehaviors, handlerBehaviors: handlerBehaviors)
    {
        public int OnProcessCount { get; private set; }

        protected override Task OnEnqueue(IQueueMessage message, CancellationToken cancellationToken)
        {
            if (message is TestQueueMessage testMessage)
            {
                testMessage.Trace.Add("broker-enqueue");
            }

            return Task.CompletedTask;
        }

        protected override Task OnProcess(IQueueMessage message, CancellationToken cancellationToken)
        {
            this.OnProcessCount++;

            if (message is TestQueueMessage testMessage)
            {
                testMessage.Trace.Add("broker-process");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StaticHandlerFactory(object handler) : IQueueMessageHandlerFactory
    {
        public object Create(Type messageHandlerType)
        {
            return handler;
        }
    }

    private sealed class RecordingEnqueuerBehavior(List<string> trace) : IQueueEnqueuerBehavior
    {
        public async Task Enqueue(IQueueMessage message, CancellationToken cancellationToken, QueueEnqueuerDelegate next)
        {
            trace.Add("enqueue-before");
            await next();
            trace.Add("enqueue-after");
        }
    }

    private sealed class RecordingHandlerBehavior(List<string> trace) : IQueueHandlerBehavior
    {
        public async Task Handle(IQueueMessage message, CancellationToken cancellationToken, object handler, QueueHandlerDelegate next)
        {
            trace.Add("handler-before");
            await next();
            trace.Add("handler-after");
        }
    }

    private sealed class TestQueueMessage(string value, List<string> trace = null) : QueueMessageBase
    {
        public string Value { get; } = value;

        public List<string> Trace { get; } = trace ?? [];
    }

    private sealed class InvalidQueueMessage : QueueMessageBase
    {
        public override ValidationResult Validate()
        {
            return new ValidationResult([new ValidationFailure(nameof(InvalidQueueMessage), "invalid")]);
        }
    }

    private sealed class TestQueueMessageHandler(List<string> trace) : IQueueMessageHandler<TestQueueMessage>
    {
        public Task Handle(TestQueueMessage message, CancellationToken cancellationToken)
        {
            trace.Add("handler");
            return Task.CompletedTask;
        }
    }
}