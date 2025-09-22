// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Rules;

using System.Diagnostics.CodeAnalysis;
using Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

[UnitTest("Common")]
[SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
public class RuleTests( /*RulesFixture fixture*/) : IClassFixture<RulesFixture>
{
    // private readonly RulesFixture fixture = fixture;
    private readonly Faker faker = new();

    [Fact]
    public void Check_WithNullRule_ShouldReturnSuccess()
    {
        // Arrange & Act
        FuncRule rule = null;
        var result = Rule.Check(rule);

        // Assert

        result.ShouldBeSuccess();
    }

    [Fact]
    public void Check_WithSuccessfulRule_ShouldReturnSuccess()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Returns(Result.Success());

        // Act
        var result = Rule.Check(rule);

        // Assert

        result.ShouldBeSuccess();
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public void Check_WithFailingRule_ShouldReturnFailureWithRuleError()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        var errorMessage = this.faker.Random.Words();
        rule.Message.Returns(errorMessage);
        rule.IsSatisfied().Returns(Result.Failure());

        // Act
        var result = Rule.Check(rule);

        // Assert

        result.ShouldBeFailure();
        result.HasError<RuleError>().ShouldBeTrue();
        var error = result.GetError<RuleError>();
        error.Message.ShouldBe(errorMessage);
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public void Check_WithRuleThrowingException_ShouldPropagateException()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Throws<InvalidOperationException>();

        // Act & Assert
        Should.Throw<RuleException>(() => Rule.Check(rule, true, true));
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public void Check_WithValidationRule_ShouldValidateCorrectly()
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
        var error = result.GetError<FluentValidationError>();
        error.Message.ShouldContain("Must be 18 or older");
        error.Message.ShouldContain("Invalid email");
    }

    [Fact]
    public async Task CheckAsync_WithNullRule_ShouldReturnSuccess()
    {
        // Arrange & Act
        FuncRule rule = null;
        var result = await Rule.CheckAsync(rule);

        // Assert

        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task CheckAsync_WithSuccessfulSyncRule_ShouldReturnSuccess()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Returns(Result.Success());

        // Act
        var result = await Rule.CheckAsync(rule);

        // Assert

        result.ShouldBeSuccess();
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public async Task CheckAsync_WithFailingSyncRule_ShouldReturnFailureWithRuleError()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        var errorMessage = this.faker.Random.Words();
        rule.Message.Returns(errorMessage);
        rule.IsSatisfied().Returns(Result.Failure());

        // Act
        var result = await Rule.CheckAsync(rule);

        // Assert

        result.ShouldBeFailure();
        result.HasError<RuleError>().ShouldBeTrue();
        var error = result.GetError<RuleError>();
        error.Message.ShouldBe(errorMessage);
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public async Task CheckAsync_WithSuccessfulAsyncRule_ShouldReturnSuccess()
    {
        // Arrange
        var rule = new TestAsyncRule(true);

        // Act
        var result = await Rule.CheckAsync(rule);

        // Assert

        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task CheckAsync_WithFailingAsyncRule_ShouldReturnFailureWithRuleError()
    {
        // Arrange
        var rule = new TestAsyncRule(false);

        // Act
        var result = await Rule.CheckAsync(rule);

        // Assert

        result.ShouldBeFailure();
        result.HasError<RuleError>().ShouldBeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var rule = new TestAsyncRule(true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            Rule.CheckAsync(rule, true, true, cts.Token));
    }

    [Fact]
    public void Check_WithNullPerson_ShouldReturnFailure()
    {
        // Arrange
        var rule = new IsAdultRule(null);

        // Act
        var result = Rule.Check(rule);

        // Assert

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
    public void Check_WithUnderagePersons_ShouldReturnFailure(int age)
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            age);
        var rule = new IsAdultRule(person);

        // Act
        var result = Rule.Check(rule);

        // Assert

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
    public void Check_WithAdultPersons_ShouldReturnSuccess(int age)
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            age);
        var rule = new IsAdultRule(person);

        // Act
        var result = Rule.Check(rule);

        // Assert

        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Check_WithMultipleRules_ShouldCombineValidations()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            "invalid-email", // Invalid email
            16); // Underage
        var adultRule = new IsAdultRule(person);
        var emailRule = RuleSet.IsValidEmail(person.Email.Value);

        // Act
        var result = await Rule.CheckAsync(adultRule);
        var emailResult = await Rule.CheckAsync(emailRule);
        var combinedResult = Result.Merge(result, emailResult);

        // Assert
        combinedResult.ShouldBeFailure();
        combinedResult.Errors.Count.ShouldBe(2);
        combinedResult.Errors.Any(e => e.Message.Contains("18 years old")).ShouldBeTrue();
        combinedResult.Errors.Any(e => e.Message.Contains("Invalid email")).ShouldBeTrue();
    }

    [Fact]
    public void Check_WithValidPersonData_ShouldPassAllRules()
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
        var result1 = Rule.Check(adultRule);
        var result2 = Rule.Check(emailRule);
        var result3 = Rule.Check(firstNameRule);
        var result4 = Rule.Check(lastNameRule);
        var combinedResult = Result.Merge(result1, result2, result3, result4);

        // Assert
        combinedResult.ShouldBeSuccess();
        combinedResult.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Check_AsyncValidation_ShouldHandleMultipleRules()
    {
        // Arrange
        var person = new PersonStub(
            "", // Empty name
            "", // Empty surname
            "invalid-email", // Invalid email
            16); // Underage

        var rules = new[]
        {
            new IsAdultRule(person),
            RuleSet.IsValidEmail(person.Email.Value),
            RuleSet.IsNotEmpty(person.FirstName),
            RuleSet.IsNotEmpty(person.LastName)
        };

        // Act
        var results = await Task.WhenAll(rules.Select(rule => Rule.CheckAsync(rule)));
        var combinedResult = Result.Merge(results);

        // Assert
        combinedResult.ShouldBeFailure();
        combinedResult.Errors.Count.ShouldBe(4);
        combinedResult.Errors.Any(e => e.Message.Contains("18 years old")).ShouldBeTrue();
        combinedResult.Errors.Any(e => e.Message.Contains("Invalid email")).ShouldBeTrue();
        combinedResult.Errors.Count(e => e.Message.Contains("must not be empty")).ShouldBe(2);
    }

    [Fact]
    public void Check_WithCancellation_ShouldRespectCancellationToken()
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
            await Rule.CheckAsync(rule, true, true, cts.Token));
    }

    [Fact]
    public void Check_WithFailingRuleAndThrowOnRuleFailure_ShouldThrowException()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Returns(Result.Failure());

        // Act & Assert
        Should.Throw<RuleException>(() => Rule.Check(rule, true));
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public async Task CheckAsync_WithFailingRuleAndThrowOnRuleFailure_ShouldThrowException()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Returns(Result.Failure());

        // Act & Assert
        await Should.ThrowAsync<RuleException>(() => Rule.CheckAsync(rule, true));
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public async Task CheckAsync_WithRuleThrowingExceptionAndThrowOnRuleException_ShouldThrowException()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Throws<InvalidOperationException>();

        // Act & Assert
        await Should.ThrowAsync<RuleException>(() => Rule.CheckAsync(rule, true, true));
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public async Task CheckAsync_WithFailingAsyncRuleAndThrowOnRuleFailure_ShouldThrowException()
    {
        // Arrange
        var rule = Substitute.For<AsyncRuleBase>();
        rule.IsSatisfiedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Failure()));

        // Act & Assert
        await Should.ThrowAsync<RuleException>(() => Rule.CheckAsync(rule, true));
        await rule.Received(1).IsSatisfiedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_WithAsyncRuleThrowingExceptionAndThrowOnRuleException_ShouldThrowException()
    {
        // Arrange
        var rule = Substitute.For<AsyncRuleBase>();
        rule.IsSatisfiedAsync(Arg.Any<CancellationToken>()).Throws<InvalidOperationException>();

        // Act & Assert
        await Should.ThrowAsync<RuleException>(() => Rule.CheckAsync(rule, true, true));
        await rule.Received(1).IsSatisfiedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Throw_WithFailingRule_ShouldThrowException()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Returns(Result.Failure());

        // Act & Assert
        Should.Throw<RuleException>(() => Rule.Throw(rule));
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public async Task ThrowAsync_WithFailingRule_ShouldThrowException()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Returns(Result.Failure());

        // Act & Assert
        await Should.ThrowAsync<RuleException>(() => Rule.ThrowAsync(rule));
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public void Check_WithRuleThrowingExceptionAndThrowOnRuleException_ShouldThrowException()
    {
        // Arrange
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Throws<InvalidOperationException>();

        // Act & Assert
        Should.Throw<RuleException>(() => Rule.Check(rule, true, true));
        rule.Received(1).IsSatisfied();
    }

    [Fact]
    public void Setup_ShouldInitializeDefaultSettings()
    {
        // Act - Settings are initialized in static constructor
        var settings = Rule.Settings;

        // Assert
        settings.ShouldNotBeNull();
        settings.ThrowOnRuleFailure.ShouldBeFalse();
        settings.ThrowOnRuleException.ShouldBeFalse();
    }

    [Fact]
    public void Check_ShouldLogSuccessfulRuleExecution()
    {
        // Arrange
        var logger = Substitute.For<IRuleLogger>();
        Rule.Setup(builder => builder.SetLogger(logger));
        var rule = new IsAdultRule(new PersonStub(this.faker.Name.FirstName(), this.faker.Name.LastName(), this.faker.Internet.Email(), 21));

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.ShouldBeSuccess();
        logger.Received(1).Log(Arg.Any<string>(), "success", rule, result, LogLevel.Debug);
    }

    [Fact]
    public void Check_ShouldLogRuleException()
    {
        // Arrange
        var logger = Substitute.For<IRuleLogger>();
        Rule.Setup(builder => builder.SetLogger(logger));
        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Throws<InvalidOperationException>();

        // Act
        var result = Rule.Check(rule, throwOnRuleException: false);

        // Assert
        result.ShouldBeFailure();
        logger.Received(1).Log(Arg.Any<string>(), "failure", rule, result, LogLevel.Debug);
    }

    [Fact]
    public void Check_WithCustomExceptionFactory_ShouldUseCustomException()
    {
        // Arrange
        var customMessage = "Custom exception message";
        Rule.Setup(builder => builder
            .SetRuleFailureExceptionFactory(r => new RuleException(r, customMessage)));

        var rule = Substitute.For<IRule>();
        rule.IsSatisfied().Returns(Result.Failure());

        // Act & Assert
        var exception = Should.Throw<RuleException>(() => Rule.Check(rule, true));
        exception.Message.ShouldContain(customMessage);
    }

    [Fact]
    public void Setup_WithNullAction_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Rule.Setup(null));
    }

    [Fact]
    public async Task CheckAsync_WithCanceledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var rule = new TestAsyncRule(true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            Rule.CheckAsync(rule, cancellationToken: cts.Token));
    }

    [Fact]
    public void Check_WithNullExpression_ShouldReturnSuccess()
    {
        // Arrange
        Func<bool> expression = null;

        // Act
        var result = Rule.Check(expression);

        // Assert
        result.ShouldBeSuccess();
    }
}

// Helper class for testing async rules
public class TestAsyncRule(bool shouldSucceed) : AsyncRuleBase
{
    public override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(10, cancellationToken); // Simulate async work

        return shouldSucceed ? Result.Success() : Result.Failure();
    }
}