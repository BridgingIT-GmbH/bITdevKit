// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Application.Requester;
using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="Notifier"/> class.
/// </summary>
public class NotifierTests
{
    private readonly IServiceProvider serviceProvider;

    public NotifierTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(TestBehavior<,>))
            .WithBehavior(typeof(AnotherTestBehavior<,>));
        this.serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Tests that PublishAsync successfully processes a notification and returns a result.
    /// </summary>
    [Fact]
    public async Task PublishAsync_SuccessfulNotification_ReturnsSuccessResult()
    {
        // Arrange
        var notifier = this.serviceProvider.GetService<INotifier>();
        var notification = new EmailSentNotification();

        // Act
        var result = await notifier.PublishAsync(notification);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that PublishAsync applies behaviors in the correct order.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithBehaviors_AppliesInCorrectOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(OrderedBehavior<,>))
            .WithBehavior(typeof(AnotherOrderedBehavior<,>));
        var behaviorOrder = new List<string>();
        services.AddScoped(sp => behaviorOrder);
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new EmailSentNotification();

        // Act
        var result = await notifier.PublishAsync(notification);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        behaviorOrder.ShouldBe(new[] { typeof(OrderedBehavior<EmailSentNotification, IResult>).PrettyName(), typeof(AnotherOrderedBehavior<EmailSentNotification, IResult>).PrettyName() });
    }

    /// <summary>
    /// Tests that PublishAsync catches exceptions and returns a failed result when HandleExceptionsAsResultError is true.
    /// </summary>
    [Fact]
    public async Task PublishAsync_HandlesExceptionsAsResultError_WhenFlagIsTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers();
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new FailingNotification();
        var options = new PublishOptions { HandleExceptionsAsResultError = true };

        // Act
        var result = await notifier.PublishAsync(notification, options);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldBeOfType<ExceptionError>();
        var error = (ExceptionError)result.Errors[0];
        error.Exception.Message.ShouldBe("Test failure");
    }

    /// <summary>
    /// Tests that PublishAsync throws exceptions when HandleExceptionsAsResultError is false.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ThrowsExceptions_WhenFlagIsFalse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers();
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new FailingNotification();
        var options = new PublishOptions { HandleExceptionsAsResultError = false };

        // Act
        var exception = await Should.ThrowAsync<Exception>(async () =>
            await notifier.PublishAsync(notification, options));

        // Assert
        exception.Message.ShouldBe("Test failure");
    }

    /// <summary>
    /// Tests that PublishAsync throws an ArgumentNullException for a null notification.
    /// </summary>
    [Fact]
    public async Task PublishAsync_NullNotification_ThrowsArgumentNullException()
    {
        // Arrange
        var notifier = this.serviceProvider.GetService<INotifier>();
        EmailSentNotification notification = null; // Simulate a null notification

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await notifier.PublishAsync(notification));
    }

    /// <summary>
    /// Tests that PublishAsync throws a NotifierException when no handler is found.
    /// </summary>
    [Fact]
    public async Task PublishAsync_NoHandlerFound_ThrowsNotifierException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers();
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new AnotherEmailSentNotification(); // No handler registered for this notification type

        // Act & Assert
        var exception = await Should.ThrowAsync<NotifierException>(async () =>
            await notifier.PublishAsync(notification));
        exception.Message.ShouldBe("No handlers found for notification type AnotherEmailSentNotification");
    }

    /// <summary>
    /// Tests that PublishAsync respects cancellation tokens.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithCancellationToken_CancelsOperation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers();
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new DelayedNotification();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Should.ThrowAsync<TaskCanceledException>(async () =>
            await notifier.PublishAsync(notification, null, cts.Token));
    }

    /// <summary>
    /// Tests that GetRegistrationInformation returns correct handler mappings and behavior types.
    /// </summary>
    [Fact]
    public void GetRegistrationInformation_ReturnsCorrectInformation()
    {
        // Arrange
        var notifier = this.serviceProvider.GetService<INotifier>();

        // Act
        var info = notifier.GetRegistrationInformation();

        // Assert
        info.HandlerMappings.ShouldNotBeNull();
        info.HandlerMappings.ShouldContainKey(nameof(EmailSentNotification));
        info.HandlerMappings[nameof(EmailSentNotification)].ShouldContain(nameof(EmailSentNotificationHandler));

        info.BehaviorTypes.ShouldNotBeNull();
        info.BehaviorTypes.ShouldBe(["TestBehavior<TRequest,TResponse>", "AnotherTestBehavior<TRequest,TResponse>"]);
    }

    /// <summary>
    /// Tests that PublishAsync processes multiple notifications independently without interference.
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleNotifications_ProcessesIndependently()
    {
        // Arrange
        var notifier = this.serviceProvider.GetService<INotifier>();
        var notification1 = new EmailSentNotification();
        var notification2 = new EmailSentNotification();

        // Act
        var result1 = await notifier.PublishAsync(notification1);
        var result2 = await notifier.PublishAsync(notification2);

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        notification1.NotificationId.ShouldNotBe(notification2.NotificationId); // Ensure notifications are independent
    }

    /// <summary>
    /// Tests that PublishAsync processes multiple notifications concurrently without interference in concurrent mode.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ConcurrentNotifications_ProcessesCorrectly()
    {
        // Arrange
        var notifier = this.serviceProvider.GetService<INotifier>();
        var notifications = new List<EmailSentNotification>
        {
            new(),
            new(),
            new()
        };
        var options = new PublishOptions { ExecutionMode = ExecutionMode.Concurrent };

        // Act
        var tasks = notifications.Select(notification => notifier.PublishAsync(notification, options)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Length.ShouldBe(3);
        foreach (var result in results)
        {
            result.IsSuccess.ShouldBeTrue();
        }

        // Verify that each notification has a unique NotificationId
        var notificationIds = notifications.Select(n => n.NotificationId).Distinct().ToList();
        notificationIds.Count.ShouldBe(3); // Ensure all NotificationIds are unique
    }

    /// <summary>
    /// Tests that PublishAsync processes notifications in fire-and-forget mode.
    /// </summary>
    [Fact]
    public async Task PublishAsync_FireAndForget_ReturnsSuccessImmediately()
    {
        // Arrange
        var notifier = this.serviceProvider.GetService<INotifier>();
        var notification = new DelayedNotification();
        var options = new PublishOptions { ExecutionMode = ExecutionMode.FireAndForget };

        // Act
        var watch = ValueStopwatch.StartNew();
        var result = await notifier.PublishAsync(notification, options);
        var elapsedMs = watch.GetElapsedMilliseconds();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        elapsedMs.ShouldBeLessThan(100); // Should return immediately, not wait for handler completion
    }

    /// <summary>
    /// Tests that a behavior can modify the notification result and the modification propagates through the pipeline.
    /// </summary>
    [Fact]
    public async Task PublishAsync_BehaviorAddsMessage_PropagatesChanges()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(MessageAddingBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new EmailSentNotification();

        // Act
        var result = await notifier.PublishAsync(notification);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Messages.ShouldContain("Behavior message");
    }

    /// <summary>
    /// Tests that an exception thrown by a behavior is handled correctly based on HandleExceptionsAsResultError.
    /// </summary>
    [Fact]
    public async Task PublishAsync_BehaviorThrowsException_HandlesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(FailingBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new EmailSentNotification();

        // Test with HandleExceptionsAsResultError = true
        var optionsTrue = new PublishOptions { HandleExceptionsAsResultError = true };
        var result = await notifier.PublishAsync(notification, optionsTrue);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldBeOfType<ExceptionError>();
        var error = (ExceptionError)result.Errors[0];
        error.Exception.Message.ShouldBe("Behavior failure");

        // Test with HandleExceptionsAsResultError = false
        var optionsFalse = new PublishOptions { HandleExceptionsAsResultError = false };
        var exception = await Should.ThrowAsync<Exception>(async () =>
            await notifier.PublishAsync(notification, optionsFalse));
        exception.Message.ShouldBe("Behavior failure");
    }

    /// <summary>
    /// Tests that a RequestContext set in PublishOptions is passed to the handler and behaviors.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithRequestContext_PassesContextToHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var contextValues = new List<string>();
        services.AddScoped(sp => contextValues);
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(ContextCapturingBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new ContextCapturingNotification(contextValues);
        var options = new PublishOptions
        {
            Context = new RequestContext { Properties = { ["UserId"] = "123" } }
        };

        // Act
        var result = await notifier.PublishAsync(notification, options);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        contextValues.ShouldContain("123"); // Captured by behavior
        contextValues.ShouldContain("123-Handler"); // Captured by handler
    }

    /// <summary>
    /// Tests that the Progress callback in PublishOptions is invoked during notification processing.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithProgressReporting_InvokesProgressCallback()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(ProgressReportingBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new EmailSentNotification();
        var progressReports = new List<ProgressReport>();
        var options = new PublishOptions
        {
            Progress = new Progress<ProgressReport>(r => progressReports.Add(r))
        };

        // Act
        var result = await notifier.PublishAsync(notification, options);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        progressReports.ShouldNotBeEmpty();
        progressReports.SelectMany(r => r.Messages).ShouldContain("Progress: 50%");
    }

    /// <summary>
    /// Tests that GetRegistrationInformation returns an empty behavior list when no behaviors are registered.
    /// </summary>
    [Fact]
    public void GetRegistrationInformation_EmptyBehaviors_ReturnsEmptyList()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers(); // No behaviors registered
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();

        // Act
        var info = notifier.GetRegistrationInformation();

        // Assert
        info.HandlerMappings.ShouldNotBeNull();
        info.HandlerMappings.ShouldContainKey(nameof(EmailSentNotification));
        info.HandlerMappings[nameof(EmailSentNotification)].ShouldContain(nameof(EmailSentNotificationHandler));
        info.BehaviorTypes.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that RetryBehavior retries the specified number of times on failure before succeeding.
    /// </summary>
    [Fact]
    public async Task RetryBehavior_RetriesOnFailure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new NotifierBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(RetryPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new RetryTestNotification();
        var options = new PublishOptions { ExecutionMode = ExecutionMode.Sequential };

        // Act
        var result = await notifier.PublishAsync(notification, options);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        RetryTestNotificationHandler.RetryAttempts.ShouldBe(2); // Should retry twice before succeeding
    }

    /// <summary>
    /// Tests that TimeoutBehavior fails if the handler execution exceeds the timeout duration.
    /// </summary>
    [Fact]
    public async Task TimeoutBehavior_FailsOnTimeout()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new NotifierBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(TimeoutPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new TimeoutTestNotification();

        // Act
        var result = await notifier.PublishAsync(notification);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldBeOfType<ExceptionError>();
        var error = (ExceptionError)result.Errors[0];
        error.Exception.ShouldBeOfType<TimeoutRejectedException>();
    }

    /// <summary>
    /// Tests that ChaosBehavior injects chaos (exceptions) when configured.
    /// </summary>
    [Fact]
    public async Task ChaosBehavior_InjectsChaos()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(ChaosPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new ChaosTestNotification();

        // Act
        var result = await notifier.PublishAsync(notification);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldBeOfType<ExceptionError>();
        var error = (ExceptionError)result.Errors[0];
        error.Exception.ShouldBeOfType<ChaosException>();
        error.Message.ShouldContain("Chaos injection triggered");
    }

    /// <summary>
    /// Tests that ValidationBehavior validates the notification and returns FluentValidationError on failure.
    /// </summary>
    [Fact]
    public async Task ValidationBehavior_FailsValidation_ReturnsFluentValidationError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(ValidationBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new InvalidEmailNotification { EmailAddress = "" }; // Invalid email (empty)

        // Act
        var result = await notifier.PublishAsync(notification);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldBeOfType<FluentValidationError>();
        var error = (FluentValidationError)result.Errors[0];
        error.Errors.ShouldNotBeEmpty();
        error.Errors[0].ErrorMessage.ShouldBe("Email cannot be empty.");
    }

    /// <summary>
    /// Tests that ValidationBehavior allows the handler to execute when validation passes.
    /// </summary>
    [Fact]
    public async Task ValidationBehavior_PassesValidation_ProceedsToHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(ValidationBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var notifier = serviceProvider.GetService<INotifier>();
        var notification = new EmailSentNotification { EmailAddress = "valid@example.com" }; // Valid email

        // Act
        var result = await notifier.PublishAsync(notification);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }
}

/// <summary>
/// A sample notification that causes the handler to fail with an exception.
/// </summary>
public class FailingNotification : NotificationBase;

/// <summary>
/// A sample notification handler that throws an exception.
/// </summary>
public class FailingNotificationHandler : NotificationHandlerBase<FailingNotification>
{
    protected override Task<Result> HandleAsync(FailingNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        throw new Exception("Test failure");
    }
}

/// <summary>
/// A sample notification that delays execution to test cancellation.
/// </summary>
public class DelayedNotification : NotificationBase;

/// <summary>
/// A sample notification handler that delays execution.
/// </summary>
public class DelayedNotificationHandler : NotificationHandlerBase<DelayedNotification>
{
    protected override async Task<Result> HandleAsync(DelayedNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// A behavior that adds a message to the result.
/// </summary>
public class MessageAddingBehavior<TNotification, TResponse> : IPipelineBehavior<TNotification, TResponse>
    where TNotification : class, INotification
    where TResponse : IResult
{
    public async Task<TResponse> HandleAsync(TNotification notification, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        var result = await next();
        return (TResponse)(object)Result.Success("Behavior message");
    }

    public bool IsHandlerSpecific() => false;
}

/// <summary>
/// A notification that captures the RequestContext for testing.
/// </summary>
public class ContextCapturingNotification : NotificationBase
{
    private readonly List<string> capturedValues;

    public ContextCapturingNotification(List<string> capturedValues) => this.capturedValues = capturedValues;
}

/// <summary>
/// A handler that captures the RequestContext and appends to the captured values.
/// </summary>
public class ContextCapturingNotificationHandler : NotificationHandlerBase<ContextCapturingNotification>
{
    private readonly List<string> capturedValues;

    public ContextCapturingNotificationHandler(List<string> capturedValues) => this.capturedValues = capturedValues;

    protected override Task<Result> HandleAsync(ContextCapturingNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        if (options?.Context?.Properties.TryGetValue("UserId", out var userId) == true)
        {
            this.capturedValues.Add($"{userId}-Handler");
        }
        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// A notification to test retry behavior.
/// </summary>
public class RetryTestNotification : NotificationBase;

/// <summary>
/// A handler that fails twice before succeeding to test retry behavior.
/// </summary>
[HandlerRetry(2, 100)] // Retry twice with 100ms delay
public class RetryTestNotificationHandler : NotificationHandlerBase<RetryTestNotification>
{
    public static int RetryAttempts { get; private set; }

    private static int attempts = 0;

    protected override async Task<Result> HandleAsync(RetryTestNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);
        attempts++;
        if (attempts <= 2) // Fail on first two attempts
        {
            RetryAttempts++;
            throw new Exception("Simulated failure");
        }
        attempts = 0; // Reset for next test
        return Result.Success();
    }
}

/// <summary>
/// A notification to test timeout behavior.
/// </summary>
public class TimeoutTestNotification : NotificationBase;

/// <summary>
/// A handler that takes too long to complete to test timeout behavior.
/// </summary>
[HandlerTimeout(100)] // Timeout after 100ms
public class TimeoutTestNotificationHandler : NotificationHandlerBase<TimeoutTestNotification>
{
    protected override async Task<Result> HandleAsync(TimeoutTestNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(500, cancellationToken); // Delay longer than the timeout
        return Result.Success();
    }
}

/// <summary>
/// A notification to test chaos behavior.
/// </summary>
public class ChaosTestNotification : NotificationBase;

/// <summary>
/// A handler to test chaos behavior with a chaos policy.
/// </summary>
[HandlerChaos(1.0)] // 100% injection rate for testing
public class ChaosTestNotificationHandler : NotificationHandlerBase<ChaosTestNotification>
{
    protected override Task<Result> HandleAsync(ChaosTestNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success());
    }
}