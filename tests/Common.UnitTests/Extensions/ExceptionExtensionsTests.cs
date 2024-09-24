// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

// ReSharper disable ExpressionIsAlwaysNull
namespace BridgingIT.DevKit.Common.UnitTests;

public class ExceptionExtensionsTests
{
    private readonly Faker faker = new();

    [Fact]
    public void GetFullMessage_NullException_ReturnsNull()
    {
        // Arrange
        Exception source = null;

        // Act
        var result = source.GetFullMessage();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetFullMessage_ExceptionWithoutInnerException_ReturnsFormattedMessage()
    {
        // Arrange
        var message = this.faker.Lorem.Sentence();
        var source = new Exception(message);

        // Act
        var result = source.GetFullMessage();

        // Assert
        result.ShouldBe($"[Exception] {message}");
    }

    [Fact]
    public void GetFullMessage_ExceptionWithInnerException_ReturnsNestedFormattedMessage()
    {
        // Arrange
        var innerMessage = this.faker.Lorem.Sentence();
        var outerMessage = this.faker.Lorem.Sentence();
        var innerException = new InvalidOperationException(innerMessage);
        var source = new Exception(outerMessage, innerException);

        // Act
        var result = source.GetFullMessage();

        // Assert
        result.ShouldBe($"[Exception] {outerMessage}  --> [InvalidOperationException] {innerMessage}");
    }

    [Fact]
    public void GetFullMessage_ExceptionWithMultipleNestedExceptions_ReturnsFullNestedMessage()
    {
        // Arrange
        var innerMostMessage = this.faker.Lorem.Sentence();
        var middleMessage = this.faker.Lorem.Sentence();
        var outerMessage = this.faker.Lorem.Sentence();
        var innerMostException = new ArgumentException(innerMostMessage);
        var middleException = new InvalidOperationException(middleMessage, innerMostException);
        var source = new Exception(outerMessage, middleException);

        // Act
        var result = source.GetFullMessage();

        // Assert
        result.ShouldBe(
            $"[Exception] {outerMessage}  --> [InvalidOperationException] {middleMessage}  --> [ArgumentException] {innerMostMessage}");
    }
}

public class ExceptionExtensionsIsExpectedExceptionTests
{
    [Fact]
    public void IsExpectedException_SingleType_MatchingException_ReturnsTrue()
    {
        // Arrange
        var ex = new ArgumentException();

        // Act
        var result = ex.IsExpectedException<ArgumentException>();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsExpectedException_SingleType_NonMatchingException_ReturnsFalse()
    {
        // Arrange
        var ex = new InvalidOperationException();

        // Act
        var result = ex.IsExpectedException<ArgumentException>();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsExpectedException_TwoTypes_MatchingFirstType_ReturnsTrue()
    {
        // Arrange
        var ex = new ArgumentException();

        // Act
        var result = ex.IsExpectedException<ArgumentException, InvalidOperationException>();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsExpectedException_TwoTypes_MatchingSecondType_ReturnsTrue()
    {
        // Arrange
        var ex = new InvalidOperationException();

        // Act
        var result = ex.IsExpectedException<ArgumentException, InvalidOperationException>();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsExpectedException_TwoTypes_NonMatchingException_ReturnsFalse()
    {
        // Arrange
        var ex = new NullReferenceException();

        // Act
        var result = ex.IsExpectedException<ArgumentException, InvalidOperationException>();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsExpectedException_AggregateException_ContainsMatchingInnerException_ReturnsTrue()
    {
        // Arrange
        var innerEx = new ArgumentException();
        var ex = new AggregateException(innerEx);

        // Act
        var result = ex.IsExpectedException<ArgumentException>();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsExpectedException_AggregateException_NoMatchingInnerException_ReturnsFalse()
    {
        // Arrange
        var innerEx = new InvalidOperationException();
        var ex = new AggregateException(innerEx);

        // Act
        var result = ex.IsExpectedException<ArgumentException>();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsExpectedException_CustomPredicate_MatchingCondition_ReturnsTrue()
    {
        // Arrange
        var ex = new ArgumentException("Custom message");

        // Act
        var result = ex.IsExpectedException(e => e is ArgumentException && e.Message.Contains("Custom"));

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsExpectedException_CustomPredicate_NonMatchingCondition_ReturnsFalse()
    {
        // Arrange
        var ex = new ArgumentException("Different message");

        // Act
        var result = ex.IsExpectedException(e => e is ArgumentException && e.Message.Contains("Custom"));

        // Assert
        result.ShouldBeFalse();
    }
}