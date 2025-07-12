// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Application.Requester;
using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="NotificationBehaviorsProvider"/> class.
/// </summary>
public class NotificationBehaviorsProviderTests
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

        var sut = new NotificationBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var behaviors = sut.GetBehaviors<EmailSentNotification>(serviceProvider);

        // Assert
        behaviors.ShouldNotBeNull();
        behaviors.Count.ShouldBe(1);
        behaviors[0].ShouldBeOfType(typeof(TestBehavior<EmailSentNotification, IResult>));
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

        var sut = new NotificationBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var behaviors = sut.GetBehaviors<EmailSentNotification>(serviceProvider);

        // Assert
        behaviors.ShouldNotBeNull();
        behaviors.Count.ShouldBe(2);
        behaviors[0].ShouldBeOfType(typeof(TestBehavior<EmailSentNotification, IResult>));
        behaviors[1].ShouldBeOfType(typeof(AnotherTestBehavior<EmailSentNotification, IResult>));
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
        var sut = new NotificationBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var behaviors = sut.GetBehaviors<EmailSentNotification>(serviceProvider);

        // Assert
        behaviors.ShouldNotBeNull();
        behaviors.Count.ShouldBe(0);
    }

    /// <summary>
    /// Tests that the provider returns an empty list when the service provider is null.
    /// </summary>
    [Fact]
    public void GetBehaviors_NullServiceProvider_ReturnsEmptyList()
    {
        // Arrange
        var pipelineBehaviorTypes = new List<Type>();
        var sut = new NotificationBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var behaviors = sut.GetBehaviors<EmailSentNotification>(null);

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
            new NotificationBehaviorsProvider(null));

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

        var sut = new NotificationBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            sut.GetBehaviors<EmailSentNotification>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that the provider throws an exception when the registered behavior type does not implement the expected interface.
    /// </summary>
    [Fact]
    public void GetBehaviors_InvalidBehaviorTypeInList_ThrowsInvalidOperationException()
    {
        // Arrange
        var pipelineBehaviorTypes = new List<Type> { typeof(InvalidBehavior) };
        var services = new ServiceCollection();
        services.AddScoped<InvalidBehavior>();
        var serviceProvider = services.BuildServiceProvider();

        var sut = new NotificationBehaviorsProvider(pipelineBehaviorTypes);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            sut.GetBehaviors<EmailSentNotification>(serviceProvider));

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
    }
}

/// <summary>
/// A sample notification for testing purposes with validation.
/// </summary>
public class EmailSentNotification : NotificationBase
{
    public string EmailAddress { get; set; }

    public class Validator : AbstractValidator<EmailSentNotification>
    {
        public Validator()
        {
            this
                .RuleFor(x => x.EmailAddress)
                .NotEmpty().WithMessage("Email cannot be empty.")
                .EmailAddress().WithMessage("Invalid email format.");
        }
    }
}

/// <summary>
/// A sample notification for testing validation failure.
/// </summary>
public class InvalidEmailNotification : NotificationBase
{
    public string EmailAddress { get; set; }

    public class Validator : AbstractValidator<InvalidEmailNotification>
    {
        public Validator()
        {
            this
                .RuleFor(x => x.EmailAddress)
                .NotEmpty().WithMessage("Email cannot be empty.")
                .EmailAddress().WithMessage("Invalid email format.");
        }
    }
}

/// <summary>
/// A sample notification handler for testing purposes.
/// </summary>
public class EmailSentNotificationHandler : NotificationHandlerBase<EmailSentNotification>
{
    protected override Task<Result> HandleAsync(EmailSentNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// A sample notification handler for testing validation failure.
/// </summary>
public class InvalidEmailNotificationHandler : NotificationHandlerBase<InvalidEmailNotification>
{
    protected override Task<Result> HandleAsync(InvalidEmailNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// Another sample notification for testing purposes, used for empty cache scenario.
/// </summary>
public class AnotherEmailSentNotification : NotificationBase;