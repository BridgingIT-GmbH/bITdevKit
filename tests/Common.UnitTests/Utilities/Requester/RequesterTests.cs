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
public class OrderedBehavior<TRequest, TResponse>(List<string> orderList) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    public async Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        orderList.Add(this.GetType().Name);
        return await next();
    }
}

/// <summary>
/// Another sample behavior that records its execution order.
/// </summary>
public class AnotherOrderedBehavior<TRequest, TResponse>(List<string> orderList) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    public async Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        orderList.Add(this.GetType().Name);
        return await next();
    }
}