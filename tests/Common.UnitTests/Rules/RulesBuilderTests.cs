// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Rules;

using Common;
using Shouldly;
using Xunit;

[UnitTest("Common")]
public class RulesBuilderTests
{
    private readonly Faker faker = new();
    private readonly TestValidator validator = new();

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
        var result = Rules.For()
            .When(person.Age >= 18, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .When(person.Age >= 18, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .Unless(person.Age < 18, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .WhenAll(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .WhenAny(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .WhenNone(conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .WhenExactly(1, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .WhenAtLeast(2, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .WhenAtMost(1, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .WhenBetween(1, 2, conditions, RuleSet.IsValidEmail(person.Email.Value))
            .Apply();

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
        var result = Rules.For()
            .Add(RuleSet.IsNotEmpty(person.FirstName))
            .Add(RuleSet.IsNotEmpty(person.LastName))
            .Add(RuleSet.IsValidEmail(person.Email.Value))
            .Add(RuleSet.GreaterThanOrEqual(person.Age, 18))
            .ContinueOnFailure()
            .Apply();

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
        var result = Rules.For()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Apply();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Sync_Rule_Should_Fail_When_Predicate_Returns_False()
    {
        // Arrange
        var person = new PersonStub(string.Empty, "Doe", "john@example.com", 25);

        // Act
        var result = Rules.For()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Apply();

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
        var result = Rules.For()
            .Add(() => !string.IsNullOrEmpty(person.FirstName), message)
            .Apply();

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
        var result = Rules.For()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Add(() => person.Age >= 18)
            .Add(() => person.Email.Value.Contains("@"))
            .Apply();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Multiple_Sync_Rules_Should_Fail_When_Age_Below_Minimum()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 15);

        // Act
        var result = Rules.For()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Add(() => person.Age >= 18)
            .Add(() => person.Email.Value.Contains("@"))
            .Apply();

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
        var result = Rules.For()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .When(shouldValidateAge,
                builder =>
                    builder.Add(() => person.Age >= 18))
            .Apply();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task Async_Rule_Should_Pass_When_Email_Valid()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 25);

        // Act
        var result = await Rules.For()
            .Add(async _ => await Task.FromResult(!string.IsNullOrEmpty(person.Email.Value)))
            .ApplyAsync();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task Async_Rule_Should_Fail_When_Email_Invalid()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", string.Empty, 25);

        // Act
        var result = await Rules.For()
            .Add(async _ => await Task.FromResult(!string.IsNullOrEmpty(person.Email.Value)))
            .ApplyAsync();

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task Should_Mix_Sync_And_Async_Rules()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 25);

        async Task<bool> IsEmailUniqueAsync(string email, CancellationToken token)
        {
            await Task.Delay(1, token);

            return email.Contains("@");
        }

        // Act
        var result = await Rules.For()
            .Add(() => !string.IsNullOrEmpty(person.FirstName))
            .Add(async (token) => await IsEmailUniqueAsync(person.Email.Value, token))
            .ApplyAsync();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task Should_Support_Cancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var person = new PersonStub("John", "Doe", "john@example.com", 25);

        async Task<bool> LongRunningCheckAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);

            return true;
        }

        // Act & Assert
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await Rules.For()
                .Add(async (token) => await LongRunningCheckAsync(token))
                .ApplyAsync(cancellationToken: cts.Token);
        });
    }

    [Fact]
    public void Should_Collect_All_Failures_With_ContinueOnFailure()
    {
        // Arrange
        var person = new PersonStub(string.Empty, string.Empty, "invalid-email", 15);

        // Act
        var result = Rules.For()
            .Add(() => !string.IsNullOrEmpty(person.FirstName), "First name required")
            .Add(() => !string.IsNullOrEmpty(person.LastName), "Last name required")
            .Add(() => person.Age >= 18, "Must be adult")
            .Add(() => person.Email.Value.Contains("@"), "Invalid email")
            .ContinueOnFailure()
            .Apply();

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
        var result = Rules.For()
            .Add(() => person.Locations.Any())
            .Add(() => person.Locations.All(l => !string.IsNullOrEmpty(l.City)))
            .Apply();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Should_Support_Complex_Person_Validation()
    {
        // Arrange
        var person = new PersonStub("John", "Doe", "john@example.com", 25);

        // Act
        var result = Rules.For()
            .Add(() => !string.IsNullOrEmpty(person.FirstName) && !string.IsNullOrEmpty(person.LastName))
            .Add(() => person.Age >= 18 && person.Age < 100)
            .Add(() => person.Email.Value.Contains("@") && !person.Email.Value.Contains(" "))
            .Add(() => person.Nationality == "USA")
            .Apply();

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
        var result = Rules.For()
            .Add(RuleSet.IsNotEmpty(person.FirstName))
            .Add(RuleSet.IsNotEmpty(person.LastName))
            .Add(RuleSet.IsValidEmail(person.Email.Value))
            .Add(RuleSet.GreaterThanOrEqual(person.Age, 18))
            .Apply();

        // Assert

        result.ShouldBeFailure();
        result.Errors.Count.ShouldBe(1); // Should stop at first error
    }

    [Fact]
    public void Apply_CompletePersonValidation_ShouldValidateAllRules()
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

        var rules = Rules.For()
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
        var result = rules.Apply();

        // Assert

        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void Apply_PersonValidationWithFailures_ShouldCollectAllErrors()
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

        var rules = Rules.For()
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
        var result = rules.Apply();

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
    public void Apply_LocationValidationOnly_ShouldValidateAllLocations()
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

        var rules = Rules.For()
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
        var result = rules.Apply();

        // Assert

        result.ShouldBeSuccess();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void Apply_WithFluentValidationAndRules_ShouldValidateCorrectly()
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
            "Canada")); // Not USA - fails domain rule

        var rules = Rules.For()
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
        var result = rules.Apply();

        // Assert

        result.IsFailure.ShouldBeTrue();
        result.Errors.Count.ShouldBe(5); // FirstName, Email (domain), USA location, Age (fluent), Email (fluent)

        // Rule errors
        result.Errors.Any(e => e.Message.Contains("must not be empty")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("Invalid email address")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("must be equal to UNK")).ShouldBeTrue();
        result.Errors.Any(e => e.Message.Contains("No element in the collection satisfies")).ShouldBeTrue();

        // FluentValidation errors
        result.HasError<FluentValidationError>().ShouldBeTrue();
        var fluentErrors = result.GetError<FluentValidationError>();
        fluentErrors.Message.ShouldContain("Must be 18 or older");
        fluentErrors.Message.ShouldContain("Invalid email");
    }

    [Fact]
    public void Apply_WithValidPersonData_ShouldPassAllValidations()
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

        var rules = Rules.For()
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
        var result = rules.Apply();

        // Assert

        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.HasError<FluentValidationError>().ShouldBeFalse();
    }

    [Fact]
    public void Apply_WithOnlyFluentValidationFailures_ShouldCollectFluentErrors()
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

        var rules = Rules.For()
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
        var result = rules.Apply();

        // Assert

        result.IsFailure.ShouldBeTrue();
        result.HasError<FluentValidationError>().ShouldBeTrue();
        var fluentErrors = result.GetError<FluentValidationError>();
        fluentErrors.Message.ShouldContain("Must be 18 or older");
        fluentErrors.Message.ShouldContain("Invalid email");
        result.Errors.Count.ShouldBe(1); // Only the FluentValidation error group
    }
}