// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Messaging;

using Application.Messaging;
using Microsoft.Extensions.Logging;

[UnitTest("Application")]
public class DummyMessagePublisherBehaviorTests
{
    [Fact]
    public async Task Publish_WhenCancelled_ShouldNotCallNext()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var message = new MessageStub();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var cancellationToken = cancellationTokenSource.Token;
        var next = Substitute.For<MessagePublisherDelegate>();
        var sut = new DummyMessagePublisherBehavior(loggerFactory);

        // Act
        await sut.Publish(message, cancellationToken, next);

        // Assert
        await next.DidNotReceive()
            .Invoke();
    }

    [Fact]
    public async Task Publish_WhenNotCancelled_ShouldCallNext()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var message = new MessageStub();
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var next = Substitute.For<MessagePublisherDelegate>();
        var sut = new DummyMessagePublisherBehavior(loggerFactory);

        // Act
        await sut.Publish(message, cancellationToken, next);

        // Assert
        await next.Received()
            .Invoke();
    }
}