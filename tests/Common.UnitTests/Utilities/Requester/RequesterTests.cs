// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;
using System.Collections.Generic;
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
/// Unit tests for the <see cref="Requester"/> class.
/// </summary>
public class RequesterTests
{
    private readonly IServiceProvider serviceProvider;

    public RequesterTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(TestBehavior<,>))
            .WithBehavior(typeof(AnotherTestBehavior<,>));
        this.serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Tests that a generic request with valid data is successfully processed.
    /// </summary>
    [Fact]
    public void Requester_NoHandlers_CanResolve()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var requester = serviceProvider.GetService<IRequester>();

        // Assert
        requester.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that SendAsync successfully processes a request and returns a result.
    /// </summary>
    [Fact]
    public async Task SendAsync_SuccessfulRequest_ReturnsSuccessResult()
    {
        // Arrange
        var requester = this.serviceProvider.GetService<IRequester>();
        var request = new MyTestRequest();

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Test");
    }

    /// <summary>
    /// Tests that SendAsync applies behaviors in the correct order.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithBehaviors_AppliesInCorrectOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(OrderedBehavior<,>))
            .WithBehavior(typeof(AnotherOrderedBehavior<,>));
        var behaviorOrder = new List<string>();
        services.AddScoped(sp => behaviorOrder);
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new MyTestRequest();

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        behaviorOrder.ShouldBe(new[] { typeof(OrderedBehavior<MyTestRequest, IResult<string>>).PrettyName(), typeof(AnotherOrderedBehavior<MyTestRequest, IResult<string>>).PrettyName() });
    }

    /// <summary>
    /// Tests that SendAsync catches exceptions and returns a failed result when HandleExceptionsAsResultError is true.
    /// </summary>
    [Fact]
    public async Task SendAsync_HandlesExceptionsAsResultError_WhenFlagIsTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .AddHandlers();
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new FailingRequest();
        var options = new SendOptions { HandleExceptionsAsResultError = true };

        // Act
        var result = await requester.SendAsync(request, options);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldBeOfType<ExceptionError>();
        var error = (ExceptionError)result.Errors[0];
        error.Exception.Message.ShouldBe("Test failure");
    }

    /// <summary>
    /// Tests that SendAsync throws exceptions when HandleExceptionsAsResultError is false.
    /// </summary>
    [Fact]
    public async Task SendAsync_ThrowsExceptions_WhenFlagIsFalse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .AddHandlers();
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new FailingRequest();
        var options = new SendOptions { HandleExceptionsAsResultError = false };

        // Act
        var exception = await Should.ThrowAsync<Exception>(async () =>
            await requester.SendAsync(request, options));

        // Assert
        exception.Message.ShouldBe("Test failure");
    }

    /// <summary>
    /// Tests that SendAsync throws an ArgumentNullException for a null request.
    /// </summary>
    [Fact]
    public async Task SendAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var requester = this.serviceProvider.GetService<IRequester>();
        MyTestRequest request = null; // Simulate a null request

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await requester.SendAsync(request));
    }

    /// <summary>
    /// Tests that SendAsync throws a RequesterException when no handler is found.
    /// </summary>
    [Fact]
    public async Task SendAsync_NoHandlerFound_ThrowsRequesterException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .AddHandlers();
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new AnotherTestRequest(); // No handler registered for this request type

        // Act & Assert
        var exception = await Should.ThrowAsync<RequesterException>(async () =>
            await requester.SendAsync(request));
        exception.Message.ShouldBe("No handler found for request type AnotherTestRequest");
    }

    /// <summary>
    /// Tests that SendAsync respects cancellation tokens.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithCancellationToken_CancelsOperation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .AddHandlers();
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new DelayedRequest();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Should.ThrowAsync<TaskCanceledException>(async () =>
            await requester.SendAsync(request, null, cts.Token));
    }

    /// <summary>
    /// Tests that GetRegistrationInformation returns correct handler mappings and behavior types.
    /// </summary>
    [Fact]
    public void GetRegistrationInformation_ReturnsCorrectInformation()
    {
        // Arrange
        var requester = this.serviceProvider.GetService<IRequester>();

        // Act
        var info = requester.GetRegistrationInformation();

        // Assert
        info.HandlerMappings.ShouldNotBeNull();
        info.HandlerMappings.ShouldContainKey(nameof(MyTestRequest));
        info.HandlerMappings[nameof(MyTestRequest)].ShouldContain(nameof(MyTestRequestHandler));

        info.BehaviorTypes.ShouldNotBeNull();
        info.BehaviorTypes.ShouldBe(["TestBehavior<TRequest,TResponse>", "AnotherTestBehavior<TRequest,TResponse>"]);
        //info.BehaviorTypes.ShouldBe(new[] { typeof(TestBehavior<TRequest, TResponse>).PrettyName(), typeof(AnotherTestBehavior<TRequest, TResponse>).PrettyName() });
    }

    /// <summary>
    /// Tests that SendAsync processes multiple requests independently without interference.
    /// </summary>
    [Fact]
    public async Task SendAsync_MultipleRequests_ProcessesIndependently()
    {
        // Arrange
        var requester = this.serviceProvider.GetService<IRequester>();
        var request1 = new MyTestRequest();
        var request2 = new MyTestRequest();

        // Act
        var result1 = await requester.SendAsync(request1);
        var result2 = await requester.SendAsync(request2);

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result1.Value.ShouldBe("Test");
        result2.IsSuccess.ShouldBeTrue();
        result2.Value.ShouldBe("Test");
        request1.RequestId.ShouldNotBe(request2.RequestId); // Ensure requests are independent
    }

    /// <summary>
    /// Tests that SendAsync processes multiple requests concurrently without interference.
    /// </summary>
    [Fact]
    public async Task SendAsync_ConcurrentRequests_ProcessesCorrectly()
    {
        // Arrange
        var requester = this.serviceProvider.GetService<IRequester>();
        var requests = new List<MyTestRequest>
        {
            new(),
            new(),
            new()
        };

        // Act
        var tasks = requests.Select(request => requester.SendAsync(request)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Length.ShouldBe(3);
        foreach (var result in results)
        {
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("Test");
        }

        // Verify that each request has a unique RequestId
        var requestIds = requests.Select(r => r.RequestId).Distinct().ToList();
        requestIds.Count.ShouldBe(3); // Ensure all RequestIds are unique
    }

    /// <summary>
    /// Tests that a behavior can modify the request and the modification propagates through the pipeline.
    /// </summary>
    [Fact]
    public async Task SendAsync_BehaviorModifiesRequest_PropagatesChanges()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(ModifyingBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new MyTestRequest();

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Test:Modified");
    }

    /// <summary>
    /// Tests that an exception thrown by a behavior is handled correctly based on HandleExceptionsAsResultError.
    /// </summary>
    [Fact]
    public async Task SendAsync_BehaviorThrowsException_HandlesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(FailingBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new MyTestRequest();

        // Test with HandleExceptionsAsResultError = true
        var optionsTrue = new SendOptions { HandleExceptionsAsResultError = true };
        var result = await requester.SendAsync(request, optionsTrue);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldBeOfType<ExceptionError>();
        var error = (ExceptionError)result.Errors[0];
        error.Exception.Message.ShouldBe("Behavior failure");

        // Test with HandleExceptionsAsResultError = false
        var optionsFalse = new SendOptions { HandleExceptionsAsResultError = false };
        var exception = await Should.ThrowAsync<Exception>(async () =>
            await requester.SendAsync(request, optionsFalse));
        exception.Message.ShouldBe("Behavior failure");
    }

    /// <summary>
    /// Tests that a RequestContext set in SendOptions is passed to the handler and behaviors.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithRequestContext_PassesContextToHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var contextValues = new List<string>();
        var builder = new RequesterBuilder(services);
        services.AddScoped(sp => contextValues);
        builder.AddHandlers()
               .WithBehavior(typeof(ContextCapturingBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new ContextCapturingRequest(contextValues);
        var options = new SendOptions
        {
            Context = new RequestContext { Properties = { ["UserId"] = "123" } }
        };

        // Act
        var result = await requester.SendAsync(request, options);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        contextValues.ShouldContain("123"); // Captured by behavior
        contextValues.ShouldContain("123-Handler"); // Captured by handler
    }

    /// <summary>
    /// Tests that the Progress callback in SendOptions is invoked during request processing.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithProgressReporting_InvokesProgressCallback()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(ProgressReportingBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new MyTestRequest();
        var progressReports = new List<ProgressReport>();
        var options = new SendOptions
        {
            Progress = new Progress<ProgressReport>(r => progressReports.Add(r))
        };

        // Act
        var result = await requester.SendAsync(request, options);
        await Task.Delay(100); // Allow time for progress reporting

        // Assert
        result.ShouldBeSuccess();
        progressReports.ShouldNotBeEmpty();
        progressReports.SelectMany(r => r.Messages).Any(m => m == "Progress: 50%");
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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers(); // No behaviors registered
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();

        // Act
        var info = requester.GetRegistrationInformation();

        // Assert
        info.HandlerMappings.ShouldNotBeNull();
        info.HandlerMappings.ShouldContainKey(nameof(MyTestRequest));
        info.HandlerMappings[nameof(MyTestRequest)].ShouldContain(nameof(MyTestRequestHandler));
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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(RetryPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new RetryTestRequest();

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Success");
        RetryTestRequestHandler.RetryAttempts.ShouldBe(2); // Should retry twice before succeeding
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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(TimeoutPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new TimeoutTestRequest();

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldBeOfType<ExceptionError>();
        var error = (ExceptionError)result.Errors[0];
        error.Exception.ShouldBeOfType<TimeoutRejectedException>();
        //error.Message.ShouldContain("Timeout after");
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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(ChaosPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new ChaosTestRequest();

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldBeOfType<ExceptionError>();
        var error = (ExceptionError)result.Errors[0];
        error.Exception.ShouldBeOfType<ChaosException>();
        error.Message.ShouldContain("Chaos injection triggered");
    }

    /// <summary>
    /// Tests that SendAsync handles a command returning Result<Unit> correctly with retry behavior.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithUnitResultAndRetry_SucceedsAfterRetries()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(RetryPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new SendNotificationCommand { Message = "Test Notification" };

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(Unit.Value);
        SendNotificationCommandHandler.RetryAttempts.ShouldBe(2); // Should retry twice before succeeding
    }

    /// <summary>
    /// Tests that ValidationBehavior validates the request and returns FluentValidationError on failure.
    /// </summary>
    [Fact]
    public async Task ValidationBehavior_FailsValidation_ReturnsFluentValidationError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(ValidationPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new CreateCustomerCommand { Email = "" }; // Invalid email (empty)

        // Act
        var result = await requester.SendAsync(request);

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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers()
               .WithBehavior(typeof(ValidationPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new CreateCustomerCommand { Email = "valid@example.com" }; // Valid email

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Customer created");
    }

    /// <summary>
    /// Tests that a generic request with valid data is successfully processed.
    /// </summary>
    [Fact]
    public async Task GenericRequest_SuccessfulProcessing_Succeeds()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            //.AddHandlers()
            .AddHandler<ProcessDataRequest<UserData>, string, GenericDataProcessor<UserData>>() // add the generic handler
            .WithBehavior(typeof(ValidationPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var handler = serviceProvider.GetService<IRequestHandler<ProcessDataRequest<UserData>, string>>(); // test if handler registered?
        var requester = serviceProvider.GetService<IRequester>();
        var data = new UserData { UserId = "user123", Name = "John Doe" };
        var request = new ProcessDataRequest<UserData> { Data = data };

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Processed: user123 (UserData)");
    }

    [Fact]
    public async Task GenericRequest2_SuccessfulProcessing_Succeeds()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            //.AddHandlers()
            .AddGenericHandlers(
                genericHandlerType: typeof(GenericDataProcessor<>),
                genericRequestType: typeof(ProcessDataRequest<>),
                typeArguments: [typeof(UserData)])
            .WithBehavior(typeof(ValidationPipelineBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var handler = serviceProvider.GetService<IRequestHandler<ProcessDataRequest<UserData>, string>>(); // test if handler registered?
        var requester = serviceProvider.GetService<IRequester>();
        var data = new UserData { UserId = "user123", Name = "John Doe" };
        var request = new ProcessDataRequest<UserData> { Data = data };

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Processed: user123 (UserData)");
    }
}

/// <summary>
/// A sample request that causes the handler to fail with an exception.
/// </summary>
public class FailingRequest : RequestBase<string>;

/// <summary>
/// A sample request handler that throws an exception.
/// </summary>
public class FailingRequestHandler : IRequestHandler<FailingRequest, string>
{
    public Task<Result<string>> HandleAsync(FailingRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        throw new Exception("Test failure");
    }
}

/// <summary>
/// A sample request that delays execution to test cancellation.
/// </summary>
public class DelayedRequest : RequestBase<string>;

/// <summary>
/// A sample request handler that delays execution.
/// </summary>
public class DelayedRequestHandler : IRequestHandler<DelayedRequest, string>
{
    public async Task<Result<string>> HandleAsync(DelayedRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);
        return Result<string>.Success("Delayed");
    }
}

/// <summary>
/// A sample behavior that records its execution order.
/// </summary>
public class OrderedBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    private readonly List<string> orderList;

    public OrderedBehavior(List<string> orderList) => this.orderList = orderList;

    public async Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        this.orderList.Add(this.GetType().PrettyName());
        return await next();
    }

    public bool IsHandlerSpecific() => false;
}

/// <summary>
/// Another sample behavior that records its execution order.
/// </summary>
public class AnotherOrderedBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    private readonly List<string> orderList;

    public AnotherOrderedBehavior(List<string> orderList) => this.orderList = orderList;

    public async Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        this.orderList.Add(this.GetType().PrettyName());
        return await next();
    }

    public bool IsHandlerSpecific() => false;
}

/// <summary>
/// A behavior that modifies the request by adding metadata to its result value.
/// </summary>
public class ModifyingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    public async Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        var result = await next();
        if (result.IsSuccess)
        {
            if (result is Result<string> stringResult) // Assuming TValue is string for MyTestRequest
            {
                return (TResponse)(object)Result<string>.Success($"{stringResult.Value}:Modified");
            }
            throw new InvalidOperationException("Result type is not compatible with expected TValue.");
        }
        return result;
    }

    public bool IsHandlerSpecific() => false;
}

/// <summary>
/// A behavior that reports progress via SendOptions.
/// </summary>
public class ProgressReportingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    public async Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        var sendOptions = options as SendOptions;
        var publishOptions = options as PublishOptions;
        sendOptions?.Progress?.Report(new ProgressReport("MyTestRequest", new[] { "Progress: 50%" }, 50.0));
        publishOptions?.Progress?.Report(new ProgressReport("MyTestRequest", new[] { "Progress: 50%" }, 50.0));
        return await next();
    }

    public bool IsHandlerSpecific() => false;
}

/// <summary>
/// A behavior that throws an exception to test exception handling.
/// </summary>
public class FailingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    public Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        throw new Exception("Behavior failure");
    }

    public bool IsHandlerSpecific() => false;
}

/// <summary>
/// A behavior that captures the RequestContext from SendOptions.
/// </summary>
public class ContextCapturingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    private readonly List<string> capturedValues;

    public ContextCapturingBehavior(List<string> capturedValues) => this.capturedValues = capturedValues;

    public async Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        var sendOptions = options as SendOptions;
        if (sendOptions?.Context?.Properties.TryGetValue("UserId", out var userId) == true)
        {
            this.capturedValues.Add(userId);
        }

        var publishOptions = options as PublishOptions;
        if (publishOptions?.Context?.Properties.TryGetValue("UserId", out var userId2) == true)
        {
            this.capturedValues.Add(userId2);
        }
        return await next();
    }

    public bool IsHandlerSpecific() => false;
}

/// <summary>
/// A request that captures the RequestContext for testing.
/// </summary>
public class ContextCapturingRequest : RequestBase<string>
{
    private readonly List<string> capturedValues;

    public ContextCapturingRequest(List<string> capturedValues) => this.capturedValues = capturedValues;
}

/// <summary>
/// A handler that captures the RequestContext and appends to the result.
/// </summary>
public class ContextCapturingRequestHandler : IRequestHandler<ContextCapturingRequest, string>
{
    private readonly List<string> capturedValues;

    public ContextCapturingRequestHandler(List<string> capturedValues) => this.capturedValues = capturedValues;

    public Task<Result<string>> HandleAsync(ContextCapturingRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        if (options?.Context?.Properties.TryGetValue("UserId", out var userId) == true)
        {
            this.capturedValues.Add($"{userId}-Handler");
        }
        return Task.FromResult(Result<string>.Success("Test"));
    }
}

/// <summary>
/// A request to test retry behavior.
/// </summary>
public class RetryTestRequest : RequestBase<string>;

/// <summary>
/// A handler that fails twice before succeeding to test retry behavior.
/// </summary>
[HandlerRetry(2, 100)] // Retry twice with 100ms delay
public class RetryTestRequestHandler : IRequestHandler<RetryTestRequest, string>
{
    public static int RetryAttempts { get; private set; }

    private static int attempts = 0;

    public async Task<Result<string>> HandleAsync(RetryTestRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);
        attempts++;
        if (attempts <= 2) // Fail on first two attempts
        {
            RetryAttempts++;
            throw new Exception("Simulated failure");
        }
        attempts = 0; // Reset for next test
        return Result<string>.Success("Success");
    }
}

/// <summary>
/// A request to test timeout behavior.
/// </summary>
public class TimeoutTestRequest : RequestBase<string>;

/// <summary>
/// A handler that takes too long to complete to test timeout behavior.
/// </summary>
[HandlerTimeout(100)] // Timeout after 100ms
public class TimeoutTestRequestHandler : IRequestHandler<TimeoutTestRequest, string>
{
    public async Task<Result<string>> HandleAsync(TimeoutTestRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(500, cancellationToken); // Delay longer than the timeout
        return Result<string>.Success("Should not reach here");
    }
}

/// <summary>
/// A request to test chaos behavior.
/// </summary>
public class ChaosTestRequest : RequestBase<string>;

/// <summary>
/// A handler to test chaos behavior with a chaos policy.
/// </summary>
[HandlerChaos(1.0)] // 100% injection rate for testing
public class ChaosTestRequestHandler : IRequestHandler<ChaosTestRequest, string>
{
    public Task<Result<string>> HandleAsync(ChaosTestRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<string>.Success("Should not reach here"));
    }
}

// Command to send a notification
public class SendNotificationCommand : RequestBase<Unit>
{
    public string Message { get; set; }
}

// Handler for the command with a retry policy
[HandlerRetry(2, 100)] // Retry twice with 100ms delay
public class SendNotificationCommandHandler : RequestHandlerBase<SendNotificationCommand, Unit>
{
    public static int RetryAttempts { get; private set; }

    private static int attempts = 0;

    protected override async Task<Result<Unit>> HandleAsync(SendNotificationCommand request, SendOptions options, CancellationToken cancellationToken)
    {
        attempts++;
        if (attempts <= 2) // Fail on first two attempts
        {
            RetryAttempts++;
            throw new Exception("Simulated failure");
        }
        attempts = 0; // Reset for next test
        await Task.Delay(50, cancellationToken); // Simulate async operation
        return Result<Unit>.Success(Unit.Value);
    }
}

/// <summary>
/// A command to send a notification with validation.
/// </summary>
public class CreateCustomerCommand : RequestBase<string>
{
    public string Email { get; set; }

    public class Validator : AbstractValidator<CreateCustomerCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email cannot be empty.")
                .EmailAddress().WithMessage("Invalid email format.");
        }
    }
}

/// <summary>
/// Handler for the CreateCustomerCommand.
/// </summary>
public class CreateCustomerCommandHandler : RequestHandlerBase<CreateCustomerCommand, string>
{
    protected override async Task<Result<string>> HandleAsync(CreateCustomerCommand request, SendOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken); // Simulate async operation
        return Result<string>.Success("Customer created");
    }
}

public interface IDataItem
{
    string GetIdentifier();
}

public class UserData : IDataItem
{
    public string UserId { get; set; }
    public string Name { get; set; }

    public string GetIdentifier() => this.UserId;
}

public class ProcessDataRequest<TData> : RequestBase<string>
    where TData : class, IDataItem
{
    public TData Data { get; set; }

    public class Validator : AbstractValidator<ProcessDataRequest<TData>>
    {
        public Validator()
        {
            RuleFor(x => x.Data).NotNull().WithMessage("Data cannot be null.");
            RuleFor(x => x.Data.GetIdentifier()).NotEmpty().WithMessage("Data identifier cannot be empty.");
        }
    }
}

[HandlerRetry(2, 100)] // Retry twice with 100ms delay
public class GenericDataProcessor<TData> : RequestHandlerBase<ProcessDataRequest<TData>, string>
    where TData : class, IDataItem
{
    protected override async Task<Result<string>> HandleAsync(ProcessDataRequest<TData> request, SendOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken); // Simulate async processing
        var identifier = request.Data.GetIdentifier();
        if (string.IsNullOrEmpty(identifier))
        {
            return Result<string>.Failure().WithMessage("Data identifier is invalid.");
        }

        var result = $"Processed: {identifier} ({typeof(TData).Name})";
        return Result<string>.Success(result);
    }
}