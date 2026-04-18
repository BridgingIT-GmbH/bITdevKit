// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Messaging;
using BridgingIT.DevKit.Presentation.Web.Queueing;
using BridgingIT.DevKit.Presentation.Web.Storage;
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

    [Fact]
    public void MessagingBuilder_AddEndpoints_WithOptionsBuilder_RegistersConfiguredOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMessaging()
            .AddEndpoints(options => options
                .RequireAuthorization()
                .GroupPath("/api/test/messages")
                .GroupTag("messages"));

        // Assert
        var options = services.Single(descriptor => descriptor.ServiceType == typeof(MessagingEndpointsOptions))
            .ImplementationInstance as MessagingEndpointsOptions;

        options.ShouldNotBeNull();
        options.RequireAuthorization.ShouldBeTrue();
        options.GroupPath.ShouldBe("/api/test/messages");
        options.GroupTag.ShouldBe("messages");
    }

    [Fact]
    public void QueueingBuilder_AddEndpoints_WithOptionsBuilder_RegistersConfiguredOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddQueueing()
            .AddEndpoints(options => options
                .RequireAuthorization()
                .GroupPath("/api/test/queueing")
                .GroupTag("queueing"));

        // Assert
        var options = services.Single(descriptor => descriptor.ServiceType == typeof(QueueingEndpointsOptions))
            .ImplementationInstance as QueueingEndpointsOptions;

        options.ShouldNotBeNull();
        options.RequireAuthorization.ShouldBeTrue();
        options.GroupPath.ShouldBe("/api/test/queueing");
        options.GroupTag.ShouldBe("queueing");
    }

    [Fact]
    public void FileStorageBuilder_AddEndpoints_WithOptionsBuilder_RegistersConfiguredOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFileStorage(factory => factory.RegisterProvider("documents", builder => builder.UseInMemory("Documents")))
            .AddEndpoints(options => options
                .RequireAuthorization()
                .GroupPath("/api/test/storage")
                .GroupTag("storage"));

        // Assert
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(IEndpoints) &&
            descriptor.ImplementationType == typeof(FileStorageEndpoints))
            .ShouldBeTrue();

        var options = services.Single(descriptor => descriptor.ServiceType == typeof(FileStorageEndpointsOptions))
            .ImplementationInstance as FileStorageEndpointsOptions;

        options.ShouldNotBeNull();
        options.RequireAuthorization.ShouldBeTrue();
        options.GroupPath.ShouldBe("/api/test/storage");
        options.GroupTag.ShouldBe("storage");
    }

    [Fact]
    public void MessagingService_AddEndpoints_WithOptionsBuilder_RegistersConfiguredOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMessagingEndpoints(options => options
            .RequireAuthorization()
            .GroupPath("/api/test/messages/direct")
            .GroupTag("messages-direct"));

        // Assert
        var options = services.Single(descriptor => descriptor.ServiceType == typeof(MessagingEndpointsOptions))
            .ImplementationInstance as MessagingEndpointsOptions;

        options.ShouldNotBeNull();
        options.RequireAuthorization.ShouldBeTrue();
        options.GroupPath.ShouldBe("/api/test/messages/direct");
        options.GroupTag.ShouldBe("messages-direct");
    }

    [Fact]
    public void QueueingService_AddEndpoints_WithOptionsBuilder_RegistersConfiguredOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddQueueingEndpoints(options => options
            .RequireAuthorization()
            .GroupPath("/api/test/queueing/direct")
            .GroupTag("queueing-direct"));

        // Assert
        var options = services.Single(descriptor => descriptor.ServiceType == typeof(QueueingEndpointsOptions))
            .ImplementationInstance as QueueingEndpointsOptions;

        options.ShouldNotBeNull();
        options.RequireAuthorization.ShouldBeTrue();
        options.GroupPath.ShouldBe("/api/test/queueing/direct");
        options.GroupTag.ShouldBe("queueing-direct");
    }

    [Fact]
    public void FileStorageService_AddEndpoints_WithOptionsBuilder_RegistersConfiguredOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFileStorageEndpoints(options => options
            .RequireAuthorization()
            .GroupPath("/api/test/storage/direct")
            .GroupTag("storage-direct"));

        // Assert
        var options = services.Single(descriptor => descriptor.ServiceType == typeof(FileStorageEndpointsOptions))
            .ImplementationInstance as FileStorageEndpointsOptions;

        options.ShouldNotBeNull();
        options.RequireAuthorization.ShouldBeTrue();
        options.GroupPath.ShouldBe("/api/test/storage/direct");
        options.GroupTag.ShouldBe("storage-direct");
    }
}
