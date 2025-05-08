// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Application.Requester;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="NotificationHandlerProvider"/> class.
/// </summary>
public class NotificationHandlerProviderTests
{
    /// <summary>
    /// Tests that the provider resolves handlers successfully when the handler type is registered.
    /// </summary>
    [Fact]
    public void GetHandlers_RegisteredHandler_ReturnsHandler()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var handlerType = typeof(EmailSentNotificationHandler);
        handlerCache.TryAdd(typeof(INotificationHandler<EmailSentNotification>), handlerType);

        var services = new ServiceCollection();
        services.AddScoped<INotificationHandler<EmailSentNotification>, EmailSentNotificationHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var sut = new NotificationHandlerProvider(handlerCache);

        // Act
        var handlers = sut.GetHandlers<EmailSentNotification>(serviceProvider);

        // Assert
        handlers.ShouldNotBeNull();
        handlers.Count.ShouldBe(1);
        handlers[0].ShouldBeOfType<EmailSentNotificationHandler>();
    }

    /// <summary>
    /// Tests that the provider throws a <see cref="NotifierException"/> when the handler type is not in the cache.
    /// </summary>
    [Fact]
    public void GetHandlers_UnregisteredHandler_ThrowsNotifierException()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var sut = new NotificationHandlerProvider(handlerCache);

        // Act
        var exception = Should.Throw<NotifierException>(() =>
            sut.GetHandlers<EmailSentNotification>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that the provider throws an exception when the handler type is in the cache but not registered in the DI container.
    /// </summary>
    [Fact]
    public void GetHandlers_HandlerNotInDIContainer_ThrowsInvalidOperationException()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var handlerType = typeof(EmailSentNotificationHandler);
        handlerCache.TryAdd(typeof(INotificationHandler<EmailSentNotification>), handlerType);

        var services = new ServiceCollection();
        // Intentionally not registering EmailSentNotificationHandler in DI container
        var serviceProvider = services.BuildServiceProvider();

        var sut = new NotificationHandlerProvider(handlerCache);

        // Act
        var exception = Should.Throw<NotifierException>(() =>
            sut.GetHandlers<EmailSentNotification>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that the provider throws an exception when the handler cache is empty.
    /// </summary>
    [Fact]
    public void GetHandlers_EmptyHandlerCache_ThrowsNotifierException()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var sut = new NotificationHandlerProvider(handlerCache);

        // Act
        var exception = Should.Throw<NotifierException>(() =>
            sut.GetHandlers<AnotherEmailSentNotification>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that constructing the provider with a null handler cache throws an <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullHandlerCache_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var exception = Should.Throw<ArgumentNullException>(() =>
            new NotificationHandlerProvider(null));

        // Assert
        exception.ParamName.ShouldBe("handlerCache");
    }

    /// <summary>
    /// Tests that the provider handles concurrent access to the handler cache correctly.
    /// </summary>
    [Fact]
    public async Task GetHandlers_ConcurrentAccess_ResolvesHandlerConsistently()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var handlerType = typeof(EmailSentNotificationHandler);
        handlerCache.TryAdd(typeof(INotificationHandler<EmailSentNotification>), handlerType);

        var services = new ServiceCollection();
        services.AddScoped<INotificationHandler<EmailSentNotification>, EmailSentNotificationHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var sut = new NotificationHandlerProvider(handlerCache);

        // Act
        const int numberOfTasks = 100;
        var tasks = new Task<IReadOnlyList<INotificationHandler<EmailSentNotification>>>[numberOfTasks];
        for (var i = 0; i < numberOfTasks; i++)
        {
            tasks[i] = Task.Run(() => sut.GetHandlers<EmailSentNotification>(serviceProvider));
        }

        await Task.WhenAll(tasks);

        // Assert
        foreach (var task in tasks)
        {
            var handlers = await task;
            handlers.ShouldNotBeNull();
            handlers.Count.ShouldBe(1);
            handlers[0].ShouldBeOfType<EmailSentNotificationHandler>();
        }
    }

    /// <summary>
    /// Tests that the provider throws an exception when the cached handler type does not implement the expected interface.
    /// </summary>
    [Fact]
    public void GetHandlers_InvalidHandlerTypeInCache_ThrowsNotifierException()
    {
        // Arrange
        var handlerCache = new HandlerCache();
        var invalidHandlerType = typeof(InvalidHandler); // Does not implement INotificationHandler<EmailSentNotification>
        handlerCache.TryAdd(typeof(INotificationHandler<EmailSentNotification>), invalidHandlerType);

        var services = new ServiceCollection();
        services.AddScoped<InvalidHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var sut = new NotificationHandlerProvider(handlerCache);

        // Act
        var exception = Should.Throw<NotifierException>(() =>
            sut.GetHandlers<EmailSentNotification>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }
}