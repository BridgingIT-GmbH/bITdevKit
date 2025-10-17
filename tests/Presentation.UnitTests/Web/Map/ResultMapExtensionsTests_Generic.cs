// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using Bogus;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.Linq;
using Xunit;

public class ResultMapExtensionsGenericTests
{
    private readonly ILogger logger;
    private readonly Faker<PersonStub> personFaker;

    public ResultMapExtensionsGenericTests()
    {
        this.logger = NullLogger.Instance;

        this.personFaker = new Faker<PersonStub>()
            .CustomInstantiator(f => new PersonStub
            {
                Id = Guid.NewGuid(),
                FirstName = f.Name.FirstName(),
                LastName = f.Name.LastName(),
                Age = f.Random.Int(18, 80)
            });
    }

    private PersonStub CreatePerson() => this.personFaker.Generate();

    [Fact]
    public void MapHttpOk_Success_ReturnsOkWithValue()
    {
        // Arrange
        var person = this.CreatePerson();
        var result = Result<PersonStub>.Success(person);

        // Act
        var response = result.MapHttpOk(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PersonStub>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<Ok<PersonStub>>();
        var okResult = (Ok<PersonStub>)innerResult;
        okResult.Value.ShouldBe(person);
    }

    [Fact]
    public void MapHttpOk_NotFound_ReturnsNotFound()
    {
        // Arrange
        var result = Result<PersonStub>.Failure().WithError(new NotFoundError());

        // Act
        var response = result.MapHttpOk(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PersonStub>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<NotFound>();
    }

    [Fact]
    public void MapHttpCreated_Success_ReturnsCreated()
    {
        // Arrange
        var person = this.CreatePerson();
        var result = Result<PersonStub>.Success(person);
        var uri = $"/api/people/{person.Id}";

        // Act
        var response = result.MapHttpCreated(uri, this.logger);

        // Assert
        response.ShouldBeOfType<Results<Created<PersonStub>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<Created<PersonStub>>();
        var createdResult = (Created<PersonStub>)innerResult;
        createdResult.Location.ShouldBe(uri);
        createdResult.Value.ShouldBe(person);
    }

    [Fact]
    public void MapHttpCreated_WithUriFactory_Success_ReturnsCreated()
    {
        // Arrange
        var person = this.CreatePerson();
        var result = Result<PersonStub>.Success(person);
        Func<PersonStub, string> uriFactory = p => $"/api/people/{p.Id}";

        // Act
        var response = result.MapHttpCreated(uriFactory, this.logger);

        // Assert
        response.ShouldBeOfType<Results<Created<PersonStub>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<Created<PersonStub>>();
        var createdResult = (Created<PersonStub>)innerResult;
        createdResult.Location.ShouldBe($"/api/people/{person.Id}");
        createdResult.Value.ShouldBe(person);
    }

    [Fact]
    public void MapHttpFile_Success_ReturnsFileContentResult()
    {
        // Arrange
        var fileContent = new FileContent(
            content: new byte[] { 1, 2, 3 },
            fileName: "test.txt",
            contentType: "text/plain");
        var result = Result<FileContent>.Success(fileContent);

        // Act
        var response = result.MapHttpFile(this.logger);

        // Assert
        response.ShouldBeOfType<Results<FileContentHttpResult, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<FileContentHttpResult>();
        var fileResult = (FileContentHttpResult)innerResult;
        fileResult.FileContents.ShouldBe(fileContent.Content);
        fileResult.FileDownloadName.ShouldBe(fileContent.FileName);
        fileResult.ContentType.ShouldBe(fileContent.ContentType);
    }

    [Fact]
    public void MapHttpOkAll_Success_ReturnsOkWithValue()
    {
        // Arrange
        var person = this.CreatePerson();
        var result = Result<PersonStub>.Success(person);

        // Act
        var response = result.MapHttpOkAll(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PersonStub>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<Ok<PersonStub>>();
        var okResult = (Ok<PersonStub>)innerResult;
        okResult.Value.ShouldBe(person);
    }

    [Fact]
    public void MapHttpAccepted_Success_ReturnsAcceptedWithValue()
    {
        // Arrange
        var person = this.CreatePerson();
        var result = Result<PersonStub>.Success(person);
        var location = "/api/status/789";

        // Act
        var response = result.MapHttpAccepted(location, this.logger);

        // Assert
        response.ShouldBeOfType<Results<Accepted<PersonStub>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<Accepted<PersonStub>>();
        var acceptedResult = (Accepted<PersonStub>)innerResult;
        acceptedResult.Location.ShouldBe(location);
        acceptedResult.Value.ShouldBe(person);
    }

    [Fact]
    public void MapHttpAccepted_WithLocationFactory_Success_ReturnsAcceptedWithValue()
    {
        // Arrange
        var person = this.CreatePerson();
        var result = Result<PersonStub>.Success(person);
        Func<PersonStub, string> locationFactory = p => $"/api/status/{p.Id}";

        // Act
        var response = result.MapHttpAccepted(locationFactory, this.logger);

        // Assert
        response.ShouldBeOfType<Results<Accepted<PersonStub>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<Accepted<PersonStub>>();
        var acceptedResult = (Accepted<PersonStub>)innerResult;
        acceptedResult.Location.ShouldBe($"/api/status/{person.Id}");
        acceptedResult.Value.ShouldBe(person);
    }

    [Fact]
    public void MapHttpFile_WithFileNameFactory_Success_ReturnsFileContentResult()
    {
        // Arrange
        var fileContent = new FileContent(
            content: new byte[] { 1, 2, 3 },
            fileName: "original.txt",
            contentType: "text/plain");
        var result = Result<FileContent>.Success(fileContent);
        Func<FileContent, string> fileNameFactory = fc => $"generated_{fc.FileName}";

        // Act
        var response = result.MapHttpFile(fileNameFactory, this.logger);

        // Assert
        response.ShouldBeOfType<Results<FileContentHttpResult, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<FileContentHttpResult>();
        var fileResult = (FileContentHttpResult)innerResult;
        fileResult.FileContents.ShouldBe(fileContent.Content);
        fileResult.FileDownloadName.ShouldBe("generated_original.txt");
        fileResult.ContentType.ShouldBe(fileContent.ContentType);
    }

    [Fact]
    public void MapHttp_WithSuccessFunc_Success_ReturnsCustomResult()
    {
        // Arrange
        var person = this.CreatePerson();
        var result = Result<PersonStub>.Success(person);
        Func<PersonStub, Ok<PersonStub>> successFunc = p => TypedResults.Ok(p);

        // Act
        var response = result.MapHttp<Ok<PersonStub>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult, PersonStub>(
            successFunc, this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PersonStub>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<Ok<PersonStub>>();
        var okResult = (Ok<PersonStub>)innerResult;
        okResult.Value.ShouldBe(person);
    }

    [Fact]
    public void MapHttpOk_WithMessages_Success_ReturnsOkWithValue()
    {
        // Arrange
        var person = this.CreatePerson();
        var result = Result<PersonStub>.Success(person)
            .WithMessage("Person retrieved successfully")
            .WithMessage("Operation completed");

        // Act
        var response = result.MapHttpOk(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PersonStub>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<Ok<PersonStub>>();
        var okResult = (Ok<PersonStub>)innerResult;
        okResult.Value.ShouldBe(person);
    }

    [Fact]
    public void MapOk_CustomValidationErrorHandler_ReturnsProblemWithCustomStatus()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => new CustomHttpResult($"Custom validation: {r.Errors.First().Message}"));
        var result = Result<PersonStub>.Failure().WithError(new ValidationError("Invalid person data"));

        // Act
        var response = result.MapHttpOk(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PersonStub>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(418); // CustomHttpResult status preserved
        problemResult.ProblemDetails.Detail.ShouldContain("A custom error handler was executed");
        problemResult.ProblemDetails.Extensions["customResultType"].ShouldBe("CustomHttpResult");

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void MapCreated_CustomValidationErrorHandler_ReturnsProblemWithCustomStatus()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => new CustomHttpResult($"Custom validation: {r.Errors.First().Message}"));
        var result = Result<PersonStub>.Failure().WithError(new ValidationError("Invalid person data"));

        // Act
        var response = result.MapHttpCreated("/api/people/1", this.logger);

        // Assert
        response.ShouldBeOfType<Results<Created<PersonStub>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(418); // CustomHttpResult status preserved
        problemResult.ProblemDetails.Detail.ShouldContain("A custom error handler was executed");
        problemResult.ProblemDetails.Extensions["customResultType"].ShouldBe("CustomHttpResult");

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void Map_GenericWithValue_CustomValidationErrorHandler_ReturnsTProblem()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<ValidationError>((logger, r) => new CustomHttpResult($"Custom validation: {r.Errors.First().Message}"));
        var result = Result<PersonStub>.Failure()
            .WithError(new ValidationError("Invalid person data"));

        // Act
        var response = result.MapHttp<Ok<PersonStub>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult, PersonStub>(person => TypedResults.Ok(person), this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PersonStub>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>(); // MapError<TProblem> returns ProblemHttpResult
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(500); // MapError defaults to 500, not 418
        problemResult.ProblemDetails.Detail.ShouldContain("Invalid person data");

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    [Fact]
    public void Map_GenericWithValue_ValidationError_ReturnsProblemWithValidationDetails()
    {
        // Arrange
        var result = Result<PersonStub>.Failure()
            .WithError(new ValidationError("Invalid person data"));

        // Act
        var response = result.MapHttpCreated("/api/people/1", this.logger);

        // Assert
        response.ShouldBeOfType<Results<Created<PersonStub>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(400);
        problemResult.ProblemDetails.Title.ShouldBe("Validation Error");

        // Validation errors are stored inside the anonymous 'data' extension under its 'errors' property.
        problemResult.ProblemDetails.Extensions.ContainsKey("data").ShouldBeTrue();
        var data = problemResult.ProblemDetails.Extensions["data"];
        data.ShouldNotBeNull();
        var errorsProperty = data.GetType().GetProperty("errors");
        errorsProperty.ShouldNotBeNull();
        var messages = (Dictionary<string, string[]>)errorsProperty.GetValue(data);
        messages.ShouldContainKey("validation");
        messages["validation"].ShouldContain("Invalid person data");
    }

    [Fact]
    public void Map_GenericWithValue_ValidationError2_ReturnsProblemWithValidationDetails()
    {
        // Arrange
        var result = Result<PersonStub>.Failure()
            .WithError(new ValidationError("Invalid firstname", "FirstName", "___"));

        // Act
        var response = result.MapHttpCreated("/api/people/1", this.logger);

        // Assert
        response.ShouldBeOfType<Results<Created<PersonStub>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = response.Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(400);
        problemResult.ProblemDetails.Title.ShouldBe("Validation Error");
        problemResult.ProblemDetails.Extensions.ContainsKey("data").ShouldBeTrue();
        var data = problemResult.ProblemDetails.Extensions["data"];
        data.ShouldNotBeNull();
        var errorsProperty = data.GetType().GetProperty("errors");
        errorsProperty.ShouldNotBeNull();
        var messages = (Dictionary<string, string[]>)errorsProperty.GetValue(data);
        messages.ShouldContainKey("FirstName");
        messages["FirstName"].ShouldContain("Invalid firstname");
    }
}
