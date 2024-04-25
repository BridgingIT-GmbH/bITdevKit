// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories;

using System;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[UnitTest("Infrastructure")]
public class OutboxMessagePublisherBehaviorTests : IClassFixture<StubDbContextFixture>
{
    private readonly StubDbContextFixture fixture;

    public OutboxMessagePublisherBehaviorTests(StubDbContextFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Publish_IsCalled_OutboxReceivedMessage()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var message = new StubMessage { FirstName = $"John {ticks}", LastName = $"Doe {ticks}" };
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var services = new ServiceCollection()
            .AddScoped(sp => this.fixture.Context);
        var serviceProvider = services.BuildServiceProvider();

        var sut = new InProcessMessageBroker(o => o
            .HandlerFactory(new ServiceProviderMessageHandlerFactory(serviceProvider))
            .WithBehavior(new DummyMessagePublisherBehavior(loggerFactory))
            .WithBehavior(new OutboxMessagePublisherBehavior<StubDbContext>(loggerFactory, serviceProvider)));

        // Act
        await sut.Publish(message, cancellationToken);

        // Assert
        //message.Properties.ContainsKey(nameof(OutboxMessagePublisherBehavior<StubDbContext>));
        message.Properties.ContainsKey("OutboxMessagePublisherBehavior");
    }
}