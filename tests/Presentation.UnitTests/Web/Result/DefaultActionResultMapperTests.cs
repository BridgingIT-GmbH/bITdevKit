// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using Microsoft.AspNetCore.Mvc;
using Presentation.Web;

[UnitTest("Presentation")]
public class DefaultActionResultMapperTests
{
    private readonly IResult successResult;
    private readonly IResult failureResult;

    public DefaultActionResultMapperTests()
    {
        this.successResult = Substitute.For<IResult>();
        this.successResult.IsSuccess.Returns(true);

        this.failureResult = Substitute.For<IResult>();
        this.failureResult.IsSuccess.Returns(false);
    }

    [Fact]
    public void Ok_WhenGivenSuccessResult_ReturnsOkObjectResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Ok(this.successResult, model);

        // Assert
        response.Result.ShouldBeOfType<OkObjectResult>();
        ((OkObjectResult)response.Result).StatusCode.ShouldBe(200);
        ((OkObjectResult)response.Result).Value.ShouldBe(model);
    }

    [Fact]
    public void Ok_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Ok(this.failureResult, model);

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(200);
    }

    [Fact]
    public void OkWithAction_WhenGivenSuccessResult_ReturnsOkObjectResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Ok(this.successResult, model);

        // Assert
        response.Result.ShouldBeOfType<OkObjectResult>();
        ((OkObjectResult)response.Result).StatusCode.ShouldBe(200);
        ((OkObjectResult)response.Result).Value.ShouldBe(model);
    }

    [Fact]
    public void OkWithAction_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Ok<PersonStub>(this.failureResult, m => m = PersonStub.Create(DateTime.UtcNow.Ticks));

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(200);
    }

    [Fact]
    public void Created_WhenGivenSuccessResultAndRouteName_ReturnsCreatedAtRouteResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();
        const string routeName = "test-route-name";

        // Act
        var response = sut.Created(this.successResult, model, routeName);

        // Assert
        response.Result.ShouldBeOfType<CreatedAtRouteResult>();
        ((CreatedAtRouteResult)response.Result).StatusCode.ShouldBe(201);
        ((CreatedAtRouteResult)response.Result).RouteName.ShouldBe(routeName);
        ((CreatedAtRouteResult)response.Result).Value.ShouldBe(model);
    }

    [Fact]
    public void Created_WhenGivenSuccessResultAndNoRouteName_ReturnsOkObjectResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Created(this.successResult, model);

        // Assert
        response.Result.ShouldBeOfType<OkObjectResult>();
        ((OkObjectResult)response.Result).StatusCode.ShouldBe(201);
        ((OkObjectResult)response.Result).Value.ShouldBe(model);
    }

    [Fact]
    public void Created_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Created(this.failureResult, model, "test-route-name");

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(200);
    }

    [Fact]
    public void CreatedWithAction_WhenGivenSuccessResultAndRouteName_ReturnsCreatedAtRouteResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();
        const string routeName = "test-route-name";

        // Act
        var response = sut.Created<PersonStub>(this.successResult, m => m = model, routeName);

        // Assert
        response.Result.ShouldBeOfType<CreatedAtRouteResult>();
        ((CreatedAtRouteResult)response.Result).StatusCode.ShouldBe(201);
        ((CreatedAtRouteResult)response.Result).RouteName.ShouldBe(routeName);
        ((CreatedAtRouteResult)response.Result).Value.ShouldNotBeNull();
    }

    [Fact]
    public void CreatedWithAction_WhenGivenSuccessResultAndNoRouteName_ReturnsOkObjectResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Created<PersonStub>(this.successResult, m => m = PersonStub.Create(DateTime.UtcNow.Ticks));

        // Assert
        response.Result.ShouldBeOfType<OkObjectResult>();
        ((OkObjectResult)response.Result).StatusCode.ShouldBe(201);
        ((OkObjectResult)response.Result).Value.ShouldNotBeNull();
    }

    [Fact]
    public void CreatedWithAction_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Created<PersonStub>(this.failureResult, m => m = PersonStub.Create(DateTime.UtcNow.Ticks), "test-route-name");

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(200);
    }

    [Fact]
    public void Updated_WhenGivenSuccessResultAndModel_ReturnsCreatedResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();
        const string routeName = "test-route-name";

        // Act
        var response = sut.Updated(this.successResult, model, routeName);

        // Assert
        response.Result.ShouldBeOfType<UpdatedAtRouteResult>();
        ((UpdatedAtRouteResult)response.Result).StatusCode.ShouldBe(200);
        ((UpdatedAtRouteResult)response.Result).RouteName.ShouldBe(routeName);
        ((UpdatedAtRouteResult)response.Result).Value.ShouldBe(model);
    }

    [Fact]
    public void Updated_WhenGivenSuccessResultAndNoModel_ReturnsNoContentResult()
    {
        // Arrange
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Updated<PersonStub>(this.successResult, model: null);

        // Assert
        response.Result.ShouldBeOfType<OkObjectResult>();
        ((OkObjectResult)response.Result).StatusCode.ShouldBe(200);
        ((OkObjectResult)response.Result).Value.ShouldBeNull();
    }

    [Fact]
    public void Updated_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Updated(this.failureResult, model, "test-route-name");

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(200);
    }

    [Fact]
    public void UpdatedWithAction_WhenGivenSuccessResultAndModel_ReturnsCreatedResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();
        const string routeName = "test-route-name";

        // Act
        var response = sut.Updated<PersonStub>(this.successResult, m => m = model, routeName);

        // Assert
        response.Result.ShouldBeOfType<UpdatedAtRouteResult>();
        ((UpdatedAtRouteResult)response.Result).StatusCode.ShouldBe(200);
        ((UpdatedAtRouteResult)response.Result).RouteName.ShouldBe(routeName);
        ((UpdatedAtRouteResult)response.Result).Value.ShouldNotBeNull();
    }

    [Fact]
    public void UpdatedWithAction_WhenGivenSuccessResultAndNoModel_ReturnsNoContentResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Updated<PersonStub>(this.successResult, m => m = PersonStub.Create(DateTime.UtcNow.Ticks));

        // Assert
        response.Result.ShouldBeOfType<OkObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(200);
    }

    [Fact]
    public void UpdatedWithAction_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Updated<PersonStub>(this.failureResult, m => m = PersonStub.Create(DateTime.UtcNow.Ticks), "test-route-name");

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(200);
    }

    [Fact]
    public void Accepted_WhenGivenSuccessResultAndModel_ReturnsAcceptedResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();
        const string routeName = "test-route-name";

        // Act
        var response = sut.Accepted(this.successResult, model, routeName);

        // Assert
        response.Result.ShouldBeOfType<AcceptedAtRouteResult>();
        ((AcceptedAtRouteResult)response.Result).StatusCode.ShouldBe(202);
        ((AcceptedAtRouteResult)response.Result).RouteName.ShouldBe(routeName);
        ((AcceptedAtRouteResult)response.Result).Value.ShouldBe(model);
    }

    [Fact]
    public void Accepted_WhenGivenSuccessResultAndNoModel_ReturnsNoContentResult()
    {
        // Arrange
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Accepted<PersonStub>(this.successResult, model: null);

        // Assert
        response.Result.ShouldBeOfType<OkObjectResult>();
        ((OkObjectResult)response.Result).StatusCode.ShouldBe(202);
        ((OkObjectResult)response.Result).Value.ShouldBeNull();
    }

    [Fact]
    public void Accepted_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Accepted(this.failureResult, model, "test-route-name");

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(200);
    }

    [Fact]
    public void AcceptedWithAction_WhenGivenSuccessResultAndModel_ReturnsAcceptedResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();
        const string routeName = "test-route-name";

        // Act
        var response = sut.Accepted<PersonStub>(this.successResult, m => m = PersonStub.Create(DateTime.UtcNow.Ticks), routeName);

        // Assert
        response.Result.ShouldBeOfType<AcceptedAtRouteResult>();
        ((AcceptedAtRouteResult)response.Result).StatusCode.ShouldBe(202);
        ((AcceptedAtRouteResult)response.Result).RouteName.ShouldBe(routeName);
    }

    [Fact]
    public void AcceptedWithAction_WhenGivenSuccessResultAndNoModel_ReturnsNoContentResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Accepted<PersonStub>(this.successResult, m => m = PersonStub.Create(DateTime.UtcNow.Ticks));

        // Assert
        response.Result.ShouldBeOfType<OkObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(202);
    }

    [Fact]
    public void AcceptedWithAction_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Accepted<PersonStub>(this.failureResult, m => m = PersonStub.Create(DateTime.UtcNow.Ticks), "test-route-name");

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(200);
    }

    [Fact]
    public void NoContent_WhenGivenSuccessResult_ReturnsNoContentResult()
    {
        // Arrange
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.NoContent<PersonStub>(this.successResult);

        // Assert
        response.Result.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public void NoContent_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.NoContent<PersonStub>(this.failureResult);

        // Assert
        response.Result.ShouldBeOfType<NoContentResult>();
        ((NoContentResult)response.Result).StatusCode.ShouldBe(204);
    }

    [Fact]
    public void Object_WhenGivenSuccessResult_ReturnsObjectResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Object(this.successResult, model, 201);

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(201);
        ((ObjectResult)response.Result).Value.ShouldBe(model);
    }

    [Fact]
    public void Object_WhenGivenFailureResult_ReturnsMappedErrorResult()
    {
        // Arrange
        var model = PersonStub.Create(DateTime.UtcNow.Ticks);
        var sut = new DefaultActionResultMapper();

        // Act
        var response = sut.Object(this.failureResult, model, 201);

        // Assert
        response.Result.ShouldBeOfType<ObjectResult>();
        ((ObjectResult)response.Result).StatusCode.ShouldBe(201);
    }
}