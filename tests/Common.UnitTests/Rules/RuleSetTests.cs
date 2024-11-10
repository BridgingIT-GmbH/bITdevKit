// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Rules;

using Common;
using Shouldly;
using Xunit;

[UnitTest("Common")]
public class RuleSetTests(RulesFixture fixture) : IClassFixture<RulesFixture>
{
    private readonly RulesFixture fixture = fixture;
    private readonly Faker faker = new();

    [Fact]
    public void Equal_WhenValuesAreEqual_ShouldReturnSuccess()
    {
        // Arrange
        var value = this.faker.Random.Int();
        var rule = RuleSet.Equal(value, value);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Equal_WhenValuesAreNotEqual_ShouldReturnFailure()
    {
        // Arrange
        var value1 = this.faker.Random.Int();
        var value2 = value1 + 1;
        var rule = RuleSet.Equal(value1, value2);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldBe($"Value must be equal to {value2}");
    }

    [Fact]
    public void NotEqual_WhenValuesAreDifferent_ShouldReturnSuccess()
    {
        // Arrange
        var value1 = this.faker.Random.Int();
        var value2 = value1 + 1;
        var rule = RuleSet.NotEqual(value1, value2);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void NotEqual_WhenValuesAreEqual_ShouldReturnFailure()
    {
        // Arrange
        var value = this.faker.Random.Int();
        var rule = RuleSet.NotEqual(value, value);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldBe($"Value must not be equal to {value}");
    }

    // Numeric Comparison Rules Tests
    [Theory]
    [InlineData(5, 3)]
    [InlineData(0, -1)]
    [InlineData(100, 99)]
    public void GreaterThan_WhenValueIsGreater_ShouldReturnSuccess(int value, int other)
    {
        // Arrange
        var rule = RuleSet.GreaterThan(value, other);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(3, 5)]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    public void GreaterThan_WhenValueIsLessOrEqual_ShouldReturnFailure(int value, int other)
    {
        // Arrange
        var rule = RuleSet.GreaterThan(value, other);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldBe($"Value must be greater than {other}");
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(0, -1)]
    [InlineData(100, 100)]
    public void GreaterThanOrEqual_WhenValueIsGreaterOrEqual_ShouldReturnSuccess(int value, int other)
    {
        // Arrange
        var rule = RuleSet.GreaterThanOrEqual(value, other);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    // String Validation Rules Tests
    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidEmail_ShouldValidateEmailCorrectly(string email, bool shouldBeValid)
    {
        // Arrange
        var rule = RuleSet.IsValidEmail(email);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBe(shouldBeValid);
        if (!shouldBeValid)
        {
            result.Errors.First().Message.ShouldBe("Invalid email address");
        }
    }

    [Theory]
    [InlineData("Hello", "lo", StringComparison.Ordinal, true)]
    [InlineData("Hello", "LO", StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("Hello", "x", StringComparison.Ordinal, false)]
    public void Contains_ShouldCheckSubstringCorrectly(string value, string substring, StringComparison comparison, bool shouldSucceed)
    {
        // Arrange
        var rule = RuleSet.Contains(value, substring, comparison);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBe(shouldSucceed);
        if (!shouldSucceed)
        {
            result.Errors.First().Message.ShouldBe($"Value must contain '{substring}'");
        }
    }

    // Collection Rules Tests
    [Fact]
    public void Any_WhenPredicateMatchesAnyItem_ShouldReturnSuccess()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var rule = RuleSet.Any(numbers, num => RuleSet.GreaterThan(num, 3));

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void All_WhenPredicateMatchesAllItems_ShouldReturnSuccess()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3 };
        var rule = RuleSet.All(numbers, num => RuleSet.LessThan(num, 4));

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    // DateTime Rules Tests
    [Fact]
    public void IsInRange_WithDateTime_WhenInRange_ShouldReturnSuccess()
    {
        // Arrange
        var now = DateTime.Now;
        var start = now.AddDays(-1);
        var end = now.AddDays(1);
        var rule = RuleSet.IsInRange(now, start, end);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsInRelativeRange_WhenWithinRange_ShouldReturnSuccess()
    {
        // Arrange
        var now = DateTime.Now;
        var value = now.AddDays(1);
        var rule = RuleSet.IsInRelativeRange(value, DateUnit.Day, 2, DateTimeDirection.Future);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    // Pattern Matching Rules Tests
    [Theory]
    [InlineData("123-456", @"\d{3}-\d{3}", true)]
    [InlineData("abc", @"\d{3}-\d{3}", false)]
    public void MatchesPattern_ShouldValidateRegexCorrectly(string value, string pattern, bool shouldMatch)
    {
        // Arrange
        var rule = RuleSet.MatchesPattern(value, pattern);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBe(shouldMatch);
        if (!shouldMatch)
        {
            result.Errors.First().Message.ShouldBe($"Value does not match pattern: {pattern}");
        }
    }

    // FluentValidation Integration Tests
    [Fact]
    public void Validate_WithFluentValidation_ShouldValidateCorrectly()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            "invalid-email",
            16);
        var validator = new TestValidator();
        var rule = RuleSet.Validate(person, validator);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<FluentValidationError>().ShouldBeTrue();
        var error = result.GetError<FluentValidationError>();
        error.Message.ShouldContain("Must be 18 or older");
        error.Message.ShouldContain("Invalid email");
    }

    // Length Validation Rules Tests
    [Theory]
    [InlineData("test", 2, 6, true)]
    [InlineData("test", 5, 10, false)]
    [InlineData("", 1, 5, false)]
    public void HasStringLength_ShouldValidateLengthCorrectly(string value, int min, int max, bool shouldBeValid)
    {
        // Arrange
        var rule = RuleSet.HasStringLength(value, min, max);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBe(shouldBeValid);
        if (!shouldBeValid)
        {
            result.Errors.First().Message.ShouldBe($"Text length must be between {min} and {max} characters");
        }
    }

    // Collection Size Rules Tests
    [Theory]
    [InlineData(3, 1, 5, true)]
    [InlineData(0, 1, 5, false)]
    [InlineData(6, 1, 5, false)]
    public void HasCollectionSize_ShouldValidateSizeCorrectly(int itemCount, int minSize, int maxSize, bool shouldBeValid)
    {
        // Arrange
        var collection = Enumerable.Range(1, itemCount);
        var rule = RuleSet.HasCollectionSize(collection, minSize, maxSize);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBe(shouldBeValid);
        if (!shouldBeValid)
        {
            result.Errors.First().Message.ShouldBe($"Collection size must be between {minSize} and {maxSize} items");
        }
    }

    // Additional Numeric Comparison Rules Tests
    [Theory]
    [InlineData(3, 5)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    public void LessThan_WhenValueIsLess_ShouldReturnSuccess(int value, int other)
    {
        // Arrange
        var rule = RuleSet.LessThan(value, other);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void LessThan_WhenValueIsGreaterOrEqual_ShouldReturnFailure(int value, int other)
    {
        // Arrange
        var rule = RuleSet.LessThan(value, other);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldBe($"Value must be less than {other}");
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(2, 3)]
    [InlineData(0, 0)]
    public void LessThanOrEqual_WhenValueIsLessOrEqual_ShouldReturnSuccess(int value, int other)
    {
        // Arrange
        var rule = RuleSet.LessThanOrEqual(value, other);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void LessThanOrEqual_WhenValueIsGreater_ShouldReturnFailure(int value, int other)
    {
        // Arrange
        var rule = RuleSet.LessThanOrEqual(value, other);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldBe($"Value must be less than or equal to {other}");
    }

// Additional DateTime Rules Tests
    [Fact]
    public void IsBefore_WhenDateTimeIsBefore_ShouldReturnSuccess()
    {
        // Arrange
        var now = DateTime.Now;
        var future = now.AddDays(1);
        var rule = RuleSet.IsBefore(now, future);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsBefore_WhenDateTimeIsAfter_ShouldReturnFailure()
    {
        // Arrange
        var now = DateTime.Now;
        var past = now.AddDays(-1);
        var rule = RuleSet.IsBefore(now, past);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldBe($"Value must be before {past}");
    }

    [Fact]
    public void IsAfter_WhenDateTimeIsAfter_ShouldReturnSuccess()
    {
        // Arrange
        var now = DateTime.Now;
        var past = now.AddDays(-1);
        var rule = RuleSet.IsAfter(now, past);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsAfter_WhenDateTimeIsBefore_ShouldReturnFailure()
    {
        // Arrange
        var now = DateTime.Now;
        var future = now.AddDays(1);
        var rule = RuleSet.IsAfter(now, future);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.First().Message.ShouldBe($"Value must be after {future}");
    }

// Additional Pattern Matching Rules Tests
    [Theory]
    [InlineData("ABC123", @"[A-Z]{3}\d{3}", true)]
    [InlineData("abc123", @"[A-Z]{3}\d{3}", false)]
    public void MatchesPattern_CaseSensitivePattern_ShouldValidateRegexCorrectly(string value, string pattern, bool shouldMatch)
    {
        // Arrange
        var rule = RuleSet.MatchesPattern(value, pattern);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBe(shouldMatch);
        if (!shouldMatch)
        {
            result.Errors.First().Message.ShouldBe($"Value does not match pattern: {pattern}");
        }
    }

// Additional Length Validation Rules Tests
    [Theory]
    [InlineData("sample", 1, 10, true)]
    [InlineData("sample", 1, 5, false)]
    public void HasStringLength_WhenStringLengthIsOutOfRange_ShouldReturnFailure(string value, int min, int max, bool shouldBeValid)
    {
        // Arrange
        var rule = RuleSet.HasStringLength(value, min, max);

        // Act
        var result = Rule.Apply(rule);

        // Assert
        result.IsSuccess.ShouldBe(shouldBeValid);
        if (!shouldBeValid)
        {
            result.Errors.First().Message.ShouldContain($"Text length must be between {min} and {max} characters");
        }
    }

    // Numeric Comparison - Boundary Tests
    [Theory]
    [InlineData(5, 6, false)]
    [InlineData(5, 5, true)]
    [InlineData(5, 4, true)]
    public void GreaterThanOrEqual_BoundaryTest(int value, int other, bool shouldSucceed)
    {
        var rule = RuleSet.GreaterThanOrEqual(value, other);
        var result = Rule.Apply(rule);

        result.IsSuccess.ShouldBe(shouldSucceed);
    }

// Date Range - Boundary Tests
    [Fact]
    public void IsInRange_WhenDateExactlyAtStartOrEnd_ShouldReturnSuccess()
    {
        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(1);
        var ruleStart = RuleSet.IsInRange(start, start, end);
        var ruleEnd = RuleSet.IsInRange(end, start, end);

        Rule.Apply(ruleStart).IsSuccess.ShouldBeTrue();
        Rule.Apply(ruleEnd).IsSuccess.ShouldBeTrue();
    }

// String Validation - Null or Empty
    [Theory]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("NotEmpty", false)]
    public void IsNullOrEmpty_ShouldValidateNullOrEmptyStrings(string value, bool shouldSucceed)
    {
        var rule = RuleSet.IsEmpty(value);
        var result = Rule.Apply(rule);

        result.IsSuccess.ShouldBe(shouldSucceed);
    }

// Null Checks
    [Fact]
    public void IsNull_WhenValueIsNull_ShouldReturnSuccess()
    {
        var rule = RuleSet.IsNull<PersonStub>(null);
        var result = Rule.Apply(rule);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsNotNull_WhenValueIsNotNull_ShouldReturnSuccess()
    {
        var rule = RuleSet.IsNotNull("non-null");
        var result = Rule.Apply(rule);

        result.IsSuccess.ShouldBeTrue();
    }

// Collection - IsEmpty and IsNotEmpty
    [Fact]
    public void IsEmpty_WhenCollectionIsEmpty_ShouldReturnSuccess()
    {
        var emptyCollection = new List<int>();
        var rule = RuleSet.IsEmpty(emptyCollection);
        var result = Rule.Apply(rule);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsNotEmpty_WhenCollectionHasItems_ShouldReturnSuccess()
    {
        var nonEmptyCollection = new List<int> { 1 };
        var rule = RuleSet.IsNotEmpty(nonEmptyCollection);
        var result = Rule.Apply(rule);

        result.IsSuccess.ShouldBeTrue();
    }

// Boolean Rules
    [Fact]
    public void IsTrue_WhenValueIsTrue_ShouldReturnSuccess()
    {
        var rule = RuleSet.IsTrue(true);
        var result = Rule.Apply(rule);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsFalse_WhenValueIsFalse_ShouldReturnSuccess()
    {
        var rule = RuleSet.IsFalse(false);
        var result = Rule.Apply(rule);

        result.IsSuccess.ShouldBeTrue();
    }
}