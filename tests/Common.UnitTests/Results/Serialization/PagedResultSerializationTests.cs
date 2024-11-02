// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

using System.Text.Json;
using Bogus;

[UnitTest("Common")]
public class PagedResultSerializationTests
{
    private readonly Faker faker = new();
    private readonly ISerializer serializer = new SystemTextJsonSerializer();

    [Fact]
    public void SerializeToString_Success_WithPagination()
    {
        // Arrange
        var people = new[]
        {
            new PersonStub(
                this.faker.Name.FirstName(),
                this.faker.Name.LastName(),
                this.faker.Internet.Email(),
                this.faker.Random.Int(20, 50)),
            new PersonStub(
                this.faker.Name.FirstName(),
                this.faker.Name.LastName(),
                this.faker.Internet.Email(),
                this.faker.Random.Int(20, 50))
        };

        var result = PagedResult<PersonStub>.Success(
            people,
            "People retrieved",
            count: 100,
            page: 2,
            pageSize: 10);

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeTrue();
        root.GetProperty("messages")[0].GetString().ShouldBe("People retrieved");
        root.GetProperty("value").GetArrayLength().ShouldBe(2);

        // Pagination properties
        root.GetProperty("currentPage").GetInt32().ShouldBe(2);
        root.GetProperty("totalPages").GetInt32().ShouldBe(10);
        root.GetProperty("totalCount").GetInt64().ShouldBe(100);
        root.GetProperty("pageSize").GetInt32().ShouldBe(10);
        root.GetProperty("hasNextPage").GetBoolean().ShouldBeTrue();
        root.GetProperty("hasPreviousPage").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void SerializeToString_Failure_WithValidationErrors()
    {
        // Arrange
        var result = PagedResult<PersonStub>.Failure()
            .WithError(new ValidationError("Page number must be positive", "page"))
            .WithError(new ValidationError("Page size must be between 1 and 100", "pageSize"));

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        root.GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Null);

        var errors = root.GetProperty("errors");
        errors.GetArrayLength().ShouldBe(2);
        errors[0].GetProperty("propertyName").GetString().ShouldBe("page");
        errors[1].GetProperty("propertyName").GetString().ShouldBe("pageSize");
    }

    [Fact]
    public void SerializeToString_EmptyPage_WithCorrectPagination()
    {
        // Arrange
        var result = PagedResult<PersonStub>.Success(
            Array.Empty<PersonStub>(),
            count: 0,
            page: 1,
            pageSize: 10);

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeTrue();
        root.GetProperty("value").GetArrayLength().ShouldBe(0);
        root.GetProperty("totalCount").GetInt64().ShouldBe(0);
        root.GetProperty("totalPages").GetInt32().ShouldBe(0);
        root.GetProperty("hasNextPage").GetBoolean().ShouldBeFalse();
        root.GetProperty("hasPreviousPage").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void SerializeToString_LastPage_WithCorrectNavigationFlags()
    {
        // Arrange
        var people = new[]
        {
            new PersonStub(
                this.faker.Name.FirstName(),
                this.faker.Name.LastName(),
                this.faker.Internet.Email(),
                this.faker.Random.Int(20, 50))
        };

        var result = PagedResult<PersonStub>.Success(
            people,
            count: 21,
            page: 3,
            pageSize: 10);

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("currentPage").GetInt32().ShouldBe(3);
        root.GetProperty("totalPages").GetInt32().ShouldBe(3);
        root.GetProperty("hasNextPage").GetBoolean().ShouldBeFalse();
        root.GetProperty("hasPreviousPage").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void SerializeToString_WithComplexError_AndPagination()
    {
        // Arrange
        var exception = new InvalidOperationException("Database error");
        var result = PagedResult<PersonStub>.Failure()
            .WithError(new ExceptionError(exception))
            .WithMessage("Failed to retrieve page");

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        root.GetProperty("messages")[0].GetString().ShouldBe("Failed to retrieve page");

        var error = root.GetProperty("errors")[0];
        error.GetProperty("message").GetString().ShouldBe("Database error");
        error.GetProperty("exceptionType").GetString().ShouldContain("InvalidOperationException");
    }

    [Fact]
    public void SerializeToString_WithMultipleMessages_AndErrors()
    {
        // Arrange
        var messages = new[] { "Processing page", "Applying filters" };
        var result = PagedResult<PersonStub>.Success(
            Array.Empty<PersonStub>(),
            messages,
            count: 0,
            page: 1,
            pageSize: 10)
            .WithError(new DomainRuleError("ValidationRule", "Filter validation failed"));

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var jsonMessages = root.GetProperty("messages").EnumerateArray()
            .Select(m => m.GetString())
            .ToArray();
        // jsonMessages.ShouldContain(messages);

        var error = root.GetProperty("errors")[0];
        error.GetProperty("rule").GetString().ShouldBe("ValidationRule");
        error.GetProperty("message").GetString().ShouldBe("Filter validation failed");
    }

    [Fact]
    public void CanConvert_ReturnsCorrectResults()
    {
        // Arrange
        var factory = new PagedResultJsonConverterFactory();

        // Act & Assert
        factory.CanConvert(typeof(PagedResult<int>)).ShouldBeTrue();
        factory.CanConvert(typeof(PagedResult<PersonStub>)).ShouldBeTrue();
        factory.CanConvert(typeof(Result<int>)).ShouldBeFalse();
        factory.CanConvert(typeof(Result)).ShouldBeFalse();
        factory.CanConvert(typeof(List<PagedResult<int>>)).ShouldBeFalse();
    }

    [Fact]
    public void Deserialize_ThrowsNotSupportedException()
    {
        // Arrange
        var json = @"{""isSuccess"":true,""value"":[],""currentPage"":1}";

        // Act & Assert
        Should.Throw<NotSupportedException>(() =>
            this.serializer.Deserialize<PagedResult<PersonStub>>(json));
    }
}