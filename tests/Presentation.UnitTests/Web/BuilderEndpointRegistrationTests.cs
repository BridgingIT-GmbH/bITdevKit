// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Messaging;
using BridgingIT.DevKit.Presentation.Web.Queueing;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Presentation")]
public class BuilderEndpointRegistrationTests
{
    [Fact]
    public void MessagingBuilder_AddEndpoints_RegistersMessagingEndpointsAndOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new MessagingEndpointsOptions { RequireAuthorization = true };

        // Act
        services.AddMessaging()
            .AddEndpoints(options);

        // Assert
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(IEndpoints) &&
            descriptor.ImplementationType == typeof(MessagingEndpoints))
            .ShouldBeTrue();
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(MessagingEndpointsOptions) &&
            ReferenceEquals(descriptor.ImplementationInstance, options))
            .ShouldBeTrue();
    }

    [Fact]
    public void QueueingBuilder_AddEndpoints_RegistersQueueingEndpointsAndOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new QueueingEndpointsOptions { RequireAuthorization = true };

        // Act
        services.AddQueueing()
            .AddEndpoints(options);

        // Assert
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(IEndpoints) &&
            descriptor.ImplementationType == typeof(QueueingEndpoints))
            .ShouldBeTrue();
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(QueueingEndpointsOptions) &&
            ReferenceEquals(descriptor.ImplementationInstance, options))
            .ShouldBeTrue();
    }
}
