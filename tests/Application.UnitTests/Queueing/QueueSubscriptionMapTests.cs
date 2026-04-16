// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;

public class QueueSubscriptionMapTests
{
    [Fact]
    public void Add_WhenDifferentHandlerIsAlreadyRegisteredForMessage_ThrowsArgumentException()
    {
        // Arrange
        var sut = new QueueSubscriptionMap();
        sut.Add<TestQueueMessage, FirstTestQueueMessageHandler>();

        // Act
        var act = () => sut.Add<TestQueueMessage, SecondTestQueueMessageHandler>();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    public sealed class TestQueueMessage(string value) : QueueMessageBase
    {
        public string Value { get; } = value;
    }

    public sealed class FirstTestQueueMessageHandler : IQueueMessageHandler<TestQueueMessage>
    {
        public Task Handle(TestQueueMessage message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class SecondTestQueueMessageHandler : IQueueMessageHandler<TestQueueMessage>
    {
        public Task Handle(TestQueueMessage message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}