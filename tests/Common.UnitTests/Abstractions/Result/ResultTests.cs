// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

[UnitTest("Common")]
public class ResultTests
{
    [Fact]
    public void Failure_WithNoArguments_ReturnsFailedResult()
    {
        // Arrange
        // Act
        var result = Result.Failure();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.ShouldBeFailure();
        result.ShouldNotContainMessages();
        result.ShouldNotContainError<NotFoundResultError>();
    }

    [Fact]
    public void Failure_WithError_ReturnsFailedResult()
    {
        // Arrange
        // Act
        var result = Result.Failure()
            .WithError<NotFoundResultError>();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.ShouldBeFailure();
        result.ShouldNotContainMessages();
        result.ShouldContainError<NotFoundResultError>();
    }

    [Fact]
    public void Failure_WithMessage_ReturnsFailedResultWithMessage()
    {
        // Arrange
        const string message = "Something went wrong";

        // Act
        var result = Result.Failure(message);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainMessage(message);
    }

    [Fact]
    public void Failure_WithMessages_ReturnsFailedResultWithMessages()
    {
        // Arrange
        var messages = new List<string> { "Something went wrong", "Try again later" };

        // Act
        var result = Result.Failure(messages);

        // Assert
        result.ShouldBeFailure();
        result.Messages.Count.ShouldBe(2);
        result.Messages.ShouldBe(messages);
    }

    [Fact]
    public void Success_WithNoArguments_ReturnsSuccessfulResult()
    {
        // Arrange
        // Act
        var result = Result.Success();

        // Assert
        result.ShouldBeSuccess();
        result.ShouldNotContainMessages();
    }

    [Fact]
    public void Success_WithMessage_ReturnsSuccessfulResultWithMessage()
    {
        // Arrange
        const string message = "Action completed successfully";

        // Act
        var result = Result.Success(message);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage(message);
    }

    [Fact]
    public void Failure_ValueWithNoArguments_ReturnsFailedResult()
    {
        // Arrange
        // Act
        var result = Result<int>.Failure();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.ShouldBeFailure();
        result.ShouldNotContainMessages();
        result.ShouldNotContainError<NotFoundResultError>();
    }

    [Fact]
    public void Failure_ValueWithError_ReturnsFailedResult()
    {
        // Arrange
        // Act
        var result = Result<int>.Failure()
            .WithError<NotFoundResultError>();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.ShouldBeFailure();
        result.ShouldNotContainMessages();
        result.ShouldContainError<NotFoundResultError>();
    }

    [Fact]
    public void Failure_ValueWithMessage_ReturnsFailedResultWithMessage()
    {
        // Arrange
        const string message = "Something went wrong";

        // Act
        var result = Result<int>.Failure(message);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainMessage(message);
    }

    [Fact]
    public void Failure_ValueWithMessages_ReturnsFailedResultWithMessages()
    {
        // Arrange
        var messages = new List<string> { "Something went wrong", "Try again later" };

        // Act
        var result = Result<int>.Failure(messages);

        // Assert
        result.ShouldBeFailure();
        result.Messages.Count.ShouldBe(2);
        result.Messages.ShouldBe(messages);
    }

    [Fact]
    public void Success_ValueWithNoArguments_ReturnsSuccessfulResult()
    {
        // Arrange
        // Act
        var result = Result<int>.Success(42);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldNotBeValue(0);
        result.ShouldBeValue(42);
        result.ShouldNotContainMessages();
    }

    [Fact]
    public void Success_ValueWithMessage_ReturnsSuccessfulResultWithMessage()
    {
        // Arrange
        const string message = "Action completed successfully";

        // Act
        var result = Result<int>.Success(42, message);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldNotBeValue(0);
        result.ShouldBeValue(42);
        result.ShouldContainMessage(message);
    }
}