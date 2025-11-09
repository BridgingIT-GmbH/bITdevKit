// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System;
using System.Linq;
using System.Collections.Generic;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

public class ResultMapExtensionsNonGenericTests
{
    private readonly ILogger logger;

    public ResultMapExtensionsNonGenericTests()
    {
        this.logger = NullLogger.Instance;
    }

    [Fact]
    public void MapHttpNoContent_Success_ReturnsNoContent()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var response = result.MapHttpNoContent(this.logger);

        // Assert
        response.ShouldBeOfType<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<NoContent>();
    }

    [Fact]
    public void MapHttpNoContent_Unauthorized_ReturnsUnauthorized()
    {
        // Arrange
        var result = Result.Failure().WithError(new UnauthorizedError());

        // Act
        var response = result.MapHttpNoContent(this.logger);

        // Assert
        response.ShouldBeOfType<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public void MapHttpOk_Success_ReturnsOk()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var response = result.MapHttpOk(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Ok>();
    }

    [Fact]
    public void MapHttpOk_NotFound_ReturnsNotFound()
    {
        // Arrange
        var result = Result.Failure().WithError(new NotFoundError());

        // Act
        var response = result.MapHttpOk(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<NotFound>();
    }

    [Fact]
    public void MapHttpAccepted_Success_ReturnsAccepted()
    {
        // Arrange
        var result = Result.Success();
        var location = "/api/status/123";

        // Act
        var response = result.MapHttpAccepted(location, this.logger);

        // Assert
        response.ShouldBeOfType<Results<Accepted, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Accepted, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Accepted>();
        var acceptedResult = (Accepted)innerResult;
        acceptedResult.Location.ShouldBe(location);
    }

    [Fact]
    public void MapHttpAccepted_NullLocation_ThrowsArgumentException()
    {
        // Arrange
        var result = Result.Success();

        // Act & Assert
        Should.Throw<ArgumentException>(() => result.MapHttpAccepted(null, this.logger));
    }

    [Fact]
    public void MapHttp_WithSuccessFunc_Success_ReturnsCustomResult()
    {
        // Arrange
        var result = Result.Success();
        Func<Created> successFunc = () => TypedResults.Created("/api/custom");

        // Act
        var response = result.MapHttp<Created, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>(
            successFunc, this.logger);

        // Assert
        response.ShouldBeOfType<Results<Created, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Created, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Created>();
        var createdResult = (Created)innerResult;
        createdResult.Location.ShouldBe("/api/custom");
    }

    [Fact]
    public void MapHttpNoContent_WithMessages_Success_ReturnsNoContent()
    {
        // Arrange
        var result = Result.Success()
            .WithMessage("Operation completed")
            .WithMessage("No data to return");

        // Act
        var response = result.MapHttpNoContent(this.logger);

        // Assert
        response.ShouldBeOfType<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<NoContent>();
    }

    [Fact]
    public void MapHttpNoContent_DefaultValidationError_ReturnsProblemWithStatus400AndValidationErrors()
    {
        // Arrange
        var result = Result.Failure().WithError(new ValidationError("Invalid input"));

        // Act
        var response = result.MapHttpNoContent(this.logger);

        // Assert
        response.ShouldBeOfType<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(400);
        problemResult.ProblemDetails.Title.ShouldBe("Validation Error");
        problemResult.ProblemDetails.Extensions.ContainsKey("data").ShouldBeTrue();
        var data = problemResult.ProblemDetails.Extensions["data"];
        var errorsProperty = data.GetType().GetProperty("errors");
        errorsProperty.ShouldNotBeNull();
        var errors = (Dictionary<string, string[]>)errorsProperty.GetValue(data);
        errors.ShouldContainKey("validation");
        errors["validation"].ShouldContain("Invalid input");
    }

    [Fact]
    public void MapNoContent_CustomValidationErrorHandler_ReturnsProblemWithCustomStatus()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => new CustomHttpResult($"Validation failed: {r.Errors.First().Message}"));
        var result = Result.Failure().WithError(new ValidationError("Invalid operation"));

        // Act
        var response = result.MapHttpNoContent(this.logger);

        // Assert
        response.ShouldBeOfType<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(418); // CustomHttpResult status preserved
        problemResult.ProblemDetails.Detail.ShouldContain("A custom error handler was executed");
        problemResult.ProblemDetails.Extensions["customResultType"].ShouldBe("CustomHttpResult");

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void Map_GenericNonGeneric_CustomValidationErrorHandler_ReturnsTProblem()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => new CustomHttpResult($"Validation failed: {r.Errors.First().Message}"));
        var result = Result.Failure().WithError(new ValidationError("Invalid operation"));

        // Act
        var response = result.MapHttp<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>(
            () => TypedResults.NoContent(),
            this.logger);

        // Assert
        response.ShouldBeOfType<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>(); // MapError<TProblem> returns ProblemHttpResult
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(500); // MapError defaults to 500, not 418
        problemResult.ProblemDetails.Detail.ShouldContain("Invalid operation");

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void MapHttpNoContent_RemoveHandler_RevertsToDefault()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => new CustomHttpResult("Custom validation"));
        ResultMapErrorHandlerRegistry.RemoveHandler<ValidationError>();
        var result = Result.Failure().WithError(new ValidationError("Invalid input"));

        // Act
        var response = result.MapHttpNoContent(this.logger);

        // Assert
        response.ShouldBeOfType<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(400);
        problemResult.ProblemDetails.Title.ShouldBe("Validation Error");
        problemResult.ProblemDetails.Extensions.ContainsKey("data").ShouldBeTrue();
        var data = problemResult.ProblemDetails.Extensions["data"];
        var errorsProperty = data.GetType().GetProperty("errors");
        errorsProperty.ShouldNotBeNull();
        var errors = (Dictionary<string, string[]>)errorsProperty.GetValue(data);
        errors.ShouldContainKey("validation");
        errors["validation"].ShouldContain("Invalid input");

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void TryExecuteCustomHandler_UnregisteredError_ReturnsFalse()
    {
        // Arrange
        var result = Result.Failure().WithError(new NotFoundError());

        // Act
        var handled = ResultMapErrorHandlerRegistry.TryExecuteCustomHandler(result, this.logger, out var customResult);

        // Assert
        handled.ShouldBeFalse();
        customResult.ShouldBeNull();
    }
}
