// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

//namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure;

//using BridgingIT.DevKit.Application.Messaging;
//using BridgingIT.DevKit.Infrastructure.Azure;
//using Microsoft.Extensions.DependencyInjection;

//[IntegrationTest("Infrastructure")]
//[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
//public class ServiceBusMessageBrokerTests
//{
//    private static readonly MessageState MessageState = new();
//    private readonly TestEnvironmentFixture fixture;

//    public ServiceBusMessageBrokerTests(TestEnvironmentFixture fixture)
//    {
//        this.fixture = fixture;
//        this.fixture.Services.AddSingleton(MessageState);
//    }

//    [Fact]
//    public async Task Publish_SingleMessageNoSubscriber_MessageHandledByNone()
//    {
//        // Arrange
//        var ticks = DateTime.UtcNow.Ticks;
//        var message = new MessageStub() { FirstName = $"1John{ticks}", LastName = $"1Doe{ticks}" };
//        var sut = this.CreateMessageBroker();

//        // Act
//        await sut.Publish(message);
//        await Task.Delay(500);

//        // Assert
//        await sut.Unsubscribe<MessageStub, MessageStubHandler>();
//        MessageState.HandledMessageIds.Count(i => i == message.Id).ShouldBe(0);
//    }

//    [Fact]
//    public async Task Publish_SingleMessage_MessageHandledBySingleHandler()
//    {
//        // Arrange
//        var ticks = DateTime.UtcNow.Ticks;
//        var message = new MessageStub() { FirstName = $"1John{ticks}", LastName = $"1Doe{ticks}" };
//        var sut = this.CreateMessageBroker();
//        await sut.Subscribe<MessageStub, MessageStubHandler>();

//        // Act
//        await sut.Publish(message);
//        await Task.Delay(500);

//        // Assert
//        await sut.Unsubscribe<MessageStub, MessageStubHandler>();
//        MessageState.HandledMessageIds.Count(i => i == message.Id).ShouldBe(1);
//        MessageState.HandledMessageIds.ShouldContain(message.Id);
//    }

//    [Fact]
//    public async Task Publish_SingleMessage_MessageHandledBySingleHandler2()
//    {
//        // Arrange
//        var ticks = DateTime.UtcNow.Ticks;
//        var message1 = new MessageStub() { FirstName = $"1John{ticks}", LastName = $"1Doe{ticks}" };
//        var message2 = new MessageStub() { FirstName = $"1John{ticks}", LastName = $"1Doe{ticks}" };
//        var sut = this.CreateMessageBroker();
//        await sut.Subscribe<MessageStub, MessageStubHandler>();

//        // Act
//        await sut.Publish(message1);
//        await Task.Delay(500);
//        await sut.Unsubscribe<MessageStub, MessageStubHandler>();
//        await sut.Publish(message2);

//        // Assert
//        MessageState.HandledMessageIds.Count(i => i == message1.Id).ShouldBe(1);
//        MessageState.HandledMessageIds.Count(i => i == message2.Id).ShouldBe(0);
//        MessageState.HandledMessageIds.ShouldContain(message1.Id);
//        MessageState.HandledMessageIds.ShouldNotContain(message2.Id);
//    }

//    [Fact]
//    public async Task Publish_SingleMessage_MessageHandledByAllHandlers()
//    {
//        // Arrange
//        var ticks = DateTime.UtcNow.Ticks;
//        var message = new MessageStub() { FirstName = $"2John{ticks}", LastName = $"2Doe{ticks}" };
//        var sut = this.CreateMessageBroker();
//        await sut.Subscribe<MessageStub, MessageStubHandler>();
//        await sut.Subscribe<MessageStub, AnotherMessageStubHandler>();

//        // Act
//        await sut.Publish(message, CancellationToken.None);
//        await Task.Delay(500);

//        // Assert
//        await sut.Unsubscribe<MessageStub, MessageStubHandler>();
//        await sut.Unsubscribe<MessageStub, AnotherMessageStubHandler>();
//        MessageState.HandledMessageIds.Count(i => i == message.Id).ShouldBe(2);
//        MessageState.HandledMessageIds.ShouldContain(message.Id);
//    }

//    private IMessageBroker CreateMessageBroker()
//    {
//        return new ServiceBusMessageBroker(o => o
//            .ConnectionString("Endpoint=sb://global-sb01.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=Him5iahSr78fBSYwRMmVKP137d4jPNKgY+ASbKyiW0U=")
//            .HandlerFactory(new ServiceProviderMessageHandlerFactory(this.fixture.ServiceProvider))
//            .MachineTopicScope("_test")
//            .Serializer(new SystemTextJsonSerializer()));
//    }
//}