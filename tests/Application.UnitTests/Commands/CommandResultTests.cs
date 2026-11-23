// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Commands;

using Bogus;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using Shouldly;
using Xunit;

public class CommandResultTests
{
    private readonly Faker faker = new();

    [Fact]
    public void For_WithoutParameters_ShouldReturnSuccessCommandResponse()
    {
        // Arrange & Act
        var response = CommandResult.For();

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result>();
        response.Result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void For_WithGenericType_ShouldReturnSuccessCommandResponseWithDefaultValue()
    {
        // Arrange & Act
        var response = CommandResult.For<int>();

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result<int>>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldBe(0);
    }

    [Fact]
    public void For_WithGenericTypeAndValue_ShouldReturnSuccessCommandResponseWithValue()
    {
        // Arrange
        var value = this.faker.Random.Int();

        // Act
        var response = CommandResult.For(value);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result<int>>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldBe(value);
    }

    [Fact]
    public void For_WithResultParameter_ShouldReturnCommandResponseWithSameResult()
    {
        // Arrange
        var result = Result.Success().WithMessage(this.faker.Lorem.Sentence());

        // Act
        var response = CommandResult.For(result);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Messages.ShouldBe(result.Messages);
    }

    [Fact]
    public void For_WithGenericResultParameter_ShouldReturnCommandResponseWithSameResult()
    {
        // Arrange
        var value = this.faker.Random.Int();
        var result = Result<int>.Success(value).WithMessage(this.faker.Lorem.Sentence());

        // Act
        var response = CommandResult.For(result);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result<int>>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldBe(value);
        response.Result.Messages.ShouldBe(result.Messages);
    }

    [Fact]
    public void For_WithGenericResultAndOutputType_ShouldReturnMappedCommandResponse()
    {
        // Arrange
        var inputValue = this.faker.Random.Int();
        var outputValue = this.faker.Lorem.Word();
        var result = Result<int>.Success(inputValue).WithMessage(this.faker.Lorem.Sentence());

        // Act
        var response = CommandResult.For(result, outputValue);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result<string>>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldBe(outputValue);
        response.Result.Messages.ShouldBe(result.Messages);
    }

    [Fact]
    public void Failure_WithTypedError_PreservesSuppliedValueAndMessage()
    {
        var sut = CommandResult.Failure<int, DomainPolicyError>(42, "Missing item").Result;

        sut.IsFailure.ShouldBeTrue();
        sut.Value.ShouldBe(42);
        sut.Messages.ShouldContain("Missing item");
        sut.HasError<DomainPolicyError>().ShouldBeTrue();
    }

    [Fact]
    public void For_WithPagedResultAsValue_PreservesInferredResponseType()
    {
        var page = ResultPaged<int>.Success([42], count: 91, page: 3, pageSize: 2)
            .WithError(new NotFoundError());

        var sut = CreateResponse();

        sut.Result.IsSuccess.ShouldBeTrue();
        sut.Result.Value.ShouldBe(page);

        CommandResponse<Result<ResultPaged<int>>> CreateResponse() => CommandResult.For(page);
    }

    [Fact]
    public void CommandResponseFor_WithFailedPage_PreservesFailureAndPaging()
    {
        var page = ResultPaged<int>.Success([42], count: 91, page: 3, pageSize: 2);
        var result = page.WithError(new NotFoundError());

        var sut = CommandResponse.For(result).Result;

        sut.IsFailure.ShouldBeTrue();
        sut.HasError<NotFoundError>().ShouldBeTrue();
        sut.TotalCount.ShouldBe(91);
        sut.CurrentPage.ShouldBe(3);
        sut.PageSize.ShouldBe(2);
    }
}
