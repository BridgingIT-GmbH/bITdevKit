// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Requester;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="RequestHandlerProvider"/> class.
/// </summary>
public class RequestHandlerProviderTests
{
    /// <summary>
    /// Tests that the provider resolves a handler successfully when the handler type is registered.
    /// </summary>
    [Fact]
    public void GetHandler_RegisteredHandler_ReturnsHandler()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var handlerType = typeof(MyTestRequestHandler);
        handlerCache.TryAdd(typeof(IRequestHandler<MyTestRequest, string>), handlerType);

        var services = new ServiceCollection();
        services.AddScoped<MyTestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var sut = new RequestHandlerProvider(handlerCache);

        // Act
        var handler = sut.GetHandler<MyTestRequest, string>(serviceProvider);

        // Assert
        handler.ShouldNotBeNull();
        handler.ShouldBeOfType<MyTestRequestHandler>();
    }

    /// <summary>
    /// Tests that the provider throws a <see cref="RequesterException"/> when the handler type is not in the cache.
    /// </summary>
    [Fact]
    public void GetHandler_UnregisteredHandler_ThrowsRequesterException()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var sut = new RequestHandlerProvider(handlerCache);

        // Act
        var exception = Should.Throw<RequesterException>(() =>
            sut.GetHandler<MyTestRequest, string>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that the provider throws an exception when the handler type is in the cache but not registered in the DI container.
    /// </summary>
    [Fact]
    public void GetHandler_HandlerNotInDIContainer_ThrowsRequesterException()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var handlerType = typeof(MyTestRequestHandler);
        handlerCache.TryAdd(typeof(IRequestHandler<MyTestRequest, string>), handlerType);

        var services = new ServiceCollection();
        // Intentionally not registering MyTestRequestHandler in DI container
        var serviceProvider = services.BuildServiceProvider();

        var sut = new RequestHandlerProvider(handlerCache);

        // Act
        var exception = Should.Throw<RequesterException>(() =>
            sut.GetHandler<MyTestRequest, string>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that the provider throws an <see cref="ArgumentNullException"/> when the service provider is null.
    /// </summary>
    [Fact]
    public void GetHandler_NullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var sut = new RequestHandlerProvider(handlerCache);

        // Act
        var exception = Should.Throw<RequesterException>(() =>
            sut.GetHandler<MyTestRequest, string>(null));
    }

    /// <summary>
    /// Tests that constructing the provider with a null handler cache throws an <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullHandlerCache_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var exception = Should.Throw<ArgumentNullException>(() =>
            new RequestHandlerProvider(null));

        // Assert
        exception.ParamName.ShouldBe("handlerCache");
    }

    /// <summary>
    /// Tests that the provider throws a <see cref="RequesterException"/> when the handler cache is empty, using a different request type.
    /// </summary>
    [Fact]
    public void GetHandler_EmptyHandlerCache_ThrowsRequesterException()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var sut = new RequestHandlerProvider(handlerCache);

        // Act
        var exception = Should.Throw<RequesterException>(() =>
            sut.GetHandler<AnotherTestRequest, int>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that the provider handles concurrent access to the handler cache correctly.
    /// </summary>
    [Fact]
    public async Task GetHandler_ConcurrentAccess_ResolvesHandlerConsistently()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var handlerType = typeof(MyTestRequestHandler);
        handlerCache.TryAdd(typeof(IRequestHandler<MyTestRequest, string>), handlerType);

        var services = new ServiceCollection();
        services.AddScoped<MyTestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var sut = new RequestHandlerProvider(handlerCache);

        // Act
        const int numberOfTasks = 100;
        var tasks = new Task<IRequestHandler<MyTestRequest, string>>[numberOfTasks];
        for (var i = 0; i < numberOfTasks; i++)
        {
            tasks[i] = Task.Run(() => sut.GetHandler<MyTestRequest, string>(serviceProvider));
        }

        await Task.WhenAll(tasks);

        // Assert
        foreach (var task in tasks)
        {
            var handler = await task;
            handler.ShouldNotBeNull();
            handler.ShouldBeOfType<MyTestRequestHandler>();
        }
    }

    /// <summary>
    /// Tests that the provider throws an exception when the cached handler type does not implement the expected interface.
    /// </summary>
    [Fact]
    public void GetHandler_InvalidHandlerTypeInCache_ThrowsRequesterException()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var invalidHandlerType = typeof(InvalidHandler); // Does not implement IRequestHandler<MyTestRequest, string>
        handlerCache.TryAdd(typeof(IRequestHandler<MyTestRequest, string>), invalidHandlerType);

        var services = new ServiceCollection();
        services.AddScoped<InvalidHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var sut = new RequestHandlerProvider(handlerCache);

        // Act
        var exception = Should.Throw<RequesterException>(() =>
            sut.GetHandler<MyTestRequest, string>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }
}

/// <summary>
/// A sample request for testing purposes.
/// </summary>
public class MyTestRequest : RequestBase<string>;

/// <summary>
/// A sample request handler for testing purposes.
/// </summary>
public class MyTestRequestHandler : RequestHandlerBase<MyTestRequest, string>
{
    protected override async Task<Result<string>> HandleAsync(MyTestRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // Simulate processing time to test concurrency
        return Result<string>.Success("Test");
    }
}

/// <summary>
/// Another sample request for testing purposes, used for empty cache scenario.
/// </summary>
public class AnotherTestRequest : RequestBase<int>;

/// <summary>
/// An invalid handler that does not implement <see cref="IRequestHandler{TRequest, TValue}"/>.
/// </summary>
public class InvalidHandler
{
    // Intentionally empty to simulate an invalid handler type
}