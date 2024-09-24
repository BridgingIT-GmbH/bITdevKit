// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using FluentValidation;
using FluentValidation.TestHelper;

[UnitTest("Common")]
public class GuidValidationTests
{
    [Fact]
    public void MustBeValidGuid_WithValidGuid_ShouldNotHaveValidationError()
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustBeValidGuid();
        var model = new TestModel
        {
            GuidString = Guid.NewGuid()
                .ToString()
        };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.GuidString);
    }

    [Fact]
    public void MustBeValidGuid_WithInvalidGuid_ShouldHaveValidationError()
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustBeValidGuid();
        var model = new TestModel { GuidString = "not-a-guid" };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GuidString);
    }

    [Fact]
    public void MustNotEmptyGuid_WithNonEmptyGuid_ShouldNotHaveValidationError()
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustNotBeEmptyGuid();
        var model = new TestModel
        {
            GuidString = Guid.NewGuid()
                .ToString()
        };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.GuidString);
    }

    [Fact]
    public void MustNotEmptyGuid_WithEmptyGuid_ShouldHaveValidationError()
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustNotBeEmptyGuid();
        var model = new TestModel { GuidString = Guid.Empty.ToString() };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GuidString);
    }

    [Fact]
    public void MustBeEmptyGuid_WithEmptyGuid_ShouldNotHaveValidationError()
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustBeEmptyGuid();
        var model = new TestModel { GuidString = Guid.Empty.ToString() };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.GuidString);
    }

    [Fact]
    public void MustBeEmptyGuid_WithNonEmptyGuid_ShouldHaveValidationError()
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustBeEmptyGuid();
        var model = new TestModel
        {
            GuidString = Guid.NewGuid()
                .ToString()
        };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GuidString);
    }

    [Fact]
    public void MustBeDefaultOrEmptyGuid_WithDefaultGuid_ShouldNotHaveValidationError()
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustBeDefaultOrEmptyGuid();
        var model = new TestModel { GuidString = default(Guid).ToString() };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.GuidString);
    }

    [Fact]
    public void MustBeDefaultOrEmptyGuid_WithEmptyString_ShouldNotHaveValidationError()
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustBeDefaultOrEmptyGuid();
        var model = new TestModel { GuidString = string.Empty };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.GuidString);
    }

    [Fact]
    public void MustBeDefaultOrEmptyGuid_WithNonDefaultNonEmptyGuid_ShouldHaveValidationError()
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustBeDefaultOrEmptyGuid();
        var model = new TestModel
        {
            GuidString = Guid.NewGuid()
                .ToString()
        };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GuidString);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{00000000-0000-0000-0000-000000000000}")]
    [InlineData("12345678-90ab-cdef-1234-567890abcdef")]
    [InlineData("{12345678-90ab-cdef-1234-567890abcdef}")]
    public void MustBeInGuidFormat_WithValidFormat_ShouldNotHaveValidationError(string value)
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustBeInGuidFormat();
        var model = new TestModel { GuidString = value };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.GuidString);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345678-90ab-cdef-1234-567890abcde")] // One character short
    [InlineData("12345678-90ab-cdef-1234-567890abcdefg")] // One character long
    [InlineData("12345678090ab0cdef012340567890abcdef")] // No hyphens
    [InlineData("(12345678-90ab-cdef-1234-567890abcdef)")] // Wrong brackets
    public void MustBeInGuidFormat_WithInvalidFormat_ShouldHaveValidationError(string value)
    {
        // Arrange
        var validator = new TestValidator();
        validator.RuleFor(x => x.GuidString)
            .MustBeInGuidFormat();
        var model = new TestModel { GuidString = value };

        // Act
        var result = validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GuidString);
    }

    private class TestModel
    {
        public string GuidString { get; set; }
    }

    private class TestValidator : AbstractValidator<TestModel> { }
}