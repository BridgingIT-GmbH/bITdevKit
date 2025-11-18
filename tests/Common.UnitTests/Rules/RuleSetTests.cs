// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Rules;

using Common;
using Shouldly;
using Xunit;

[UnitTest("Common")]
[Collection(nameof(RuleBuilderCollectionDefinition))] // prevents parellel execution with RuleTests (which modify the global Rule Settings)
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Equal_WhenValuesAreNotEqual_ShouldReturnFailure()
    {
        // Arrange
        var value1 = this.faker.Random.Int();
        var value2 = value1 + 1;
        var rule = RuleSet.Equal(value1, value2);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeFailure();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void NotEqual_WhenValuesAreEqual_ShouldReturnFailure()
    {
        // Arrange
        var value = this.faker.Random.Int();
        var rule = RuleSet.NotEqual(value, value);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeFailure();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeFailure();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
    }

    // Additional failure cases
    [Theory]
    [InlineData(3, 5)]
    [InlineData(0, 1)]
    public void GreaterThanOrEqual_WhenValueIsLess_ShouldReturnFailure(int value, int other)
    {
        var rule = RuleSet.GreaterThanOrEqual(value, other);
        var result = Rule.Check(rule);

        result.ShouldBeFailure();
        result.Errors.First().Message.ShouldBe($"Value must be greater than or equal to {other}");
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
        var result = Rule.Check(rule);

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
        var result = Rule.Check(rule);

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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Any_WhenNoItemsMatchPredicate_ShouldReturnFailure()
    {
        var numbers = new[] { 1, 2, 3 };
        var rule = RuleSet.Any(numbers, num => RuleSet.GreaterThan(num, 10));

        var result = Rule.Check(rule);
        result.ShouldBeFailure();
    }

    [Fact]
    public void All_WhenPredicateMatchesAllItems_ShouldReturnSuccess()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3 };
        var rule = RuleSet.All(numbers, num => RuleSet.LessThan(num, 4));

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void All_WhenNotAllItemsMatchPredicate_ShouldReturnFailure()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var rule = RuleSet.All(numbers, num => RuleSet.LessThan(num, 3));

        var result = Rule.Check(rule);
        result.ShouldBeFailure();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsInRelativeRange_WhenWithinRange_ShouldReturnSuccess()
    {
        // Arrange
        var now = DateTime.Now;
        var value = now.AddDays(1);
        var rule = RuleSet.IsInRelativeRange(value, DateUnit.Day, 2, DateTimeDirection.Future);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsInRelativeRange_WithDateTime_WhenOutsideRange_ShouldReturnFailure()
    {
        var now = DateTime.Now;
        var value = now.AddDays(3);
        var rule = RuleSet.IsInRelativeRange(value, DateUnit.Day, 2, DateTimeDirection.Future);

        var result = Rule.Check(rule);
        result.ShouldBeFailure();
    }

    [Fact]
    public void IsInRelativeRange_WithTimeOnly_WhenOutsideRange_ShouldReturnFailure()
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var value = now.Add(TimeUnit.Minute, 3);
        var rule = RuleSet.IsInRelativeRange(value, TimeUnit.Minute, 1, DateTimeDirection.Future);

        var result = Rule.Check(rule);
        result.ShouldBeFailure();
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
        var result = Rule.Check(rule);

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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeFailure();
        result.HasError<FluentValidationError>().ShouldBeTrue();
        var fluentError = result.GetError<FluentValidationError>();
        fluentError.Errors.Any(e => e.ErrorMessage.Contains("Must be 18 or older")).ShouldBeTrue();
        fluentError.Errors.Any(e => e.ErrorMessage.Contains("Invalid email")).ShouldBeTrue();
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
        var result = Rule.Check(rule);

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
        var result = Rule.Check(rule);

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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeFailure();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeFailure();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsBefore_WhenDateTimeIsAfter_ShouldReturnFailure()
    {
        // Arrange
        var now = DateTime.Now;
        var past = now.AddDays(-1);
        var rule = RuleSet.IsBefore(now, past);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeFailure();
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
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsAfter_WhenDateTimeIsBefore_ShouldReturnFailure()
    {
        // Arrange
        var now = DateTime.Now;
        var future = now.AddDays(1);
        var rule = RuleSet.IsAfter(now, future);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeFailure();
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
        var result = Rule.Check(rule);

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
    [InlineData("sample", 1, 4, false)]
    // [InlineData("sample", 1, 5, false)]
    public void HasStringLength_WhenStringLengthIsOutOfRange_ShouldReturnFailure(string value, int min, int max, bool shouldBeValid)
    {
        // Arrange
        var rule = RuleSet.HasStringLength(value, min, max);

        // Act
        var result = Rule.Check(rule);

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
        var result = Rule.Check(rule);

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

        Rule.Check(ruleStart).ShouldBeSuccess();
        Rule.Check(ruleEnd).ShouldBeSuccess();
    }

    // String Validation - Null or Empty
    [Theory]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("NotEmpty", false)]
    public void IsNullOrEmpty_ShouldValidateNullOrEmptyStrings(string value, bool shouldSucceed)
    {
        var rule = RuleSet.IsEmpty(value);
        var result = Rule.Check(rule);

        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    // Null Checks
    [Fact]
    public void IsNull_WhenValueIsNull_ShouldReturnSuccess()
    {
        var rule = RuleSet.IsNull<PersonStub>(null);
        var result = Rule.Check(rule);

        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsNotNull_WhenValueIsNotNull_ShouldReturnSuccess()
    {
        var rule = RuleSet.IsNotNull("non-null");
        var result = Rule.Check(rule);

        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsNotNull_WhenValueIsNull_ShouldReturnFailure()
    {
        var rule = RuleSet.IsNotNull<string>(null);
        var result = Rule.Check(rule);

        result.ShouldBeFailure();
    }

    // Collection - IsEmpty and IsNotEmpty
    [Fact]
    public void IsEmpty_WhenCollectionIsEmpty_ShouldReturnSuccess()
    {
        var emptyCollection = new List<int>();
        var rule = RuleSet.IsEmpty(emptyCollection);
        var result = Rule.Check(rule);

        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsNotEmpty_WhenCollectionHasItems_ShouldReturnSuccess()
    {
        var nonEmptyCollection = new List<int> { 1 };
        var rule = RuleSet.IsNotEmpty(nonEmptyCollection);
        var result = Rule.Check(rule);

        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsNotEmpty_WhenStringIsEmpty_ShouldReturnFailure()
    {
        var rule = RuleSet.IsNotEmpty(string.Empty);
        var result = Rule.Check(rule);

        result.ShouldBeFailure();
    }

    [Fact]
    public void IsNotEmpty_WhenCollectionIsEmpty_ShouldReturnFailure()
    {
        var emptyCollection = Array.Empty<int>();
        var rule = RuleSet.IsNotEmpty(emptyCollection);
        var result = Rule.Check(rule);

        result.ShouldBeFailure();
    }

    // Boolean Rules
    [Fact]
    public void IsTrue_WhenValueIsTrue_ShouldReturnSuccess()
    {
        var rule = RuleSet.IsTrue(true);
        var result = Rule.Check(rule);

        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsTrue_WhenValueIsFalse_ShouldReturnFailure()
    {
        var rule = RuleSet.IsTrue(false);
        var result = Rule.Check(rule);

        result.ShouldBeFailure();
        result.Errors.First().Message.ShouldBe("Value must be true");
    }

    [Fact]
    public void IsFalse_WhenValueIsFalse_ShouldReturnSuccess()
    {
        var rule = RuleSet.IsFalse(false);
        var result = Rule.Check(rule);

        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsFalse_WhenValueIsTrue_ShouldReturnFailure()
    {
        var rule = RuleSet.IsFalse(true);
        var result = Rule.Check(rule);

        result.ShouldBeFailure();
        result.Errors.First().Message.ShouldBe("Value must be false");
    }

    // String Manipulation Rules Tests
    [Theory]
    [InlineData("Hello World", "Hello", StringComparison.Ordinal, true)]
    [InlineData("Hello World", "HELLO", StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("Hello World", "Goodbye", StringComparison.Ordinal, false)]
    public void StartsWith_ShouldCheckPrefixCorrectly(string value, string prefix, StringComparison comparison, bool shouldSucceed)
    {
        var rule = RuleSet.StartsWith(value, prefix, comparison);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    [Theory]
    [InlineData("Hello World", "World", StringComparison.Ordinal, true)]
    [InlineData("Hello World", "WORLD", StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("Hello World", "Earth", StringComparison.Ordinal, false)]
    public void EndsWith_ShouldCheckSuffixCorrectly(string value, string suffix, StringComparison comparison, bool shouldSucceed)
    {
        var rule = RuleSet.EndsWith(value, suffix, comparison);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    [Theory]
    [InlineData("test", new[] { "test", "sample" }, StringComparison.Ordinal, true)]
    [InlineData("TEST", new[] { "test", "sample" }, StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("other", new[] { "test", "sample" }, StringComparison.Ordinal, false)]
    public void TextIn_ShouldCheckAllowedValuesCorrectly(string value, string[] allowedValues, StringComparison comparison, bool shouldSucceed)
    {
        var rule = RuleSet.TextIn(value, allowedValues, comparison);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    [Theory]
    [InlineData("test", new[] { "forbidden", "banned" }, StringComparison.Ordinal, true)]
    [InlineData("TEST", new[] { "test", "banned" }, StringComparison.OrdinalIgnoreCase, false)]
    public void TextNotIn_ShouldCheckDisallowedValuesCorrectly(string value, string[] disallowedValues, StringComparison comparison, bool shouldSucceed)
    {
        var rule = RuleSet.TextNotIn(value, disallowedValues, comparison);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    // Numeric Range Tests
    [Theory]
    [InlineData(5, new[] { 1, 5, 10 }, true)]
    [InlineData(7, new[] { 1, 5, 10 }, false)]
    public void NumericIn_ShouldCheckAllowedValuesCorrectly(int value, int[] allowedValues, bool shouldSucceed)
    {
        var rule = RuleSet.NumericIn(value, allowedValues);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    [Theory]
    [InlineData(5, new[] { 1, 2, 3 }, true)]
    [InlineData(2, new[] { 1, 2, 3 }, false)]
    public void NumericNotIn_ShouldCheckDisallowedValuesCorrectly(int value, int[] disallowedValues, bool shouldSucceed)
    {
        var rule = RuleSet.NumericNotIn(value, disallowedValues);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    // Time Range Tests
    [Fact]
    public void IsInRange_WithTimeOnly_WhenInRange_ShouldReturnSuccess()
    {
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);
        var value = new TimeOnly(12, 0);
        var rule = RuleSet.IsInRange(value, start, end);

        var result = Rule.Check(rule);
        result.ShouldBeSuccess();
    }

    [Fact]
    public void IsInRelativeRange_WithTimeOnly_WhenWithinRange_ShouldReturnSuccess()
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var value = now.Add(TimeUnit.Minute, 3);
        var rule = RuleSet.IsInRelativeRange(value, TimeUnit.Minute, 5, DateTimeDirection.Future);

        var result = Rule.Check(rule);
        result.ShouldBeSuccess();
    }

    // Collection Rule Tests
    [Fact]
    public void None_WhenNoElementsMatchPredicate_ShouldReturnSuccess()
    {
        var numbers = new[] { 1, 2, 3 };
        var rule = RuleSet.None(numbers, num => RuleSet.GreaterThan(num, 5));

        var result = Rule.Check(rule);
        result.ShouldBeSuccess();
    }

    [Fact]
    public void None_WhenPredicateMatchesAnItem_ShouldReturnFailure()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var rule = RuleSet.None(numbers, num => RuleSet.Equal(num, 3));

        var result = Rule.Check(rule);
        result.ShouldBeFailure();
    }

    // Full Text Search Tests
    [Theory]
    [InlineData("This is an important document", "important", StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("This is a document", "important", StringComparison.OrdinalIgnoreCase, false)]
    public void FullTextSearch_ShouldFindMatchesCorrectly(string text, string searchTerm, StringComparison comparison, bool shouldSucceed)
    {
        var rule = RuleSet.FullTextSearch(text, searchTerm, comparison);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    // Enum Tests
    [Fact]
    public void HasValues_WhenEnumValueIsAllowed_ShouldReturnSuccess()
    {
        var value = TestEnum.Value1;
        var allowedValues = new[] { TestEnum.Value1, TestEnum.Value2 };
        var rule = RuleSet.HasValues(value, allowedValues);

        var result = Rule.Check(rule);
        result.ShouldBeSuccess();
    }

    [Fact]
    public void HasValues_WhenEnumValueNotAllowed_ShouldReturnFailure()
    {
        var value = TestEnum.Value3;
        var allowedValues = new[] { TestEnum.Value1, TestEnum.Value2 };
        var rule = RuleSet.HasValues(value, allowedValues);

        var result = Rule.Check(rule);
        result.ShouldBeFailure();
    }

    // Add this at class level if not already present
    private enum TestEnum
    {
        Value1,
        Value2,
        Value3
    }

    // Additional String Tests
    [Theory]
    [InlineData("Hello World", "Earth", StringComparison.Ordinal, true)]
    [InlineData("Hello World", "hello", StringComparison.OrdinalIgnoreCase, false)]
    public void DoesNotContain_ShouldCheckSubstringCorrectly(string value, string substring, StringComparison comparison, bool shouldSucceed)
    {
        var rule = RuleSet.DoesNotContain(value, substring, comparison);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    [Theory]
    [InlineData("Hello World", "Earth", StringComparison.Ordinal, true)]
    [InlineData("Hello World", "Hello", StringComparison.Ordinal, false)]
    public void DoesNotStartWith_ShouldCheckPrefixCorrectly(string value, string prefix, StringComparison comparison, bool shouldSucceed)
    {
        var rule = RuleSet.DoesNotStartWith(value, prefix, comparison);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    [Theory]
    [InlineData("Hello World", "Earth", StringComparison.Ordinal, true)]
    [InlineData("Hello World", "World", StringComparison.Ordinal, false)]
    public void DoesNotEndWith_ShouldCheckSuffixCorrectly(string value, string suffix, StringComparison comparison, bool shouldSucceed)
    {
        var rule = RuleSet.DoesNotEndWith(value, suffix, comparison);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    // Numeric Range Tests
    [Theory]
    [InlineData(5, 1, 10, true)]
    [InlineData(0, 1, 10, false)]
    [InlineData(11, 1, 10, false)]
    public void NumericRange_ShouldValidateRangeCorrectly(int value, int min, int max, bool shouldSucceed)
    {
        var rule = RuleSet.NumericRange(value, min, max);
        var result = Rule.Check(rule);
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    // Generic In/NotIn Tests
    [Fact]
    public void In_WhenValueInAllowedValues_ShouldReturnSuccess()
    {
        var value = "test";
        var allowed = new[] { "sample", "test", "example" };
        var rule = RuleSet.In(value, allowed);

        var result = Rule.Check(rule);
        result.ShouldBeSuccess();
    }

    [Fact]
    public void In_WhenValueNotInAllowedValues_ShouldReturnFailure()
    {
        var value = "other";
        var allowed = new[] { "sample", "test", "example" };
        var rule = RuleSet.In(value, allowed);

        var result = Rule.Check(rule);
        result.ShouldBeFailure();
    }

    [Fact]
    public void NotIn_WhenValueNotInDisallowedValues_ShouldReturnSuccess()
    {
        var value = "valid";
        var disallowed = new[] { "invalid", "blocked" };
        var rule = RuleSet.NotIn(value, disallowed);

        var result = Rule.Check(rule);
        result.ShouldBeSuccess();
    }

    [Fact]
    public void NotIn_WhenValueInDisallowedValues_ShouldReturnFailure()
    {
        var value = "invalid";
        var disallowed = new[] { "invalid", "blocked" };
        var rule = RuleSet.NotIn(value, disallowed);

        var result = Rule.Check(rule);
        result.ShouldBeFailure();
    }

    // Additional Time Range Tests
    [Fact]
    public void IsInRange_WithTimeOnly_WhenOutsideRange_ShouldReturnFailure()
    {
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);
        var value = new TimeOnly(8, 0);
        var rule = RuleSet.IsInRange(value, start, end);

        var result = Rule.Check(rule);
        result.ShouldBeFailure();
    }

    [Fact]
    public void IsInRange_WithDateTime_WhenOutsideRange_ShouldReturnFailure()
    {
        // Arrange
        var now = DateTime.Now;
        var start = now.AddDays(1);
        var end = now.AddDays(2);
        var rule = RuleSet.IsInRange(now, start, end);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeFailure();
        result.Errors.First().Message.ShouldBe($"Date must be between {start} and {end}");
    }

    [Fact]
    public void IsInRange_WithTimeOnly_WhenOutsideRangeWithMessage_ShouldReturnFailure()
    {
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);
        var value = new TimeOnly(8, 0);
        var rule = RuleSet.IsInRange(value, start, end);

        var result = Rule.Check(rule);

        result.ShouldBeFailure();
        result.Errors.First().Message.ShouldBe($"Time must be between {start} and {end}");
    }

    [Fact]
    public void IsEmpty_WhenCollectionIsNotEmpty_ShouldReturnFailure()
    {
        var collection = new[] { 1, 2, 3 };
        var rule = RuleSet.IsEmpty(collection);
        var result = Rule.Check(rule);

        result.ShouldBeFailure();
        result.Errors.First().Message.ShouldBe("Value must be empty");
    }
}