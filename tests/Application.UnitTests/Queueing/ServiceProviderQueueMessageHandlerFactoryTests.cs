// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.DependencyInjection;

public class ServiceProviderQueueMessageHandlerFactoryTests
{
    [Fact]
    public void Create_WhenHandlerHasRegisteredDependencies_ReturnsResolvedHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<TestDependency>();
        using var provider = services.BuildServiceProvider();
        var sut = new ServiceProviderQueueMessageHandlerFactory(provider);

        // Act
        var result = sut.Create(typeof(TestQueueMessageHandler));

        // Assert
        result.ShouldBeOfType<TestQueueMessageHandler>();
        ((TestQueueMessageHandler)result).Dependency.ShouldNotBeNull();
    }

    private sealed class TestDependency;

    private sealed class TestQueueMessageHandler(TestDependency dependency)
    {
        public TestDependency Dependency { get; } = dependency;
    }
}