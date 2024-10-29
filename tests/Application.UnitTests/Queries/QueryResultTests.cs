// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Queries;

using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using Bogus;
using Shouldly;
using Xunit;

public class QueryResultTests
{
    private readonly Faker faker = new();

    [Fact]
    public void For_WithoutParameters_ShouldReturnSuccessQueryResponse()
    {
        // Arrange & Act
        var response = QueryResult.For();

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result>();
        response.Result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void For_WithGenericType_ShouldReturnSuccessQueryResponseWithDefaultValue()
    {
        // Arrange & Act
        var response = QueryResult.For<int>();

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result<int>>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldBe(default);
    }

    [Fact]
    public void For_WithGenericTypeAndValue_ShouldReturnSuccessQueryResponseWithValue()
    {
        // Arrange
        var value = this.faker.Random.Int();

        // Act
        var response = QueryResult.For(value);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result<int>>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldBe(value);
    }

    [Fact]
    public void For_WithResultParameter_ShouldReturnQueryResponseWithSameResult()
    {
        // Arrange
        var result = Result.Success().WithMessage(this.faker.Lorem.Sentence());

        // Act
        var response = QueryResult.For<int>(result);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result<int>>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Messages.ShouldBe(result.Messages);
    }

    [Fact]
    public void For_WithGenericResultParameter_ShouldReturnQueryResponseWithSameResult()
    {
        // Arrange
        var value = this.faker.Random.Int();
        var result = Result<int>.Success(value).WithMessage(this.faker.Lorem.Sentence());

        // Act
        var response = QueryResult.For(result);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result<int>>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldBe(value);
        response.Result.Messages.ShouldBe(result.Messages);
    }

    [Fact]
    public void For_WithGenericResultAndOutputType_ShouldReturnMappedQueryResponse()
    {
        // Arrange
        var inputValue = this.faker.Random.Int();
        var outputValue = this.faker.Lorem.Word();
        var result = Result<int>.Success(inputValue).WithMessage(this.faker.Lorem.Sentence());

        // Act
        var response = QueryResult.For<int, string>(result, outputValue);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldBeOfType<Result<string>>();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldBe(outputValue);
        response.Result.Messages.ShouldBe(result.Messages);
    }
}