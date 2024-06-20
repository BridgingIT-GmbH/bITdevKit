// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

[IntegrationTest("Infrastructure")]
//[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class MessagingBuilderContextTests(ITestOutputHelper output) : TestsBase(output, s =>
        {
            s.AddMediatR();

            s.AddMessaging(o => o
                .StartupDelay("00:00:10"))
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
    public void GetBroker_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var sut = this.ServiceProvider.GetService<IMessageBroker>();

        // Assert
        sut.ShouldNotBeNull();
    }
}
