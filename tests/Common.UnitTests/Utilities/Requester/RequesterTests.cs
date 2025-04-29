// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using BridgingIT.DevKit.Application.Requester;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers(["^System\\..*"])
               .WithBehavior(typeof(TestBehavior<,>))
               .WithBehavior(typeof(AnotherTestBehavior<,>));
        this.serviceProvider = services.BuildServiceProvider();
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
        var behaviorOrder = new List<string>();
        var builder = new RequesterBuilder(services);
        builder.AddHandlers(["^System\\..*"])
               .WithBehavior(typeof(OrderedBehavior<,>))
               .WithBehavior(typeof(AnotherOrderedBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();
        var requester = serviceProvider.GetService<IRequester>();
        var request = new MyTestRequest();

        // Act
        var result = await requester.SendAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        behaviorOrder.ShouldBe(new[] { nameof(OrderedBehavior<MyTestRequest, IResult<string>>), nameof(AnotherOrderedBehavior<MyTestRequest, IResult<string>>) });
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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers(["^System\\..*"]);
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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers(["^System\\..*"]);
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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers(["^System\\..*"]);
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
        var builder = new RequesterBuilder(services);
        builder.AddHandlers(["^System\\..*"]);
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
        builder.AddHandlers(["^System\\..*"])
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
        builder.AddHandlers(["^System\\..*"])
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
        builder.AddHandlers(["^System\\..*"])
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
    /// Tests that GetRegistrationInformation returns an empty behavior list when no behaviors are registered.
    /// </summary>
    [Fact]
    public void GetRegistrationInformation_EmptyBehaviors_ReturnsEmptyList()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new RequesterBuilder(services);
        builder.AddHandlers(["^System\\..*"]); // No behaviors registered
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

    public OrderedBehavior(List<string> orderList)
    {
        this.orderList = orderList;
    }

    public async Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        this.orderList.Add(this.GetType().Name);
        return await next();
    }
}

/// <summary>
/// Another sample behavior that records its execution order.
/// </summary>
public class AnotherOrderedBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    private readonly List<string> orderList;

    public AnotherOrderedBehavior(List<string> orderList)
    {
        this.orderList = orderList;
    }

    public async Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        this.orderList.Add(this.GetType().Name);
        return await next();
    }
}

/// <summary>
/// A behavior that modifies the request by adding metadata to its result value.
/// </summary>
public class ModifyingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    public async Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
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
}

/// <summary>
/// A behavior that throws an exception to test exception handling.
/// </summary>
public class FailingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    public Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        throw new Exception("Behavior failure");
    }
}

/// <summary>
/// A behavior that captures the RequestContext from SendOptions.
/// </summary>
public class ContextCapturingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    private readonly List<string> capturedValues;

    public ContextCapturingBehavior(List<string> capturedValues)
    {
        this.capturedValues = capturedValues;
    }

    public async Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        var sendOptions = options as SendOptions;
        if (sendOptions?.Context?.Properties.TryGetValue("UserId", out var userId) == true)
        {
            this.capturedValues.Add(userId.ToString());
        }
        return await next();
    }
}

/// <summary>
/// A request that captures the RequestContext for testing.
/// </summary>
public class ContextCapturingRequest : RequestBase<string>
{
    private readonly List<string> capturedValues;

    public ContextCapturingRequest(List<string> capturedValues)
    {
        this.capturedValues = capturedValues;
    }
}

/// <summary>
/// A handler that captures the RequestContext and appends to the result.
/// </summary>
public class ContextCapturingRequestHandler : IRequestHandler<ContextCapturingRequest, string>
{
    private readonly List<string> capturedValues;

    public ContextCapturingRequestHandler(List<string> capturedValues)
    {
        this.capturedValues = capturedValues;
    }

    public Task<Result<string>> HandleAsync(ContextCapturingRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        if (options?.Context?.Properties.TryGetValue("UserId", out var userId) == true)
        {
            this.capturedValues.Add($"{userId}-Handler");
        }
        return Task.FromResult(Result<string>.Success("Test"));
    }
}