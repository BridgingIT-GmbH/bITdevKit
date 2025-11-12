// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

using Shouldly;
using Xunit;

public class SafeReadOnlyDictionaryTests
{
    [Fact]
    public void Indexer_ShouldReturnDefault_WhenKeyDoesNotExist()
    {
        // Arrange
        var dictionary = new SafeReadOnlyDictionary<string, string>();

        // Act
        var value = dictionary["missing"];

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void Indexer_ShouldBeCaseInsensitive_ForStringKeys()
    {
        // Arrange
        var source = new Dictionary<string, string>
            {
                { "Connection", "Server=db01" }
            };

        var dictionary = new SafeReadOnlyDictionary<string, string>(source);

        // Act
        var result = dictionary["connection"];

        // Assert
        result.ShouldBe("Server=db01");
    }

    [Fact]
    public void TryGetValue_ShouldSucceed_ForExistingKey_CaseInsensitive()
    {
        // Arrange
        var dictionary = new SafeReadOnlyDictionary<string, string>(
            new Dictionary<string, string> { ["Item"] = "Value123" });

        // Act
        var found = dictionary.TryGetValue("ITEM", out var result);

        // Assert
        found.ShouldBeTrue();
        result.ShouldBe("Value123");
    }

    [Fact]
    public void ContainsKey_ShouldWork_CaseInsensitive()
    {
        // Arrange
        var dictionary = new SafeReadOnlyDictionary<string, string>(
            new Dictionary<string, string> { ["Something"] = "Else" });

        // Act
        var contains = dictionary.ContainsKey("something");

        // Assert
        contains.ShouldBeTrue();
    }

    [Fact]
    public void Count_ShouldReflectNumberOfEntries()
    {
        // Arrange
        var dictionary = new SafeReadOnlyDictionary<string, string>(
            new Dictionary<string, string>
            {
                ["First"] = "A",
                ["Second"] = "B"
            });

        // Act
        var count = dictionary.Count;

        // Assert
        count.ShouldBe(2);
    }
}
