// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

using Shouldly;
using Xunit;

public class SafeDictionaryTests
{
    [Fact]
    public void Indexer_ShouldReturnDefault_WhenKeyDoesNotExist()
    {
        // Arrange
        var dictionary = new SafeDictionary<string, string>();

        // Act
        var result = dictionary["nonexistent"];

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Indexer_ShouldBeCaseInsensitive_ForStringKeys()
    {
        // Arrange
        var dictionary = new SafeDictionary<string, string>
        {
            ["Default"] = "Server=localhost"
        };

        // Act
        var result = dictionary["default"];

        // Assert
        result.ShouldBe("Server=localhost");
    }

    [Fact]
    public void TryGetValue_ShouldReturnTrue_WhenKeyExists_CaseInsensitive()
    {
        // Arrange
        var dictionary = new SafeDictionary<string, string>
        {
            ["MyKey"] = "SomeValue"
        };

        // Act
        var success = dictionary.TryGetValue("mykey", out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe("SomeValue");
    }

    [Fact]
    public void Remove_ShouldDeleteKeySuccessfully()
    {
        // Arrange
        var dictionary = new SafeDictionary<string, string>
        {
            ["ToRemove"] = "abc"
        };

        // Act
        var removed = dictionary.Remove("toremove");

        // Assert
        removed.ShouldBeTrue();
        dictionary.ContainsKey("ToRemove").ShouldBeFalse();
    }

    [Fact]
    public void AsReadOnly_ShouldShareValuesAndBeSafe()
    {
        // Arrange
        var dictionary = new SafeDictionary<string, string>
        {
            ["Primary"] = "MainDB"
        };

        // Act
        var readOnly = dictionary.AsReadOnly();
        var result = readOnly["PRIMARY"];

        // Assert
        result.ShouldBe("MainDB");
    }

    [Fact]
    public void Indexer_Set_ShouldAddOrUpdateEntries()
    {
        // Arrange
        var dictionary = new SafeDictionary<string, string>
        {
            // Act
            ["Env"] = "Dev",
            ["env"] = "Prod" // case-insensitive update
        };

        // Assert
        dictionary.Count.ShouldBe(1);
        dictionary["ENV"].ShouldBe("Prod");
    }
}
