// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

public class ResultMapExtensionsPagedTests
{
    private readonly ILogger logger;
    private readonly Faker<PersonStub> personFaker;

    public ResultMapExtensionsPagedTests()
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

    private IEnumerable<PersonStub> CreatePeople(int count) => this.personFaker.Generate(count);

    [Fact]
    public void MapHttpOkPaged_Success_ReturnsOkWithPagedResponse()
    {
        // Arrange
        var people = this.CreatePeople(3).ToList();
        var totalCount = 100L;
        var page = 1;
        var pageSize = 10;
        var result = ResultPaged<PersonStub>.Success(people, totalCount, page, pageSize)
            .WithMessage("Successfully retrieved people");

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Ok<PagedResponse<PersonStub>>>();
        var okResult = (Ok<PagedResponse<PersonStub>>)innerResult;
        var pagedResponse = okResult.Value;

        pagedResponse.Items.ShouldBe(people);
        pagedResponse.Page.ShouldBe(page);
        pagedResponse.PageSize.ShouldBe(pageSize);
        pagedResponse.TotalCount.ShouldBe(totalCount);
        pagedResponse.TotalPages.ShouldBe((int)Math.Ceiling(totalCount / (double)pageSize));
        pagedResponse.HasPreviousPage.ShouldBeFalse();
        pagedResponse.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void MapHttpOkPaged_UnauthorizedError_ReturnsUnauthorized()
    {
        // Arrange
        var result = ResultPaged<PersonStub>.Failure()
            .WithError(new UnauthorizedError("Access denied"));

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public void MapHttpOkPaged_ValidationError_ReturnsBadRequest()
    {
        // Arrange
        var result = ResultPaged<PersonStub>.Failure()
            .WithError(new ValidationError("Page number must be positive"));

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
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
        errors["validation"].ShouldContain("Page number must be positive");
    }

    [Fact]
    public void MapHttpOkPaged_EmptyResultWithZeroCount_ReturnsOkWithEmptyPagedResponse()
    {
        // Arrange
        var result = ResultPaged<PersonStub>.Success(
            values: Array.Empty<PersonStub>(),
            count: 0,
            page: 1,
            pageSize: 10);

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Ok<PagedResponse<PersonStub>>>();
        var okResult = (Ok<PagedResponse<PersonStub>>)innerResult;
        var pagedResponse = okResult.Value;

        pagedResponse.Items.ShouldBeEmpty();
        pagedResponse.Page.ShouldBe(1);
        pagedResponse.PageSize.ShouldBe(10);
        pagedResponse.TotalCount.ShouldBe(0);
        pagedResponse.TotalPages.ShouldBe(0);
        pagedResponse.HasPreviousPage.ShouldBeFalse();
        pagedResponse.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void MapHttpOkPaged_InvalidPageNumber_ReturnsOkWithCorrectedPage()
    {
        // Arrange
        var people = this.CreatePeople(5).ToList();
        var totalCount = 50L;
        var result = ResultPaged<PersonStub>.Success(people, totalCount, page: -1, pageSize: 10); // Negative page corrected to 1

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Ok<PagedResponse<PersonStub>>>();
        var okResult = (Ok<PagedResponse<PersonStub>>)innerResult;
        var pagedResponse = okResult.Value;

        pagedResponse.Items.ShouldBe(people);
        pagedResponse.Page.ShouldBe(1); // Corrected from -1
        pagedResponse.PageSize.ShouldBe(10);
        pagedResponse.TotalCount.ShouldBe(totalCount);
        pagedResponse.TotalPages.ShouldBe(5);
        pagedResponse.HasPreviousPage.ShouldBeFalse();
        pagedResponse.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void MapHttpOkPaged_WithMessages_Success_ReturnsOkWithPagedResponse()
    {
        // Arrange
        var people = this.CreatePeople(2).ToList();
        var totalCount = 20L;
        var page = 2;
        var pageSize = 5;
        var result = ResultPaged<PersonStub>.Success(people, totalCount, page, pageSize)
            .WithMessages(new[] { "Page retrieved", "Filtered by age" });

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Ok<PagedResponse<PersonStub>>>();
        var okResult = (Ok<PagedResponse<PersonStub>>)innerResult;
        var pagedResponse = okResult.Value;

        pagedResponse.Items.ShouldBe(people);
        pagedResponse.Page.ShouldBe(page);
        pagedResponse.PageSize.ShouldBe(pageSize);
        pagedResponse.TotalCount.ShouldBe(totalCount);
        pagedResponse.TotalPages.ShouldBe(4); // 20 / 5
        pagedResponse.HasPreviousPage.ShouldBeTrue();
        pagedResponse.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void MapHttpOkPaged_LastPage_Success_ReturnsOkWithPagedResponse()
    {
        // Arrange
        var people = this.CreatePeople(3).ToList();
        var totalCount = 23L;
        var page = 5; // Last page: 23 items, 5 per page → 5 pages
        var pageSize = 5;
        var result = ResultPaged<PersonStub>.Success(people, totalCount, page, pageSize);

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Ok<PagedResponse<PersonStub>>>();
        var okResult = (Ok<PagedResponse<PersonStub>>)innerResult;
        var pagedResponse = okResult.Value;

        pagedResponse.Items.ShouldBe(people);
        pagedResponse.Page.ShouldBe(page);
        pagedResponse.PageSize.ShouldBe(pageSize);
        pagedResponse.TotalCount.ShouldBe(totalCount);
        pagedResponse.TotalPages.ShouldBe(5); // 23 / 5 → ceiling to 5
        pagedResponse.HasPreviousPage.ShouldBeTrue();
        pagedResponse.HasNextPage.ShouldBeFalse(); // Last page
    }

    [Fact]
    public void MapHttpOkPaged_SingleItemPage_Success_ReturnsOkWithPagedResponse()
    {
        // Arrange
        var people = this.CreatePeople(1).ToList();
        var totalCount = 1L;
        var page = 1;
        var pageSize = 1; // Single item per page
        var result = ResultPaged<PersonStub>.Success(people, totalCount, page, pageSize);

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Ok<PagedResponse<PersonStub>>>();
        var okResult = (Ok<PagedResponse<PersonStub>>)innerResult;
        var pagedResponse = okResult.Value;

        pagedResponse.Items.ShouldBe(people);
        pagedResponse.Page.ShouldBe(1);
        pagedResponse.PageSize.ShouldBe(1);
        pagedResponse.TotalCount.ShouldBe(1);
        pagedResponse.TotalPages.ShouldBe(1);
        pagedResponse.HasPreviousPage.ShouldBeFalse();
        pagedResponse.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void MapHttpOkPaged_LargePageSize_Success_ReturnsOkWithPagedResponse()
    {
        // Arrange
        var people = this.CreatePeople(10).ToList();
        var totalCount = 10L;
        var page = 1;
        var pageSize = 100; // Page size larger than total count
        var result = ResultPaged<PersonStub>.Success(people, totalCount, page, pageSize);

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<Ok<PagedResponse<PersonStub>>>();
        var okResult = (Ok<PagedResponse<PersonStub>>)innerResult;
        var pagedResponse = okResult.Value;

        pagedResponse.Items.ShouldBe(people);
        pagedResponse.Page.ShouldBe(1);
        pagedResponse.PageSize.ShouldBe(100);
        pagedResponse.TotalCount.ShouldBe(10);
        pagedResponse.TotalPages.ShouldBe(1); // All items fit in one page
        pagedResponse.HasPreviousPage.ShouldBeFalse();
        pagedResponse.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void MapOkPaged_CustomUnauthorizedErrorHandler_ReturnsProblemWithCustomStatus()
    {
        // Arrange
        ResultMapErrorHandlerRegistry.RegisterHandler<UnauthorizedError>((logger, r) => new CustomHttpResult($"Custom unauthorized: {r.Errors.First().Message}"));
        var result = ResultPaged<PersonStub>.Failure()
            .WithError(new UnauthorizedError("Access denied"));

        // Act
        var response = result.MapHttpOkPaged(this.logger);

        // Assert
        response.ShouldBeOfType<Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>>();
        var innerResult = ((Results<Ok<PagedResponse<PersonStub>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>)response).Result;
        innerResult.ShouldBeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)innerResult;
        problemResult.StatusCode.ShouldBe(418); // CustomHttpResult status preserved
        problemResult.ProblemDetails.Detail.ShouldContain("A custom error handler was executed");
        problemResult.ProblemDetails.Extensions["customResultType"].ShouldBe("CustomHttpResult");

        // Cleanup
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }
}
