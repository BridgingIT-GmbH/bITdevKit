// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System;
using System.Linq;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using IResult = Microsoft.AspNetCore.Http.IResult;

public class ResultMapErrorHandlerRegistryTests
{
    private readonly ILogger<ResultMapErrorHandlerRegistryTests> logger;

    public ResultMapErrorHandlerRegistryTests()
    {
        this.logger = NullLogger<ResultMapErrorHandlerRegistryTests>.Instance;
        ResultMapErrorHandlerRegistry.ClearHandlers(); // Ensure clean slate
    }

    [Fact]
    public void RegisterHandler_ValidHandler_RegistersSuccessfully()
    {
        // Arrange
        Func<ILogger, Result, IResult> handler = (logger, result) => TypedResults.Ok();

        // Act
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>(handler);

        // Assert
        ResultMapErrorHandlerRegistry.HasHandlerFor<ValidationError>().ShouldBeTrue();
        ResultMapErrorHandlerRegistry.GetRegisteredErrorTypes().ShouldContain(typeof(ValidationError));
        ResultMapErrorHandlerRegistry.GetRegisteredErrorTypes().Count().ShouldBe(1);

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void RegisterHandler_NullHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>(null))
            .ParamName.ShouldBe("handler");

        // Verify no change
        ResultMapErrorHandlerRegistry.HasHandlerFor<ValidationError>().ShouldBeFalse();
    }

    [Fact]
    public void RegisterHandler_MultipleHandlers_OverridesPrevious()
    {
        // Arrange
        Func<ILogger, Result, IResult> handler1 = (logger, result) => TypedResults.Ok();
        Func<ILogger, Result, IResult> handler2 = (logger, result) => TypedResults.NotFound();
        var result = Result.Failure().WithError(new ValidationError("Test"));

        // Act
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>(handler1);
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>(handler2);
        ResultMapErrorHandlerRegistry.TryExecuteCustomHandler(result, this.logger, out var executedResult);

        // Assert
        ResultMapErrorHandlerRegistry.HasHandlerFor<ValidationError>().ShouldBeTrue();
        ResultMapErrorHandlerRegistry.GetRegisteredErrorTypes().Count().ShouldBe(1);
        executedResult.ShouldBeOfType<NotFound>(); // handler2 overrides handler1

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void RemoveHandler_RegisteredHandler_RemovesSuccessfully()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => TypedResults.Ok());

        // Act
        var removed = ResultMapErrorHandlerRegistry.RemoveHandler<ValidationError>();

        // Assert
        removed.ShouldBeTrue();
        ResultMapErrorHandlerRegistry.HasHandlerFor<ValidationError>().ShouldBeFalse();
        ResultMapErrorHandlerRegistry.GetRegisteredErrorTypes().ShouldBeEmpty();

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void RemoveHandler_UnregisteredHandler_ReturnsFalse()
    {
        // Act
        var removed = ResultMapErrorHandlerRegistry.RemoveHandler<ValidationError>();

        // Assert
        removed.ShouldBeFalse();
        ResultMapErrorHandlerRegistry.HasHandlerFor<ValidationError>().ShouldBeFalse();
    }

    [Fact]
    public void ClearHandlers_MultipleHandlers_ClearsAll()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => TypedResults.Ok());
        ResultMapErrorHandlerRegistry.RegisterHandler<UnauthorizedError>((logger, r) => TypedResults.Unauthorized());

        // Act
        ResultMapErrorHandlerRegistry.ClearHandlers();

        // Assert
        ResultMapErrorHandlerRegistry.HasHandlerFor<ValidationError>().ShouldBeFalse();
        ResultMapErrorHandlerRegistry.HasHandlerFor<UnauthorizedError>().ShouldBeFalse();
        ResultMapErrorHandlerRegistry.GetRegisteredErrorTypes().ShouldBeEmpty();
    }

    [Fact]
    public void HasHandlerFor_UnregisteredHandler_ReturnsFalse()
    {
        // Assert
        ResultMapErrorHandlerRegistry.HasHandlerFor<ValidationError>().ShouldBeFalse();
    }

    [Fact]
    public void GetRegisteredErrorTypes_EmptyRegistry_ReturnsEmpty()
    {
        // Act
        var types = ResultMapErrorHandlerRegistry.GetRegisteredErrorTypes();

        // Assert
        types.ShouldBeEmpty();
    }

    [Fact]
    public void TryExecuteCustomHandler_RegisteredHandler_ExecutesAndReturnsTrue()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => new CustomHttpResult("Custom error"));
        var result = Result.Failure().WithError(new ValidationError("Invalid data"));

        // Act
        var executed = ResultMapErrorHandlerRegistry.TryExecuteCustomHandler(result, this.logger, out var customResult);

        // Assert
        executed.ShouldBeTrue();
        customResult.ShouldBeOfType<CustomHttpResult>();

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void TryExecuteCustomHandler_UnregisteredError_ReturnsFalse()
    {
        // Arrange
        var result = Result.Failure().WithError(new NotFoundError());

        // Act
        var executed = ResultMapErrorHandlerRegistry.TryExecuteCustomHandler(result, this.logger, out var customResult);

        // Assert
        executed.ShouldBeFalse();
        customResult.ShouldBeNull();
    }

    [Fact]
    public void TryExecuteCustomHandler_SuccessResult_ReturnsFalse()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => TypedResults.Ok());
        var result = Result.Success();

        // Act
        var executed = ResultMapErrorHandlerRegistry.TryExecuteCustomHandler(result, this.logger, out var customResult);

        // Assert
        executed.ShouldBeFalse();
        customResult.ShouldBeNull();

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void TryExecuteCustomHandler_MultipleErrors_FirstMatchingHandlerExecutes()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => new CustomHttpResult("Validation error"));
        ResultMapErrorHandlerRegistry.RegisterHandler<UnauthorizedError>((logger, r) => TypedResults.Unauthorized());
        var result = Result.Failure()
            .WithError(new ValidationError("Invalid"))
            .WithError(new UnauthorizedError("Unauthorized"));

        // Act
        var executed = ResultMapErrorHandlerRegistry.TryExecuteCustomHandler(result, this.logger, out var customResult);

        // Assert
        executed.ShouldBeTrue();
        customResult.ShouldBeOfType<CustomHttpResult>(); // First error (ValidationError) handler executes

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }
}