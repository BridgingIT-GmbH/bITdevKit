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
public class RuleBuilderTests(RulesFixture fixture) : IClassFixture<RulesFixture>
{
    private readonly RulesFixture fixture = fixture;
    private readonly Faker faker = new();
    private readonly TestValidator validator = [];

    [Fact]
    public void RulesBuilder_When_WithConditionTrue_ShouldExecuteRule()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            "invalid-email",
            20);

        // Act
        var result = Rule.Add()
            .When(person.Age >= 18, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
        result.Errors.First().Message.ShouldContain("Invalid email");
    }

    [Fact]
    public void RulesBuilder_When_WithConditionFalse_ShouldSkipRule()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            "invalid-email",
            15);

        // Act
        var result = Rule.Add()
            .When(person.Age >= 18, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void RulesBuilder_Unless_WithConditionTrue_ShouldSkipRule()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            "invalid-email",
            15);

        // Act
        var result = Rule.Add()
            .Unless(person.Age < 18, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void RulesBuilder_WhenAll_WithAllConditionsTrue_ShouldExecuteRule()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            "test@email.com",
            25);

        var conditions = new[]
        {
            person.Age >= 18,
            !string.IsNullOrEmpty(person.FirstName),
            !string.IsNullOrEmpty(person.Email.Value)
        };

        // Act
        var result = Rule.Add()
            .WhenAll(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeSuccess();
    }

    [Fact]
    public void RulesBuilder_WhenAny_WithSomeConditionsTrue_ShouldExecuteRule()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            "", // Empty last name
            "invalid-email",
            25);

        var conditions = new[]
        {
            string.IsNullOrEmpty(person.LastName),
            person.Age < 18,
            !person.Email.Value.Contains('@')
        };

        // Act
        var result = Rule.Add()
            .WhenAny(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void RulesBuilder_WhenNone_WithNoConditionsTrue_ShouldExecuteRule()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            "test@email.com",
            25);

        var conditions = new[]
        {
            string.IsNullOrEmpty(person.FirstName),
            string.IsNullOrEmpty(person.LastName),
            person.Age < 18
        };

        // Act
        var result = Rule.Add()
            .WhenNone(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeSuccess();
    }

    [Fact]
    public void RulesBuilder_WhenExactly_WithExactNumberTrue_ShouldExecuteRule()
    {
        // Arrange
        var person = new PersonStub(
            "", // Empty first name
            this.faker.Name.LastName(),
            "test@email.com",
            25);

        var conditions = new[]
        {
            string.IsNullOrEmpty(person.FirstName), // true
            string.IsNullOrEmpty(person.LastName), // false
            person.Age < 18 // false
        };

        // Act
        var result = Rule.Add()
            .WhenExactly(1, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeSuccess();
    }

    [Fact]
    public void RulesBuilder_WhenAtLeast_WithMinimumConditionsTrue_ShouldExecuteRule()
    {
        // Arrange
        var person = new PersonStub(
            "", // Empty first name
            "", // Empty last name
            "test@email.com",
            25);

        var conditions = new[]
        {
            string.IsNullOrEmpty(person.FirstName), // true
            string.IsNullOrEmpty(person.LastName), // true
            person.Age < 18 // false
        };

        // Act
        var result = Rule.Add()
            .WhenAtLeast(2, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeSuccess();
    }

    [Fact]
    public void RulesBuilder_WhenAtMost_WithMaximumConditionsTrue_ShouldExecuteRule()
    {
        // Arrange
        var person = new PersonStub(
            "", // Empty first name
            this.faker.Name.LastName(),
            "test@email.com",
            25);

        var conditions = new[]
        {
            string.IsNullOrEmpty(person.FirstName), // true
            string.IsNullOrEmpty(person.LastName), // false
            person.Age < 18 // false
        };

        // Act
        var result = Rule.Add()
            .WhenAtMost(1, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeSuccess();
    }

    [Fact]
    public void RulesBuilder_WhenBetween_WithConditionsInRange_ShouldExecuteRule()
    {
        // Arrange
        var person = new PersonStub(
            "", // Empty first name
            "", // Empty last name
            "test@email.com",
            25);

        var conditions = new[]
        {
            string.IsNullOrEmpty(person.FirstName), // true
            string.IsNullOrEmpty(person.LastName), // true
            person.Age < 18 // false
        };

        // Act
        var result = Rule.Add()
            .WhenBetween(1, 2, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert

        result.ShouldBeSuccess();
    }

    [Fact]
    public void RulesBuilder_ContinueOnFailure_ShouldCollectAllErrors()
    {
        // Arrange
        var person = new PersonStub(
            "", // Empty first name
            "", // Empty last name
            "invalid-email",
            16); // Underage

        // Act
        var result = Rule.Add()
            .Add(RuleSet.IsNotEmpty(person.FirstName))
            .Add(RuleSet.IsNotEmpty(person.LastName))
            .Add(RuleSet.IsValidEmail(person.Email.Value))
            .Add(RuleSet.GreaterThanOrEqual(person.Age, 18))
            .ContinueOnFailure()
            .Check();

        // Assert

        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(4); // All rules should fail
    }

    [Fact]
    public void Sync_Rule_Should_Pass_When_Predicate_Returns_True()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 25);

        // Act
        var result = Rule.Add()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Check();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Sync_Rule_Should_Fail_When_Predicate_Returns_False()
    {
        // Arrange
        var person = new PersonStub(string.Empty, "Doe", "john@example.com", 25);

        // Act
        var result = Rule.Add()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Check();

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public void Sync_Rule_Should_Use_Custom_Message()
    {
        // Arrange
        var person = new PersonStub(string.Empty, "Doe", "john@example.com", 25);
        var message = "First name is required";

        // Act
        var result = Rule.Add()
            .Add(() => !string.IsNullOrEmpty(person.FirstName), message)
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.ShouldContain(e => e.Message == message);
    }

    [Fact]
    public void Multiple_Sync_Rules_Should_Pass_When_All_Valid()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 25);

        // Act
        var result = Rule.Add()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Add(() => person.Age >= 18)
            .Add(() => person.Email.Value.Contains("@"))
            .Check();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Multiple_Sync_Rules_Should_Fail_When_Age_Below_Minimum()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 15);

        // Act
        var result = Rule.Add()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Add(() => person.Age >= 18)
            .Add(() => person.Email.Value.Contains('@'))
            .Check();

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public void Should_Handle_Conditional_Rules()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 15);
        var shouldValidateAge = false;

        // Act
        var result = Rule.Add()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            .When(shouldValidateAge,
                builder =>
                    builder.Add(() => person.Age >= 18))
            .Check();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task Async_Rule_Should_Pass_When_Email_Valid()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 25);

        // Act
        var result = await Rule.Add()
            .Add(async _ => await Task.FromResult(!string.IsNullOrEmpty(person.Email.Value)))
            .CheckAsync();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task Async_Rule_Should_Fail_When_Email_Invalid()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", string.Empty, 25);

        // Act
        var result = await Rule.Add()
            .Add(async _ => await Task.FromResult(!string.IsNullOrEmpty(person.Email.Value)))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task Should_Mix_Sync_And_Async_Rules()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 25);

        // Act
        var result = await Rule.Add()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Add(async token => await IsEmailUniqueAsync(person.Email.Value, token))
            .CheckAsync();

        // Assert
        result.ShouldBeSuccess();

        return;

        async Task<bool> IsEmailUniqueAsync(string email, CancellationToken token)
        {
            await Task.Delay(1, token);

            return email.Contains("@");
        }
    }

    [Fact]
    public async Task Should_Support_Cancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act & Assert
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await Rule.Add()
                .Add(async token => await LongRunningCheckAsync(token))
                .CheckAsync(cancellationToken: cts.Token);
        });

        return;

        async Task<bool> LongRunningCheckAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);

            return true;
        }
    }

    [Fact]
    public void Should_Collect_All_Failures_With_ContinueOnFailure()
    {
        // Arrange
        var person = new PersonStub(string.Empty, string.Empty, "invalid-email", 15);

        // Act
        var result = Rule.Add()
            .Add(() => !string.IsNullOrEmpty(person.FirstName), "First name required")
            .Add(() => !string.IsNullOrEmpty(person.LastName), "Last name required")
            .Add(() => person.Age >= 18, "Must be adult")
            .Add(() => person.Email.Value.Contains("@"), "Invalid email")
            .ContinueOnFailure()
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(4);
    }

    [Fact]
    public void Should_Support_Location_Validation()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 25);
        var location = LocationStub.Create("Home", "123 Main St", null, "12345", "City", "Country");
        person.AddLocation(location);

        // Act
        var result = Rule.Add()
            .Add(() => person.Locations.Any())
            .Add(() => person.Locations.All(l => !string.IsNullOrEmpty(l.City)))
            .Check();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Should_Support_Complex_Person_Validation()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 25);

        // Act
        var result = Rule.Add()
            .Add(() => !string.IsNullOrEmpty(person.FirstName) && !string.IsNullOrEmpty(person.LastName))
            .Add(() => person.Age >= 18 && person.Age < 100)
            .Add(() => person.Email.Value.Contains("@") && !person.Email.Value.Contains(" "))
            .Add(() => person.Nationality == "USA")
            .Check();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void RulesBuilder_WithoutContinueOnFailure_ShouldStopAtFirstError()
    {
        // Arrange
        var person = new PersonStub(
            "", // Empty first name
            "", // Empty last name
            "invalid-email",
            16); // Underage

        // Act
        var result = Rule.Add()
            .Add(RuleSet.IsNotEmpty(person.FirstName))
            .Add(RuleSet.IsNotEmpty(person.LastName))
            .Add(RuleSet.IsValidEmail(person.Email.Value))
            .Add(RuleSet.GreaterThanOrEqual(person.Age, 18))
            .Check();

        // Assert

        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1); // Should stop at first error
    }

    [Fact]
    public void Check_CompletePersonValidation_ShouldValidateAllRules()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            25);
        person.AddLocation(LocationStub.Create(
            "Home",
            this.faker.Address.StreetAddress(),
            this.faker.Address.SecondaryAddress(),
            this.faker.Address.ZipCode(),
            this.faker.Address.City(),
            "USA"));
        person.AddLocation(LocationStub.Create(
            "Work",
            this.faker.Address.StreetAddress(),
            this.faker.Address.SecondaryAddress(),
            this.faker.Address.ZipCode(),
            this.faker.Address.City(),
            "Canada"));

        var rules = Rule.Add()
            .Add(RuleSet.IsNotEmpty(person.FirstName))
            .Add(RuleSet.IsNotEmpty(person.LastName))
            .Add(RuleSet.IsValidEmail(person.Email.Value))
            .Add(RuleSet.GreaterThanOrEqual(person.Age, 18))
            .Add(RuleSet.Equal(person.Nationality, "USA"))
            .Add(RuleSet.IsNotEmpty(person.Locations))
            // All locations must have address and city
            .Add(RuleSet.All(person.Locations,
                location =>
                    RuleSet.IsNotEmpty(location.AddressLine1)))
            .Add(RuleSet.All(person.Locations,
                location =>
                    RuleSet.IsNotEmpty(location.City)))
            // At least one location must be in USA
            .Add(RuleSet.Any(person.Locations,
                location =>
                    RuleSet.Equal(location.Country, "USA")))
            // No locations should have empty postal codes
            .Add(RuleSet.None(person.Locations,
                location =>
                    RuleSet.IsEmpty(location.PostalCode)));

        // Act
        var result = rules.Check();

        // Assert

        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void Check_PersonValidationWithFailures_ShouldCollectAllErrors()
    {
        // Arrange
        var person = new PersonStub(
            "", // Empty first name
            this.faker.Name.LastName(),
            "invalid-email", // Invalid email
            16); // Underage
        person.AddLocation(LocationStub.Create(
            "Home",
            "", // Empty address
            null,
            "", // Empty postal code
            this.faker.Address.City(),
            "Canada")); // Not USA
        person.AddLocation(LocationStub.Create(
            "Work",
            this.faker.Address.StreetAddress(),
            null,
            "", // Empty postal code
            this.faker.Address.City(),
            "UK")); // Not USA

        var rules = Rule.Add()
            .Add(() => !person.LastName.IsNullOrEmpty())
            .Add(RuleSet.IsNotEmpty(person.FirstName))
            .Add(RuleSet.IsValidEmail(person.Email.Value))
            .Add(RuleSet.GreaterThanOrEqual(person.Age, 18))
            .Add(RuleSet.Equal(person.Nationality, "UNK"))
            // All locations must have addresses
            .Add(RuleSet.All(person.Locations,
                location => RuleSet.IsNotEmpty(location.AddressLine1)))
            // At least one location must be in USA
            .Add(RuleSet.Any(person.Locations,
                location => RuleSet.Equal(location.Country, "USA")))
            // No locations should have empty postal codes
            .Add(RuleSet.None(person.Locations,
                location => RuleSet.IsEmpty(location.PostalCode)))
            .ContinueOnFailure();

        // Act
        var result = rules.Check();

        // Assert

        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(7); // FirstName, Email, Age, Nationality, Address, USA location, Postal codes
        result.Errors.Any(e => e.Message.Contains("must not be empty")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("Invalid email")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("greater than or equal to 18")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("must be equal to UNK")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("No element in the collection satisfies the condition")).ShouldBeTrue();
    }

    [Fact]
    public void Check_LocationValidationOnly_ShouldValidateAllLocations()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            this.faker.Internet.Email(),
            25);

        // Add locations with mix of USA and non-USA addresses
        person.AddLocation(LocationStub.Create(
            "Home",
            this.faker.Address.StreetAddress(),
            this.faker.Address.SecondaryAddress(),
            this.faker.Address.ZipCode(),
            this.faker.Address.City(),
            "USA"));
        person.AddLocation(LocationStub.Create(
            "Work",
            this.faker.Address.StreetAddress(),
            this.faker.Address.SecondaryAddress(),
            this.faker.Address.ZipCode(),
            this.faker.Address.City(),
            "Canada"));

        var rules = Rule.Add()
            .Add(RuleSet.IsNotEmpty(person.Locations))
            .Add(RuleSet.HasCollectionSize(person.Locations, 1, 5))
            // All locations must have required fields
            .Add(RuleSet.All(person.Locations,
                location =>
                    RuleSet.IsNotEmpty(location.AddressLine1)))
            .Add(RuleSet.All(person.Locations,
                location =>
                    RuleSet.IsNotEmpty(location.City)))
            .Add(RuleSet.All(person.Locations,
                location =>
                    RuleSet.IsNotEmpty(location.PostalCode)))
            // At least one location must be in USA
            .Add(RuleSet.Any(person.Locations,
                location =>
                    RuleSet.Equal(location.Country, "USA")))
            // No locations should have empty address fields
            .Add(RuleSet.None(person.Locations,
                location =>
                    RuleSet.IsEmpty(location.AddressLine1)))
            .ContinueOnFailure();

        // Act
        var result = rules.Check();

        // Assert

        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void Check_WithFluentValidationAndRules_ShouldValidateCorrectly()
    {
        // Arrange
        var person = new PersonStub(
            "", // Empty first name - fails domain rule
            this.faker.Name.LastName(),
            "invalid-email", // Invalid email - fails both fluent and domain rules
            16); // Underage - fails fluent validation rule
        person.AddLocation(LocationStub.Create(
            "Home",
            this.faker.Address.StreetAddress(),
            null,
            this.faker.Address.ZipCode(),
            this.faker.Address.City(),
            "Canada")); // Not USA

        var rules = Rule.Add()
            // Rules
            .Add(RuleSet.IsNotEmpty(person.FirstName))
            .Add(RuleSet.IsValidEmail(person.Email.Value))
            .Add(RuleSet.Equal(person.Nationality, "UNK"))
            .Add(RuleSet.Any(person.Locations,
                location =>
                    RuleSet.Equal(location.Country, "USA")))
            // FluentValidation rules (validates age >= 18 and email format)
            .Add(RuleSet.Validate(person, this.validator))
            .ContinueOnFailure();

        // Act
        var result = rules.Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(5); // FirstName, Email (domain), USA location, Age (fluent), Email (fluent)

        // Rule errors
        result.Errors.Any(e => e.Message.Contains("must not be empty")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("Invalid email address")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("must be equal to UNK")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("No element in the collection satisfies")).ShouldBeTrue();

        // FluentValidation errors
        result.HasError<FluentValidationError>().ShouldBeTrue();
        var fluentError = result.GetError<FluentValidationError>();
        fluentError.Errors.Any(e => e.ErrorMessage.Contains("Must be 18 or older")).ShouldBeTrue();
        fluentError.Errors.Any(e => e.ErrorMessage.Contains("Invalid email")).ShouldBeTrue();
    }

    [Fact]
    public void Check_WithValidPersonData_ShouldPassAllValidations()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(),
            this.faker.Name.LastName(),
            "valid.email@example.com",
            25);
        person.AddLocation(LocationStub.Create(
            "Home",
            this.faker.Address.StreetAddress(),
            null,
            this.faker.Address.ZipCode(),
            this.faker.Address.City(),
            "USA"));

        var rules = Rule.Add()
            // Rules
            .Add(RuleSet.IsNotEmpty(person.FirstName))
            .Add(RuleSet.IsValidEmail(person.Email.Value))
            .Add(RuleSet.Equal(person.Nationality, "USA"))
            .Add(RuleSet.Any(person.Locations,
                location =>
                    RuleSet.Equal(location.Country, "USA")))
            // FluentValidation rules
            .Add(RuleSet.Validate(person, this.validator))
            .ContinueOnFailure();

        // Act
        var result = rules.Check();

        // Assert

        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.HasError<FluentValidationError>().ShouldBeFalse();
    }

    [Fact]
    public void Check_WithOnlyFluentValidationFailures_ShouldCollectFluentErrors()
    {
        // Arrange
        var person = new PersonStub(
            this.faker.Name.FirstName(), // Valid
            this.faker.Name.LastName(), // Valid
            "invalid-email", // Invalid - fails fluent validation
            16); // Underage - fails fluent validation
        person.AddLocation(LocationStub.Create(
            "Home",
            this.faker.Address.StreetAddress(),
            null,
            this.faker.Address.ZipCode(),
            this.faker.Address.City(),
            "USA"));

        var rules = Rule.Add()
            // Rules - all should pass
            .Add(RuleSet.IsNotEmpty(person.FirstName))
            .Add(RuleSet.IsNotEmpty(person.LastName))
            .Add(RuleSet.Any(person.Locations,
                location =>
                    RuleSet.Equal(location.Country, "USA")))
            // FluentValidation rules - should fail
            .Add(RuleSet.Validate(person, this.validator))
            .ContinueOnFailure();

        // Act
        var result = rules.Check();

        // Assert

        result.IsFailure.ShouldBeTrue();
        result.HasError<FluentValidationError>().ShouldBeTrue();
        var fluentError = result.GetError<FluentValidationError>();
        fluentError.Errors.Any(e => e.ErrorMessage.Contains("Must be 18 or older")).ShouldBeTrue();
        fluentError.Errors.Any(e => e.ErrorMessage.Contains("Invalid email")).ShouldBeTrue();
        result.Errors.Count.ShouldBe(1); // Only the FluentValidation error group
    }

    [Fact]
    public void When_WithFuncCondition_ShouldExecuteRuleWhenTrue()
    {
        // Arrange
        var person = new PersonStub("", "Doe", "invalid-email", 20);

        // Act
        var result = Rule.Add()
            .When(() => person.Age >= 18, RuleSet.IsNotEmpty(person.FirstName))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void When_WithMultipleRules_ShouldExecuteAllWhenConditionIsTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);

        // Act
        var result = Rule.Add()
            .When(() => person.Age >= 18,
                RuleSet.IsNotEmpty(person.FirstName),
                RuleSet.IsNotEmpty(person.LastName))
            .ContinueOnFailure()
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void When_WithBuilderAction_ShouldExecuteAllRulesWhenConditionIsTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);

        // Act
        var result = Rule.Add()
            .When(() => person.Age >= 18,
                builder => builder
                    .Add(RuleSet.IsNotEmpty(person.FirstName))
                    .Add(RuleSet.IsNotEmpty(person.LastName)))
            .ContinueOnFailure()
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WhenAsync_WithSingleRule_ShouldExecuteWhenConditionIsTrue()
    {
        // Arrange
        var person = new PersonStub("", "Doe", "invalid-email", 20);

        // Act
        var result = await (await Rule.Add()
                .WhenAsync(
                    async _ => await Task.FromResult(person.Age >= 18),
                    async _ => await Task.FromResult(RuleSet.IsNotEmpty(person.FirstName))))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenAsync_WithMultipleRules_ShouldExecuteAllWhenConditionIsTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);

        // Act
        var result = await (await Rule.Add()
                .WhenAsync(
                    async _ => await Task.FromResult(person.Age >= 18),
                    async _ => await Task.FromResult(RuleSet.IsNotEmpty(person.FirstName)),
                    async _ => await Task.FromResult(RuleSet.IsNotEmpty(person.LastName))))
            .ContinueOnFailure()
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WhenAsync_WithBuilderAction_ShouldExecuteAllRulesWhenConditionIsTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);

        // Act
        var result = await Rule.Add()
            .WhenAsync(
                async _ => await Task.FromResult(person.Age >= 18),
                builder => builder
                    .Add(RuleSet.IsNotEmpty(person.FirstName))
                    .Add(RuleSet.IsNotEmpty(person.LastName)))
            .ContinueOnFailure()
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WhenAllAsync_WithSingleRule_ShouldExecuteWhenAllConditionsAreTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);
        var conditions = new Func<CancellationToken, Task<bool>>[]
        {
            async _ => await Task.FromResult(person.Age >= 18),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.FirstName)),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.LastName))
        };

        // Act
        var result = await (await Rule.Add()
                .WhenAllAsync(conditions,
                    async _ => await Task.FromResult(RuleSet.IsValidEmail(person.Email.Value))))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenAll_WithPredicates_ShouldExecuteWhenAllTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);
        var conditions = new[]
        {
            () => person.Age >= 18,
            () => string.IsNullOrEmpty(person.FirstName),
            () => string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenAll(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenAny_WithPredicates_ShouldExecuteWhenAnyTrue()
    {
        // Arrange
        var person = new PersonStub("John", "", "invalid-email", 15);
        var conditions = new[]
        {
            () => person.Age >= 18,
            () => string.IsNullOrEmpty(person.FirstName),
            () => string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenAny(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenAnyAsync_WithSingleRule_ShouldExecuteWhenAnyConditionIsTrue()
    {
        // Arrange
        var person = new PersonStub("John", "", "invalid-email", 20);
        var conditions = new Func<CancellationToken, Task<bool>>[]
        {
            async _ => await Task.FromResult(person.Age >= 18),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.FirstName)),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.LastName))
        };

        // Act
        var result = await (await Rule.Add()
                .WhenAnyAsync(conditions,
                    async _ => await Task.FromResult(RuleSet.IsValidEmail(person.Email.Value))))
            .ContinueOnFailure()
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenNone_WithPredicates_ShouldExecuteWhenNoneTrue()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "invalid-email", 25);
        var conditions = new[]
        {
            () => person.Age < 18,
            () => string.IsNullOrEmpty(person.FirstName),
            () => string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenNone(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenNoneAsync_WithSingleRule_ShouldExecuteWhenNoConditionsAreTrue()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "invalid-email", 25);
        var conditions = new Func<CancellationToken, Task<bool>>[]
        {
            async _ => await Task.FromResult(person.Age < 18),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.FirstName)),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.LastName))
        };

        // Act
        var result = await (await Rule.Add()
                .WhenNoneAsync(conditions,
                    async _ => await Task.FromResult(RuleSet.IsValidEmail(person.Email.Value))))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenAll_WithBooleanConditions_ShouldExecuteWhenAllTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);
        var conditions = new[]
        {
            person.Age >= 18,
            string.IsNullOrEmpty(person.FirstName),
            string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenAll(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenAny_WithBooleanConditions_ShouldExecuteWhenAnyTrue()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "invalid-email", 20);
        var conditions = new[]
        {
            person.Age >= 18,
            string.IsNullOrEmpty(person.FirstName),
            string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenAny(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .ContinueOnFailure()
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenNone_WithBooleanConditions_ShouldExecuteWhenNoneTrue()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "invalid-email", 25);
        var conditions = new[]
        {
            person.Age < 18,
            string.IsNullOrEmpty(person.FirstName),
            string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenNone(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenExactlyAsync_WithSingleRule_ShouldExecuteWhenExactNumberTrue()
    {
        // Arrange
        var person = new PersonStub("John", "", "invalid-email", 15);
        var conditions = new Func<CancellationToken, Task<bool>>[]
        {
            async _ => await Task.FromResult(person.Age >= 18),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.FirstName)),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.LastName))
        };

        // Act
        var result = await (await Rule.Add()
                .WhenExactlyAsync(1,
                    conditions,
                    async _ => await Task.FromResult(RuleSet.IsValidEmail(person.Email.Value))))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenExactly_WithBooleanConditions_ShouldExecuteWhenExactNumberTrue()
    {
        // Arrange
        var person = new PersonStub("John", "", "invalid-email", 15);
        var conditions = new[]
        {
            person.Age >= 18,
            string.IsNullOrEmpty(person.FirstName),
            string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenExactly(1, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenExactly_WithPredicates_ShouldExecuteWhenExactNumberTrue()
    {
        // Arrange
        var person = new PersonStub("John", "", "invalid-email", 15);
        var conditions = new[]
        {
            () => person.Age >= 18,
            () => string.IsNullOrEmpty(person.FirstName),
            () => string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenExactly(1, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenAtLeastAsync_WithSingleRule_ShouldExecuteWhenMinimumTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);
        var conditions = new Func<CancellationToken, Task<bool>>[]
        {
            async _ => await Task.FromResult(person.Age >= 18),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.FirstName)),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.LastName))
        };

        // Act
        var result = await (await Rule.Add()
                .WhenAtLeastAsync(2,
                    conditions,
                    async _ => await Task.FromResult(RuleSet.IsValidEmail(person.Email.Value))))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenAtLeast_WithBooleanConditions_ShouldExecuteWhenMinimumTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);
        var conditions = new[]
        {
            person.Age >= 18,
            string.IsNullOrEmpty(person.FirstName),
            string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenAtLeast(2, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenAtLeast_WithPredicates_ShouldExecuteWhenMinimumTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);
        var conditions = new[]
        {
            () => person.Age >= 18,
            () => string.IsNullOrEmpty(person.FirstName),
            () => string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenAtLeast(2, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenAtMostAsync_WithSingleRule_ShouldExecuteWhenMaximumTrue()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "invalid-email", 15);
        var conditions = new Func<CancellationToken, Task<bool>>[]
        {
            async _ => await Task.FromResult(person.Age >= 18),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.FirstName)),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.LastName))
        };

        // Act
        var result = await (await Rule.Add()
                .WhenAtMostAsync(1,
                    conditions,
                    async _ => await Task.FromResult(RuleSet.IsValidEmail(person.Email.Value))))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenAtMost_WithBooleanConditions_ShouldExecuteWhenMaximumTrue()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "invalid-email", 15);
        var conditions = new[]
        {
            person.Age >= 18,
            string.IsNullOrEmpty(person.FirstName),
            string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenAtMost(1, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenAtMost_WithPredicates_ShouldExecuteWhenMaximumTrue()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "invalid-email", 15);
        var conditions = new[]
        {
            () => person.Age >= 18,
            () => string.IsNullOrEmpty(person.FirstName),
            () => string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenAtMost(1, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenBetweenAsync_WithSingleRule_ShouldExecuteWhenCountInRange()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);
        var conditions = new Func<CancellationToken, Task<bool>>[]
        {
            async _ => await Task.FromResult(person.Age >= 18),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.FirstName)),
            async _ => await Task.FromResult(string.IsNullOrEmpty(person.LastName))
        };

        // Act
        var result = await (await Rule.Add()
                .WhenBetweenAsync(2,
                    3,
                    conditions,
                    async _ => await Task.FromResult(RuleSet.IsValidEmail(person.Email.Value))))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenBetween_WithPredicates_ShouldExecuteWhenCountInRange()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);
        var conditions = new[]
        {
            () => person.Age >= 18,
            () => string.IsNullOrEmpty(person.FirstName),
            () => string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenBetween(2, 3, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenAsync_WithAsyncPredicate_ShouldExecuteRuleWhenTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);

        // Act
        var result = await (await Rule.Add()
                .WhenAsync(
                    async _ => await Task.FromResult(person.Age >= 18),
                    async _ => await Task.FromResult(!string.IsNullOrEmpty(person.FirstName)),
                    "First name required"))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UnlessAsync_WithAsyncPredicate_ShouldExecuteRuleWhenFalse()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 15);

        // Act
        var result = await (await Rule.Add()
                .UnlessAsync(
                    async _ => await Task.FromResult(person.Age >= 18),
                    async _ => await Task.FromResult(!string.IsNullOrEmpty(person.FirstName)),
                    "First name required"))
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenAsync_WithMultipleAsyncPredicates_ShouldExecuteAllWhenConditionTrue()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);

        // Act
        var result = await (await Rule.Add()
                .WhenAsync(
                    async _ => await Task.FromResult(person.Age >= 18),
                    async _ => await Task.FromResult(!string.IsNullOrEmpty(person.FirstName)),
                    async _ => await Task.FromResult(!string.IsNullOrEmpty(person.LastName))))
            .ContinueOnFailure()
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UnlessAsync_WithMultipleAsyncPredicates_ShouldExecuteAllWhenConditionFalse()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 15);

        // Act
        var result = await (await Rule.Add()
                .UnlessAsync(
                    async _ => await Task.FromResult(person.Age >= 18),
                    async _ => await Task.FromResult(!string.IsNullOrEmpty(person.FirstName)),
                    async _ => await Task.FromResult(!string.IsNullOrEmpty(person.LastName))))
            .ContinueOnFailure()
            .CheckAsync();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void WhenBetween_WithBooleanConditions_ShouldExecuteWhenCountInRange()
    {
        // Arrange
        var person = new PersonStub("", "", "invalid-email", 20);
        var conditions = new[]
        {
            person.Age >= 18,
            string.IsNullOrEmpty(person.FirstName),
            string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenBetween(2, 3, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void WhenBetween_WithBooleanConditions_ShouldSkipWhenCountOutOfRange()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "invalid-email", 15);
        var conditions = new[]
        {
            person.Age >= 18,
            string.IsNullOrEmpty(person.FirstName),
            string.IsNullOrEmpty(person.LastName)
        };

        // Act
        var result = Rule.Add()
            .WhenBetween(2, 3, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Check();

        // Assert
        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Support_Multiple_Async_Rules_With_Cancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act & Assert
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await (await Rule.Add()
                    .WhenAsync(
                        async token => await DelayedCheckAsync(token),
                        async token => await DelayedCheckAsync(token)))
                .CheckAsync(cancellationToken: cts.Token);
        });

        return;

        async Task<bool> DelayedCheckAsync(CancellationToken token)
        {
            await Task.Delay(100, token);

            return true;
        }
    }

    [Fact]
    public void Filter_WithNoRules_ShouldReturnAllItems()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 25),
            new PersonStub("Jane", "Doe", "jane@example.com", 30)
        };

        // Act
        var result = Rule.Add()
            .Filter(items);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Count().ShouldBe(2);
    }

    [Fact]
    public void Filter_WithRules_ShouldReturnMatchingItems()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 25),
            new PersonStub("Jane", "", "invalid-email", 15),
            new PersonStub("Bob", "Smith", "bob@example.com", 35)
        };

        // Act
        var result = Rule.Add()
            .Add<PersonStub>(p => !string.IsNullOrEmpty(p.LastName))
            .Add<PersonStub>(p => RuleSet.Contains(p.Email.Value, "@"))
            .Add<PersonStub>(p => p.Age >= 18)
            .Filter(items);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Count().ShouldBe(2);
        result.Value.All(p => !string.IsNullOrEmpty(p.LastName) &&
                             p.Email.Value.Contains("@") &&
                             p.Age >= 18).ShouldBeTrue();
    }

    [Fact]
    public async Task FilterAsync_WithRules_ShouldReturnMatchingItems()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 25),
            new PersonStub("Jane", "", "invalid-email", 15),
            new PersonStub("Bob", "Smith", "bob@example.com", 35)
        };

        // Act
        var result = await Rule.Add()
            .Add<PersonStub>(async (p, _) => await Task.FromResult(!string.IsNullOrEmpty(p.LastName)))
            .Add<PersonStub>((p, _) => RuleSet.Contains(p.Email.Value, "@"))
            .Add<PersonStub>(async (p, _) => await Task.FromResult(p.Age >= 18))
            .FilterAsync(items);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Count().ShouldBe(2);
        result.Value.All(p => !string.IsNullOrEmpty(p.LastName) &&
                             p.Email.Value.Contains("@") &&
                             p.Age >= 18).ShouldBeTrue();
    }

    [Fact]
    public void Switch_WithNoRules_ShouldProcessAllItemsWithMatchHandler()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 25),
            new PersonStub("Jane", "Doe", "jane@example.com", 30)
        };
        var matchCount = 0;
        var unmatchCount = 0;

        // Act
        var result = Rule.Add()
            .Switch(items,
                matched =>
                {
                    matchCount = matched.Count();
                    return Result.Success();
                },
                unmatched =>
                {
                    unmatchCount = unmatched.Count();
                    return Result.Success();
                });

        // Assert
        result.ShouldBeSuccess();
        matchCount.ShouldBe(2);
        unmatchCount.ShouldBe(0);
    }

    [Fact]
    public void Switch_WithRules_ShouldSplitItemsCorrectly()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 25),
            new PersonStub("Jane", "", "invalid-email", 15),
            new PersonStub("Bob", "Smith", "bob@example.com", 35)
        };
        var validCount = 0;
        var invalidCount = 0;

        // Act
        var result = Rule.Add()
            .Add<PersonStub>(p => !string.IsNullOrEmpty(p.LastName))
            .Add<PersonStub>(p => p.Email.Value.Contains("@"))
            .Add<PersonStub>(p => p.Age >= 18)
            .Switch(items,
                valid =>
                {
                    validCount = valid.Count();
                    return Result.Success();
                },
                invalid =>
                {
                    invalidCount = invalid.Count();
                    return Result.Success();
                });

        // Assert
        result.ShouldBeSuccess();
        validCount.ShouldBe(2);
        invalidCount.ShouldBe(1);
    }

    [Fact]
    public async Task SwitchAsync_WithRules_ShouldSplitItemsCorrectly()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 25),
            new PersonStub("Jane", "", "invalid-email", 15),
            new PersonStub("Bob", "Smith", "bob@example.com", 35)
        };
        var validCount = 0;
        var invalidCount = 0;

        // Act
        var result = await Rule.Add()
            .Add<PersonStub>(async (p, _) => await Task.FromResult(!string.IsNullOrEmpty(p.LastName)))
            .Add<PersonStub>(async (p, _) => await Task.FromResult(p.Email.Value.Contains("@")))
            .Add<PersonStub>(async (p, _) => await Task.FromResult(p.Age >= 18))
            .SwitchAsync(items,
                async valid =>
                {
                    validCount = valid.Count();
                    return await Task.FromResult(Result.Success());
                },
                async invalid =>
                {
                    invalidCount = invalid.Count();
                    return await Task.FromResult(Result.Success());
                });

        // Assert
        result.ShouldBeSuccess();
        validCount.ShouldBe(2);
        invalidCount.ShouldBe(1);
    }

    [Fact]
    public async Task Switch_WithHandlerErrors_ShouldReturnCombinedFailure()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 25),
            new PersonStub("Jane", "", "invalid-email", 15)
        };

        // Act
        var result = await Rule.Add()
            //.Add(RuleSet.IsValidEmail(p => p.Email.Value))
            .Add<PersonStub>(async (p, _) => await Task.FromResult(p.Email.Value.Contains("@")))
            .SwitchAsync(items,
                _ => Result.Success("Valid handler error"),
                _ => Result.Failure("Invalid handler error")
                    .WithError(new ValidationError("incorrect email")));

        // Assert
        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1);
        result.ShouldContainMessage("Invalid handler error");
        // result.Errors.Select(e => e.Message)
        //     .ShouldContain(new[] { "Valid handler error", "Invalid handler error" });
    }

    [Fact]
    public async Task SwitchAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 25)
        };
        using var cts = new CancellationTokenSource();

        // Act & Assert
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await Rule.Add()
                .Add(async token => { await Task.Delay(1000, token); return Result.Success(); })
                .SwitchAsync(items,
                    async _ => await Task.FromResult(Result.Success()),
                    async _ => await Task.FromResult(Result.Success()),
                    cts.Token);
        });
    }

    [Fact]
    public void Filter_WithRuleSetRule_ShouldReturnMatchingItems()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "", "john@example.com", 25),
            new PersonStub("Jane", "Doe", "invalid-email", 15),
            new PersonStub("Bob", "Smith", "bob@example.com", 35)
        };

        // Act
        var result = Rule.Add()
            .Add<PersonStub>(p => RuleSet.IsValidEmail(p.Email.Value))
            .Add<PersonStub>(p => RuleSet.GreaterThanOrEqual(p.Age, 18))
            .Add<PersonStub>(p => RuleSet.IsNotEmpty(p.LastName))
            .Filter(items);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Count().ShouldBe(1); // Only Bob Smith matches all criteria
        var match = result.Value.First();
        match.LastName.ShouldBe("Smith");
        match.Email.Value.ShouldBe("bob@example.com");
        match.Age.ShouldBe(35);
    }

    [Fact]
    public async Task FilterAsync_WithMixedRuleTypes_ShouldReturnMatchingItems()
    {
        // Arrange
        var items = new[]
        {
            new PersonStub("John", "Doe", "john@example.com", 25),
            new PersonStub("", "", "invalid", 15),
            new PersonStub("Bob", "Smith", "bob@example.com", 20)
        };

        // Act
        var result = await Rule.Add()
            .Add<PersonStub>((p, _) => RuleSet.IsValidEmail(p.Email.Value))
            .Add<PersonStub>(async (p, _) => await Task.FromResult(RuleSet.IsNotEmpty(p.FirstName)))
            .Add<PersonStub>((p, _) => RuleSet.IsNotEmpty(p.LastName))
            .Add<PersonStub>((p, _) => RuleSet.NumericRange(p.Age, 20, 30))
            .FilterAsync(items);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Count().ShouldBe(2); // John Doe and Bob Smith match
        result.Value.All(p =>
            !string.IsNullOrEmpty(p.FirstName) &&
            !string.IsNullOrEmpty(p.LastName) &&
            p.Email.Value.Contains("@") &&
            p.Age >= 20 && p.Age <= 30
        ).ShouldBeTrue();
    }
}

[CollectionDefinition(nameof(RuleBuilderCollectionDefinition), DisableParallelization = true)]
public class RuleBuilderCollectionDefinition;