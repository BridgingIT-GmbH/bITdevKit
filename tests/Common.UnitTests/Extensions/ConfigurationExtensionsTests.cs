namespace BridgingIT.DevKit.Common.Tests;

using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;
using Bogus;
using System.Collections.Generic;

public class ConfigurationExtensionsTests
{
    private readonly Faker faker = new();

    private IConfiguration CreateConfiguration(IDictionary<string, string> initialData)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();
    }

    [Fact]
    public void GetSection_WithNullSource_ReturnsNull()
    {
        // Arrange
        var key = this.faker.Random.Word();

        // Act
        var result = Extensions.GetSection(null, key, false);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetSection_WithNonExistentKey_ReturnsEmptySection()
    {
        // Arrange
        var source = this.CreateConfiguration(new Dictionary<string, string>());
        var key = this.faker.Random.Word();

        // Act
        var result = Extensions.GetSection(source, key, false);

        // Assert
        result.ShouldNotBeNull();
        // result.Key.ShouldBe(key);
        // result.Value.ShouldBeNull();
    }

    [Fact]
    public void GetSection_WithSkipPlaceholders_ReturnsSectionWithoutReplacement()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var childKey = this.faker.Random.Word();
        var placeholderKey = this.faker.Random.Word();
        var placeholderValue = this.faker.Random.Word();
        var source = this.CreateConfiguration(new Dictionary<string, string>
        {
            [$"{key}:{childKey}"] = $"{{{{{placeholderKey}}}}}",
            [placeholderKey] = placeholderValue
        });

        // Act
        var result = Extensions.GetSection(source, key, true);

        // Assert
        result.ShouldNotBeNull();
        result[childKey].ShouldBe($"{{{{{placeholderKey}}}}}");
    }

    [Fact]
    public void GetSection_WithPlaceholdersInValues_ReplacesPlaceholdersWithCorrectValues()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var childKey = this.faker.Random.Word();
        var placeholderKey = this.faker.Random.Word();
        var placeholderValue = this.faker.Random.Word();
        var source = this.CreateConfiguration(new Dictionary<string, string>
        {
            [$"{key}:{childKey}"] = $"{{{{{placeholderKey}}}}}",
            [placeholderKey] = placeholderValue
        });

        // Act
        var result = Extensions.GetSection(source, key, false);

        // Assert
        result.ShouldNotBeNull();
        result[childKey].ShouldBe(placeholderValue);
    }

    [Fact]
    public void GetSection_WithMultiplePlaceholdersInValues_ReplacesAllPlaceholdersWithCorrectValues()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var childKey1 = this.faker.Random.Word();
        var childKey2 = this.faker.Random.Word();
        var childKey3 = this.faker.Random.Word();
        var placeholderKey1 = this.faker.Random.Word();
        var placeholderKey2 = this.faker.Random.Word();
        var placeholderKey3 = this.faker.Random.Word();
        var placeholderValue1 = this.faker.Random.Word();
        var placeholderValue2 = this.faker.Random.Word();
        var placeholderValue3 = this.faker.Random.Word();

        var source = this.CreateConfiguration(new Dictionary<string, string>
        {
            [$"{key}:{childKey1}"] = $"Start {{{{{placeholderKey1}}}}} Middle {{{{{placeholderKey2}}}}} End",
            [$"{key}:{childKey2}"] = $"{{{{{placeholderKey3}}}}} and {{{{{placeholderKey1}}}}}",
            [$"{key}:{childKey3}"] = $"No placeholders here",
            [placeholderKey1] = placeholderValue1,
            [placeholderKey2] = placeholderValue2,
            [placeholderKey3] = placeholderValue3
        });

        // Act
        var result = Extensions.GetSection(source, key, false);

        // Assert
        result.ShouldNotBeNull();
        result[childKey1].ShouldBe($"Start {placeholderValue1} Middle {placeholderValue2} End");
        result[childKey2].ShouldBe($"{placeholderValue3} and {placeholderValue1}");
        result[childKey3].ShouldBe("No placeholders here");
    }

    [Fact]
    public void GetSection_WithPlaceholdersInKeys_DoesNotReplaceKeys()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var childKey = $"{{{{.{this.faker.Random.Word()}}}}}";
        var value = this.faker.Random.Word();
        var source = this.CreateConfiguration(new Dictionary<string, string>
        {
            [$"{key}:{childKey}"] = value
        });

        // Act
        var result = Extensions.GetSection(source, key, false);

        // Assert
        result.ShouldNotBeNull();
        result[childKey].ShouldBe(value);
    }

    [Fact]
    public void GetSection_WithNoPlaceholders_DoesNotModifyValues()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var childKey = this.faker.Random.Word();
        var originalValue = this.faker.Random.Word();
        var source = this.CreateConfiguration(new Dictionary<string, string>
        {
            [$"{key}:{childKey}"] = originalValue
        });

        // Act
        var result = Extensions.GetSection(source, key, false);

        // Assert
        result.ShouldNotBeNull();
        result[childKey].ShouldBe(originalValue);
    }

    [Fact]
    public void GetSection_WithMultiplePlaceholdersInValue_ReplacesAllPlaceholders()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var childKey = this.faker.Random.Word();
        var placeholder1Key = this.faker.Random.Word();
        var placeholder1Value = this.faker.Random.Word();
        var placeholder2Key = this.faker.Random.Word();
        var placeholder2Value = this.faker.Random.Word();
        var source = this.CreateConfiguration(new Dictionary<string, string>
        {
            [$"{key}:{childKey}"] = $"{{{{{placeholder1Key}}}}} and {{{{{placeholder2Key}}}}}",
            [placeholder1Key] = placeholder1Value,
            [placeholder2Key] = placeholder2Value
        });

        // Act
        var result = Extensions.GetSection(source, key, false);

        // Assert
        result.ShouldNotBeNull();
        result[childKey].ShouldBe($"{placeholder1Value} and {placeholder2Value}");
    }

    // [Fact]
    // public void GetSection_WithNestedPlaceholdersInValue_ReplacesAllPlaceholders()
    // {
    //     // Arrange
    //     var key = this.faker.Random.Word();
    //     var childKey = this.faker.Random.Word();
    //     var placeholder1Key = this.faker.Random.Word();
    //     var placeholder2Key = this.faker.Random.Word();
    //     var placeholder2Value = this.faker.Random.Word();
    //     var source = this.CreateConfiguration(new Dictionary<string, string>
    //     {
    //         [$"{key}:{childKey}"] = $"{{{{{placeholder1Key}}}}}",
    //         [placeholder1Key] = $"{{{{{placeholder2Key}}}}}",
    //         [placeholder2Key] = placeholder2Value
    //     });
    //
    //     // Act
    //     var result = Extensions.GetSection(source, key, false);
    //
    //     // Assert
    //     result.ShouldNotBeNull();
    //     result[childKey].ShouldBe(placeholder2Value);
    // }

    [Fact]
    public void GetSection_WithNonExistentPlaceholder_LeavesPlaceholderUnchanged()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var childKey = this.faker.Random.Word();
        var nonExistentPlaceholder = this.faker.Random.Word();
        var source = this.CreateConfiguration(new Dictionary<string, string>
        {
            [$"{key}:{childKey}"] = $"{{{{.{nonExistentPlaceholder}}}}}"
        });

        // Act
        var result = Extensions.GetSection(source, key, false);

        // Assert
        result.ShouldNotBeNull();
        result[childKey].ShouldBe($"{{{{.{nonExistentPlaceholder}}}}}");
    }

    [Fact]
    public void GetSection_WithPlaceholderReferencingItself_LeavesPlaceholderUnchanged()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var childKey = this.faker.Random.Word();
        var source = this.CreateConfiguration(new Dictionary<string, string>
        {
            [$"{key}:{childKey}"] = $"{{{{.{key}:{childKey}}}}}"
        });

        // Act
        var result = Extensions.GetSection(source, key, false);

        // Assert
        result.ShouldNotBeNull();
        result[childKey].ShouldBe($"{{{{.{key}:{childKey}}}}}");
    }
}