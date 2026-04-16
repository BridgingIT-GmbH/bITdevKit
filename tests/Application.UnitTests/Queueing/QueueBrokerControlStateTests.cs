// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;

public class QueueBrokerControlStateTests
{
    [Fact]
    public void PauseQueue_WhenCalled_AddsQueueToPausedSet()
    {
        // Arrange
        var sut = new QueueBrokerControlState();

        // Act
        sut.PauseQueue("orders");

        // Assert
        sut.IsQueuePaused("orders").ShouldBeTrue();
        sut.GetPausedQueues().ShouldContain("orders");
    }

    [Fact]
    public void ResumeMessageType_WhenPaused_RemovesMessageTypeFromPausedSet()
    {
        // Arrange
        var sut = new QueueBrokerControlState();
        sut.PauseMessageType("OrderQueuedMessage");

        // Act
        sut.ResumeMessageType("OrderQueuedMessage");

        // Assert
        sut.IsMessageTypePaused("OrderQueuedMessage").ShouldBeFalse();
        sut.GetPausedTypes().ShouldNotContain("OrderQueuedMessage");
    }
}