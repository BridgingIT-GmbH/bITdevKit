// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

using System.Text;
using System.Text.Json;
using Bogus;

[UnitTest("Common")]
public class ResultSerializationTests
{
    private readonly Faker faker = new();
    private readonly ISerializer serializer = new SystemTextJsonSerializer();

    [Fact]
    public void Serialize_SuccessResult_ToStream()
    {
        // Arrange
        var message = this.faker.Lorem.Sentence();
        var result = Result.Success(message);
        using var stream = new MemoryStream();

        // Act
        this.serializer.Serialize(result, stream);
        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeTrue();
        root.GetProperty("messages").GetArrayLength().ShouldBe(1);
        root.GetProperty("messages")[0].GetString().ShouldBe(message);
        root.GetProperty("errors").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void Serialize_FailureResultWithErrors_ToStream()
    {
        // Arrange
        var result = Result.Failure()
            .WithError(new EntityNotFoundError("User", "123"))
            .WithError(new ValidationError("Invalid format", "email"));
        using var stream = new MemoryStream();

        // Act
        this.serializer.Serialize(result, stream);
        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        var errors = root.GetProperty("errors");
        errors.GetArrayLength().ShouldBe(2);

        errors[0].GetProperty("entityType").GetString().ShouldBe("User");
        errors[1].GetProperty("propertyName").GetString().ShouldBe("email");
    }

    [Fact]
    public void Serialize_NullStream_HandlesGracefully()
    {
        // Arrange
        var result = Result.Success();

        // Act & Assert
        Should.NotThrow(() => this.serializer.Serialize(result, null));
    }

    [Fact]
    public void SerializeToString_ResultOfT_Success()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            this.faker.Random.Int(20, 50));

        var result = Result<PersonStub>.Success(person)
            .WithMessage("Person created");

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeTrue();
        root.GetProperty("value").GetProperty("firstName").GetString().ShouldBe(person.FirstName);
        root.GetProperty("messages")[0].GetString().ShouldBe("Person created");
    }

    [Fact]
    public void SerializeToBytes_ResultOfT_WithComplexError()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var result = Result<int>.Failure(42)
            .WithError(new ExceptionError(exception));

        // Act
        var bytes = this.serializer.SerializeToBytes(result);
        var json = Encoding.UTF8.GetString(bytes);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        // root.GetProperty("value").GetInt32().ShouldBe(42);

        var error = root.GetProperty("errors")[0];
        error.GetProperty("message").GetString().ShouldBe("Test exception");
        error.GetProperty("exceptionType").GetString().ShouldContain("InvalidOperationException");
    }

    [Fact]
    public void SerializeToString_DomainRuleError_WithMessages()
    {
        // Arrange
        var result = Result.Failure()
            .WithError(new Error("Rule1"));

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var error = root.GetProperty("errors")[0];
        error.GetProperty("message").GetString().ShouldBe("Rule1");
    }

    [Fact]
    public void Deserialize_Stream_ThrowsNotSupported()
    {
        // Arrange
        var json = @"{""isSuccess"":true,""messages"":[],""errors"":[]}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act & Assert
        Should.Throw<NotSupportedException>(() =>
            this.serializer.Deserialize<Result>(stream));
    }

    [Fact]
    public void Deserialize_NullStream_ReturnsDefault()
    {
        // Arrange & Act
        var result = this.serializer.Deserialize<Result>(null);

        // Assert
        result.ShouldBe(default);
    }

    [Fact]
    public void Deserialize_EmptyStream_ReturnsDefault()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = this.serializer.Deserialize<Result>(stream);

        // Assert
        result.ShouldBe(default);
    }

    [Fact]
    public void Serialize_ComplexChainOfOperations()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            17);

        var result = Result<PersonStub>.Success(person)
            .WithMessage("Initial validation")
            .Ensure(p => p.Age >= 18, new ValidationError("age", "Must be 18 or older"))
            .WithMessage("Age validation")
            .Map(p => p.Age)
            .WithError(new Error("Additional rule failed"));

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        //root.GetProperty("value").GetInt32().ShouldBe(0); // due to Ensure there is no value as it failed there

        var messages = root.GetProperty("messages").EnumerateArray().Select(e => e.GetString()).ToArray();
        messages.ShouldContain("Initial validation");
        messages.ShouldContain("Age validation");

        var errors = root.GetProperty("errors");
        errors.GetArrayLength().ShouldBe(2); // ValidationError and DomainRuleError
    }

    [Fact]
    public void SerializeToString_WithNullValue_InResultOfT()
    {
        // Arrange
        PersonStub person = null;
        var result = Result<PersonStub>.Success(person)
            .WithMessage("Created with null");

        // Act
        var json = this.serializer.SerializeToString(result);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeTrue();
        root.GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("messages")[0].GetString().ShouldBe("Created with null");
    }
}