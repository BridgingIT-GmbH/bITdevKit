// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Requester;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="RequestBehaviorsProvider"/> class.
/// </summary>
public class RequestBehaviorsProviderTests
{
    /// <summary>
    /// Tests that the provider resolves a single behavior successfully when the behavior type is registered.
    /// </summary>
    [Fact]
    public void GetBehaviors_RegisteredBehavior_ReturnsBehavior()
    {
        // Arrange
        var pipelineBehaviorTypes = new List<Type> { typeof(TestBehavior<,>) };
        var services = new ServiceCollection();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TestBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();

        var sut = new RequestBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var behaviors = sut.GetBehaviors<MyTestRequest, string>(serviceProvider);

        // Assert
        behaviors.ShouldNotBeNull();
        behaviors.Count.ShouldBe(1);
        behaviors[0].ShouldBeOfType(typeof(TestBehavior<MyTestRequest, IResult<string>>));
        //behaviors[0].ShouldBeOfType<TestBehavior>(); // How to assert?
    }

    /// <summary>
    /// Tests that the provider resolves multiple behaviors in the correct order when registered.
    /// </summary>
    [Fact]
    public void GetBehaviors_MultipleRegisteredBehaviors_ReturnsBehaviorsInOrder()
    {
        // Arrange
        var pipelineBehaviorTypes = new List<Type>
        {
            typeof(TestBehavior<,>),
            typeof(AnotherTestBehavior<,>)
        };
        var services = new ServiceCollection();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TestBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AnotherTestBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();

        var sut = new RequestBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var behaviors = sut.GetBehaviors<MyTestRequest, string>(serviceProvider);

        // Assert
        behaviors.ShouldNotBeNull();
        behaviors.Count.ShouldBe(2);
        behaviors[0].ShouldBeOfType(typeof(TestBehavior<MyTestRequest, IResult<string>>));
        behaviors[1].ShouldBeOfType(typeof(AnotherTestBehavior<MyTestRequest, IResult<string>>));
        //behaviors[0].ShouldBeOfType<TestBehavior>(); // How to assert?
        //behaviors[1].ShouldBeOfType<AnotherTestBehavior>(); // How to assert?
    }

    /// <summary>
    /// Tests that the provider returns an empty list when no behavior types are registered.
    /// </summary>
    [Fact]
    public void GetBehaviors_NoRegisteredBehaviors_ReturnsEmptyList()
    {
        // Arrange
        var pipelineBehaviorTypes = new List<Type>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var sut = new RequestBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var behaviors = sut.GetBehaviors<MyTestRequest, string>(serviceProvider);

        // Assert
        behaviors.ShouldNotBeNull();
        behaviors.Count.ShouldBe(0);
    }

    /// <summary>
    /// Tests that the provider throws an <see cref="ArgumentNullException"/> when the service provider is null.
    /// </summary>
    [Fact]
    public void GetBehaviors_NullServiceProvider_ReturnsEmptyLis()
    {
        // Arrange
        var pipelineBehaviorTypes = new List<Type>();
        var sut = new RequestBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var behaviors = sut.GetBehaviors<MyTestRequest, string>(null);

        // Assert
        behaviors.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that constructing the provider with a null behavior types list throws an <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullPipelineBehaviorTypes_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var exception = Should.Throw<ArgumentNullException>(() =>
            new RequestBehaviorsProvider(null));

        // Assert
        exception.ParamName.ShouldBe("pipelineBehaviorTypes");
    }

    /// <summary>
    /// Tests that the provider throws an exception when a behavior type is in the list but not registered in the DI container.
    /// </summary>
    [Fact]
    public void GetBehaviors_BehaviorNotInDIContainer_ThrowsInvalidOperationException()
    {
        // Arrange
        var pipelineBehaviorTypes = new List<Type> { typeof(TestBehavior<,>) };
        var services = new ServiceCollection();
        // Intentionally not registering TestBehavior in DI container
        var serviceProvider = services.BuildServiceProvider();

        var sut = new RequestBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            sut.GetBehaviors<MyTestRequest, string>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that the provider throws an exception when the registered behavior type does not implement the expected interface.
    /// </summary>
    [Fact]
    public void GetBehaviors_InvalidBehaviorTypeInList_InvalidOperationException()
    {
        // Arrange
        var pipelineBehaviorTypes = new List<Type> { typeof(InvalidBehavior) };
        var services = new ServiceCollection();
        services.AddScoped<InvalidBehavior>();
        var serviceProvider = services.BuildServiceProvider();

        var sut = new RequestBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            sut.GetBehaviors<MyTestRequest, string>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }
}

/// <summary>
/// A sample behavior for testing purposes.
/// </summary>
public class TestBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    public Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        return next();
    }
}

/// <summary>
/// Another sample behavior for testing purposes.
/// </summary>
public class AnotherTestBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    public Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        return next();
    }
}

/// <summary>
/// An invalid behavior that does not implement the expected <see cref="IPipelineBehavior{TRequest, TResponse}"/> interface.
/// </summary>
public class InvalidBehavior
{
    // Intentionally empty to simulate an invalid behavior type
}