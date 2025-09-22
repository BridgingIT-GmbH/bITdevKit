// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Validation;

using FluentValidation;

public class FluentValidatorExtensionsTests
{
    public class Person
    {
        public int Age { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            var type = typeof(Person);

            this.AddRangeRule(type.GetProperty(nameof(Person.Age)), 18, 65, "Age must be between 18 and 65.");
        }
    }

    private readonly PersonValidator validator = [];

    [Fact]
    public void Should_Fail_When_Age_Is_OutOfRange()
    {
        var person = new Person { Age = 10 };
        var result = this.validator.Validate(person);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Pass_When_Age_Is_WithinRange()
    {
        var person = new Person { Age = 30 };
        var result = this.validator.Validate(person);

        result.IsValid.ShouldBeTrue();
    }
}