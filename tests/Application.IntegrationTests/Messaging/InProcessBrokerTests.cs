// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Application")]
//[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class InProcessBrokerTests(ITestOutputHelper output) : TestsBase(output, s =>
        {
            s.AddMessaging()
                .WithSubscription<EchoMessage, EchoMessageHandler>() // TODO: needed?
                .WithBehavior<RetryMessageHandlerBehavior>()
                .WithBehavior<TimeoutMessageHandlerBehavior>()
                .WithInProcessBroker(new InProcessMessageBrokerConfiguration
                {
                    ProcessDelay = 0,
                    MessageExpiration = new TimeSpan(0, 1, 0)
                });
        })
{
    [Fact]
    public async Task Publish_SingleMessage_MessageHandledBySingleHandler()
    {
        // Arrange
        var message = new StubMessage("John", DateTime.UtcNow.Ticks);
        var messageBroker = this.ServiceProvider.GetService<IMessageBroker>();
        await messageBroker.Subscribe<StubMessage, StubMessageHandler>();
        StubMessageHandler.Processed = false;

        // Act
        await messageBroker.Publish(message, CancellationToken.None);
        //await Task.Delay(1000);

        // Assert
        StubMessageHandler.Processed.ShouldBeTrue();
        StubMessageHandler.Result.ShouldBe($"{message.FirstName} {message.Ticks}");
    }

    [Fact]
    public async Task Publish_ExpiredMessage_MessageNotHandledByHandler()
    {
        // Arrange
        var message = new StubMessage("John", DateTime.UtcNow.Ticks);
        ReflectionHelper.SetProperty(message, nameof(StubMessage.Timestamp), DateTime.UtcNow.AddMinutes(-5));

        var messageBroker = this.ServiceProvider.GetService<IMessageBroker>();
        await messageBroker.Subscribe<StubMessage, StubMessageHandler>();
        StubMessageHandler.Processed = false;
        // Act
        await messageBroker.Publish(message, CancellationToken.None);

        // Assert
        StubMessageHandler.Processed.ShouldBeFalse();
    }

    [Fact]
    public async Task Publish_SingleMessage_MessageHandledByAllHandlers()
    {
        // Arrange
        var message = new StubMessage("John", DateTime.UtcNow.Ticks);
        var messageBroker = this.ServiceProvider.GetService<IMessageBroker>();
        await messageBroker.Subscribe<StubMessage, StubMessageHandler>();
        await messageBroker.Subscribe<StubMessage, AnotherStubMessageHandler>();
        StubMessageHandler.Processed = false;
        AnotherStubMessageHandler.Processed = false;

        // Act
        await messageBroker.Publish(message, CancellationToken.None);
        //await Task.Delay(1000);

        // Assert
        StubMessageHandler.Processed.ShouldBeTrue();
        StubMessageHandler.Result.ShouldBe($"{message.FirstName} {message.Ticks}");
        AnotherStubMessageHandler.Processed.ShouldBeTrue();
        AnotherStubMessageHandler.Result.ShouldBe($"{message.FirstName} {message.Ticks}");
    }

    // TODO: check if behaviors are called
    // TODO: check when no handler (no subsription)
}