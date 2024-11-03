// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Rules;

using Common;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

[UnitTest("Common")]
public class RulesTests
{
    private readonly Faker faker = new();

    [Fact]
    public void Apply_WithNullRule_ShouldReturnSuccess()
    {
        // Arrange & Act
        var result = Rules.Apply(null);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Apply_WithSuccessfulRule_ShouldReturnSuccess()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.Apply().Returns(Result.Success());

        // Act
        var result = Rules.Apply(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSuccess();
        rule.Received(1).Apply();
    }

    [Fact]
    public void Apply_WithFailingRule_ShouldReturnFailureWithRuleError()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        var errorMessage = this.faker.Random.Words();
        rule.Message.Returns(errorMessage);
        rule.Apply().Returns(Result.Failure());

        // Act
        var result = Rules.Apply(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeFailure();
        result.HasError<RuleError>().ShouldBeTrue();
        var error = result.GetError<RuleError>();
        error.Message.ShouldBe(errorMessage);
        rule.Received(1).Apply();
    }

    [Fact]
    public void Apply_WithRuleThrowingException_ShouldPropagateException()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.Apply().Throws<InvalidOperationException>();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => Rules.Apply(rule));
        rule.Received(1).Apply();
    }

    [Fact]
    public void Apply_WithValidationRule_ShouldValidateCorrectly()
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
        var result = Rules.Apply(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeFailure();
        result.HasError<FluentValidationError>().ShouldBeTrue();
        var error = result.GetError<FluentValidationError>();
        error.Message.ShouldContain("Must be 18 or older");
        error.Message.ShouldContain("Invalid email");
    }

    [Fact]
    public async Task ApplyAsync_WithNullRule_ShouldReturnSuccess()
    {
        // Arrange & Act
        var result = await Rules.ApplyAsync(null);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task ApplyAsync_WithSuccessfulSyncRule_ShouldReturnSuccess()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.Apply().Returns(Result.Success());

        // Act
        var result = await Rules.ApplyAsync(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSuccess();
        rule.Received(1).Apply();
    }

    [Fact]
    public async Task ApplyAsync_WithFailingSyncRule_ShouldReturnFailureWithRuleError()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        var errorMessage = this.faker.Random.Words();
        rule.Message.Returns(errorMessage);
        rule.Apply().Returns(Result.Failure());

        // Act
        var result = await Rules.ApplyAsync(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeFailure();
        result.HasError<RuleError>().ShouldBeTrue();
        var error = result.GetError<RuleError>();
        error.Message.ShouldBe(errorMessage);
        rule.Received(1).Apply();
    }

    [Fact]
    public async Task ApplyAsync_WithSuccessfulAsyncRule_ShouldReturnSuccess()
    {
        // Arrange
        var rule = new TestAsyncRule(true);

        // Act
        var result = await Rules.ApplyAsync(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task ApplyAsync_WithFailingAsyncRule_ShouldReturnFailureWithRuleError()
    {
        // Arrange
        var rule = new TestAsyncRule(false);

        // Act
        var result = await Rules.ApplyAsync(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeFailure();
        result.HasError<RuleError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var rule = new TestAsyncRule(true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            Rules.ApplyAsync(rule, cts.Token));
    }

    [Fact]
    public void Apply_WithNullPerson_ShouldReturnFailure()
    {
        // Arrange
        var rule = new IsAdultRule(null);

        // Act
        var result = Rules.Apply(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
        result.HasError<RuleError>().ShouldBeTrue();
        var error = result.GetError<RuleError>();
        error.Message.ShouldBe("Person must be at least 18 years old");
    }

    [Theory]
    [InlineData(17)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_WithUnderagePersons_ShouldReturnFailure(int age)
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            age);
        var rule = new IsAdultRule(person);

        // Act
        var result = Rules.Apply(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
        result.HasError<RuleError>().ShouldBeTrue();
        var error = result.GetError<RuleError>();
        error.Message.ShouldBe("Person must be at least 18 years old");
    }

    [Theory]
    [InlineData(18)]
    [InlineData(21)]
    [InlineData(99)]
    public void Apply_WithAdultPersons_ShouldReturnSuccess(int age)
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            age);
        var rule = new IsAdultRule(person);

        // Act
        var result = Rules.Apply(rule);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Apply_WithMultipleRules_ShouldCombineValidations()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            "invalid-email",  // Invalid email
            16);  // Underage
        var adultRule = new IsAdultRule(person);
        var emailRule = RuleSet.IsValidEmail(person.Email.Value);

        // Act
        var result = await Rules.ApplyAsync(adultRule);
        var emailResult = await Rules.ApplyAsync(emailRule);
        var combinedResult = Result.Combine(result, emailResult);

        // Assert
        combinedResult.ShouldNotBeNull();
        combinedResult.ShouldBeFailure();
        combinedResult.Errors.Count.ShouldBe(2);
        combinedResult.Errors.Any(e => e.Message.Contains("18 years old")).ShouldBeTrue();
        combinedResult.Errors.Any(e => e.Message.Contains("Invalid email")).ShouldBeTrue();
    }

    [Fact]
    public void Apply_WithValidPersonData_ShouldPassAllRules()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            21);
        var adultRule = new IsAdultRule(person);
        var emailRule = RuleSet.IsValidEmail(person.Email.Value);
        var firstNameRule = RuleSet.IsNotEmpty(person.FirstName);
        var lastNameRule = RuleSet.IsNotEmpty(person.LastName);

        // Act
        var result1 = Rules.Apply(adultRule);
        var result2 = Rules.Apply(emailRule);
        var result3 = Rules.Apply(firstNameRule);
        var result4 = Rules.Apply(lastNameRule);
        var combinedResult = Result.Combine(result1, result2, result3, result4);

        // Assert
        combinedResult.ShouldNotBeNull();
        combinedResult.ShouldBeSuccess();
        combinedResult.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Apply_AsyncValidation_ShouldHandleMultipleRules()
    {
        // Arrange
        var person = new PersonStub(
            "",  // Empty name
            "",  // Empty surname
            "invalid-email",  // Invalid email
            16);  // Underage

        var rules = new IRule[]
        {
            new IsAdultRule(person),
            RuleSet.IsValidEmail(person.Email.Value),
            RuleSet.IsNotEmpty(person.FirstName),
            RuleSet.IsNotEmpty(person.LastName)
        };

        // Act
        var results = await Task.WhenAll(
            rules.Select(rule => Rules.ApplyAsync(rule)));
        var combinedResult = Result.Combine(results);

        // Assert
        combinedResult.ShouldNotBeNull();
        combinedResult.ShouldBeFailure();
        combinedResult.Errors.Count.ShouldBe(4);
        combinedResult.Errors.Any(e => e.Message.Contains("18 years old")).ShouldBeTrue();
        combinedResult.Errors.Any(e => e.Message.Contains("Invalid email")).ShouldBeTrue();
        combinedResult.Errors.Count(e => e.Message.Contains("must not be empty")).ShouldBe(2);
    }

    [Fact]
    public void Apply_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            21);
        var rule = new IsAdultRule(person);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Should.Throw<OperationCanceledException>(async () =>
            await Rules.ApplyAsync(rule, cts.Token));
    }
}

// Helper class for testing async rules
public class TestAsyncRule : AsyncRuleBase
{
    private readonly bool shouldSucceed;

    public TestAsyncRule(bool shouldSucceed)
    {
        this.shouldSucceed = shouldSucceed;
    }

    protected override async Task<Result> ExecuteRuleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(10, cancellationToken); // Simulate async work

        return this.shouldSucceed ? Result.Success() : Result.Failure();
    }
}