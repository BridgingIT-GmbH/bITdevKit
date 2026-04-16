namespace BridgingIT.DevKit.Application.UnitTests.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;

[UnitTest("Application")]
public class MessageBrokerBaseTests
{
    [Fact]
    public async Task Publish_WhenMessageValidationFails_ThrowsValidationException()
    {
        // Arrange
        var sut = new TestMessageBroker(new StaticHandlerFactory(new TestMessageHandler(new List<string>())));

        // Act
        var act = () => sut.Publish(new InvalidMessage());

        // Assert
        await act.ShouldThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Publish_WhenPublisherBehaviorExists_InvokesBehaviorAndBrokerInOrder()
    {
        // Arrange
        var trace = new List<string>();
        var sut = new TestMessageBroker(
            new StaticHandlerFactory(new TestMessageHandler(trace)),
            publisherBehaviors: [new RecordingPublisherBehavior(trace)]);
        var message = new TestMessage("msg-1", trace);

        // Act
        await sut.Publish(message);

        // Assert
        trace.ShouldBe(["publish-before", "broker-publish", "publish-after"]);
    }

    [Fact]
    public async Task Process_WhenNoSubscriptionExists_CompletesAsSucceededWithoutProcessing()
    {
        // Arrange
        var sut = new TestMessageBroker(new StaticHandlerFactory(new TestMessageHandler(new List<string>())));
        bool? result = null;

        // Act
        await sut.Process(new MessageRequest(new TestMessage("msg-1"), value => result = value, CancellationToken.None));

        // Assert
        result.ShouldBe(true);
        sut.OnProcessCount.ShouldBe(0);
    }

    [Fact]
    public async Task Process_WhenSubscriptionExists_InvokesHandlerPipelineAndCompletesSucceeded()
    {
        // Arrange
        var trace = new List<string>();
        var handler = new TestMessageHandler(trace);
        var sut = new TestMessageBroker(
            new StaticHandlerFactory(handler),
            handlerBehaviors: [new RecordingHandlerBehavior(trace)]);
        bool? result = null;
        var message = new TestMessage("msg-1", trace);
        await sut.Subscribe<TestMessage, TestMessageHandler>();

        // Act
        await sut.Process(new MessageRequest(message, value => result = value, CancellationToken.None));

        // Assert
        result.ShouldBe(true);
        sut.OnProcessCount.ShouldBe(1);
        trace.ShouldBe(["broker-process", "handler-before", "handler", "handler-after"]);
    }

    [Fact]
    public async Task Process_WhenSubscriptionExists_DisposesHandlerResultAfterPipelineCompletes()
    {
        // Arrange
        var trace = new List<string>();
        var lifetime = new HandlerLifetime();
        var sut = new TestMessageBroker(
            new LifetimeTrackingHandlerFactory(trace, lifetime),
            handlerBehaviors: [new LifetimeAssertingHandlerBehavior(trace, lifetime)]);
        bool? result = null;
        await sut.Subscribe<TestMessage, LifetimeTrackingMessageHandler>();

        // Act
        await sut.Process(new MessageRequest(new TestMessage("msg-1", trace), value => result = value, CancellationToken.None));

        // Assert
        result.ShouldBe(true);
        lifetime.IsDisposed.ShouldBeTrue();
        trace.ShouldBe(["broker-process", "handler-before", "handler", "handler-after", "handler-disposed"]);
    }

    private sealed class TestMessageBroker(
        IMessageHandlerFactory handlerFactory,
        IEnumerable<IMessagePublisherBehavior> publisherBehaviors = null,
        IEnumerable<IMessageHandlerBehavior> handlerBehaviors = null)
        : MessageBrokerBase(NullLoggerFactory.Instance, handlerFactory, publisherBehaviors: publisherBehaviors, handlerBehaviors: handlerBehaviors)
    {
        public int OnProcessCount { get; private set; }

        protected override Task OnPublish(IMessage message, CancellationToken cancellationToken)
        {
            if (message is TestMessage testMessage)
            {
                testMessage.Trace.Add("broker-publish");
            }

            return Task.CompletedTask;
        }

        protected override Task OnProcess(IMessage message, CancellationToken cancellationToken)
        {
            this.OnProcessCount++;

            if (message is TestMessage testMessage)
            {
                testMessage.Trace.Add("broker-process");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StaticHandlerFactory(object handler) : IMessageHandlerFactory
    {
        public MessageHandlerFactoryResult Create(Type messageHandlerType)
        {
            return MessageHandlerFactoryResult.Create(handler);
        }
    }

    private sealed class LifetimeTrackingHandlerFactory(List<string> trace, HandlerLifetime lifetime) : IMessageHandlerFactory
    {
        public MessageHandlerFactoryResult Create(Type messageHandlerType)
        {
            return new MessageHandlerFactoryResult(
                new LifetimeTrackingMessageHandler(trace, lifetime),
                () =>
                {
                    lifetime.IsDisposed = true;
                    trace.Add("handler-disposed");
                    return ValueTask.CompletedTask;
                });
        }
    }

    private sealed class RecordingPublisherBehavior(List<string> trace) : IMessagePublisherBehavior
    {
        public async Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken, MessagePublisherDelegate next)
            where TMessage : IMessage
        {
            trace.Add("publish-before");
            await next();
            trace.Add("publish-after");
        }
    }

    private sealed class RecordingHandlerBehavior(List<string> trace) : IMessageHandlerBehavior
    {
        public async Task Handle<TMessage>(TMessage message, CancellationToken cancellationToken, object handler, MessageHandlerDelegate next)
            where TMessage : IMessage
        {
            trace.Add("handler-before");
            await next();
            trace.Add("handler-after");
        }
    }

    private sealed class LifetimeAssertingHandlerBehavior(List<string> trace, HandlerLifetime lifetime) : IMessageHandlerBehavior
    {
        public async Task Handle<TMessage>(TMessage message, CancellationToken cancellationToken, object handler, MessageHandlerDelegate next)
            where TMessage : IMessage
        {
            lifetime.IsDisposed.ShouldBeFalse();
            trace.Add("handler-before");
            await next();
            lifetime.IsDisposed.ShouldBeFalse();
            trace.Add("handler-after");
        }
    }

    private sealed class TestMessage(string value, List<string> trace = null) : MessageBase
    {
        public string Value { get; } = value;

        public List<string> Trace { get; } = trace ?? [];
    }

    private sealed class InvalidMessage : MessageBase
    {
        public override ValidationResult Validate()
        {
            return new ValidationResult([new ValidationFailure(nameof(InvalidMessage), "invalid")]);
        }
    }

    private sealed class TestMessageHandler(List<string> trace) : IMessageHandler<TestMessage>
    {
        public Task Handle(TestMessage message, CancellationToken cancellationToken)
        {
            trace.Add("handler");
            return Task.CompletedTask;
        }
    }

    private sealed class LifetimeTrackingMessageHandler(List<string> trace, HandlerLifetime lifetime) : IMessageHandler<TestMessage>
    {
        public Task Handle(TestMessage message, CancellationToken cancellationToken)
        {
            lifetime.IsDisposed.ShouldBeFalse();
            trace.Add("handler");
            return Task.CompletedTask;
        }
    }

    private sealed class HandlerLifetime
    {
        public bool IsDisposed { get; set; }
    }
}