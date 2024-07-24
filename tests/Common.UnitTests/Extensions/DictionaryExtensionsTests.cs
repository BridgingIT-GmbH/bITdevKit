// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System.Collections.Generic;

[UnitTest("Common")]
public class DictionaryExtensionsTests
{
    [Fact]
    public void GetValue_WithValidKey_ShouldReturnValue()
    {
        // Arrange
        IDictionary<string, int> dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        var result = dictionary.GetValue("key");

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void GetValue_WithInvalidKey_ShouldReturnDefault()
    {
        // Arrange
        IDictionary<string, int> dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        var result = dictionary.GetValue("invalidKey");

        // Assert
        result.ShouldBe(0); // default(int) is 0
    }

    [Fact]
    public void GetValue_WithNullDictionary_ShouldReturnDefault()
    {
        // Arrange
        IDictionary<string, int> dictionary = null;

        // Act
        var result = dictionary.GetValue("key");

        // Assert
        result.ShouldBe(0); // default(int) is 0
    }

    [Fact]
    public void GetValue_WithNullKey_ShouldReturnDefault()
    {
        // Arrange
        IDictionary<string, int> dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        var result = dictionary.GetValue(null);

        // Assert
        result.ShouldBe(0); // default(int) is 0
    }

    [Fact]
    public void GetValueOrDefault_WithValidKey_ShouldReturnValue()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        var result = dictionary.GetValueOrDefault("key", 100);

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void GetValueOrDefault_WithInvalidKey_ShouldReturnDefaultValue()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        var result = dictionary.GetValueOrDefault("invalidKey", 100);

        // Assert
        result.ShouldBe(100);
    }

    [Fact]
    public void GetValueOrDefault_WithNullDictionary_ShouldReturnDefaultValue()
    {
        // Arrange
        Dictionary<string, int> dictionary = null;

        // Act
        var result = dictionary.SafeNull().GetValueOrDefault("key", 100);

        // Assert
        result.ShouldBe(100);
    }

    [Fact]
    public void GetValueOrDefault_WithNullKey_ShouldReturnDefaultValue()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        var result = dictionary.SafeNull().GetValueOrDefault(null, 100);

        // Assert
        result.ShouldBe(100);
    }

    [Fact]
    public void AddOrUpdate_WithNewKey_ShouldAddNewEntry()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key1", 42 } };

        // Act
        dictionary.AddOrUpdate("key2", 100);

        // Assert
        dictionary.ShouldContainKey("key2");
        dictionary["key2"].ShouldBe(100);
    }

    [Fact]
    public void AddOrUpdate_WithExistingKey_ShouldUpdateEntry()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        dictionary.AddOrUpdate("key", 100);

        // Assert
        dictionary["key"].ShouldBe(100);
    }

    [Fact]
    public void AddOrUpdate_WithNullDictionary_ShouldReturnNull()
    {
        // Arrange
        Dictionary<string, int> dictionary = null;

        // Act
        var result = dictionary.AddOrUpdate("key", 100);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddOrUpdate_WithNullKey_ShouldNotModifyDictionary()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        dictionary.AddOrUpdate(null, 100);

        // Assert
        dictionary.Count.ShouldBe(1);
        dictionary["key"].ShouldBe(42);
    }

    [Fact]
    public void AddOrUpdate_WithMultipleItems_ShouldAddOrUpdateAllEntries()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key1", 42 } };
        var itemsToAdd = new Dictionary<string, int>
            {
                { "key1", 100 },
                { "key2", 200 }
            };

        // Act
        dictionary.AddOrUpdate(itemsToAdd);

        // Assert
        dictionary.Count.ShouldBe(2);
        dictionary["key1"].ShouldBe(100);
        dictionary["key2"].ShouldBe(200);
    }

    [Fact]
    public void AddOrUpdate_WithNullItemsDictionary_ShouldNotModifySourceDictionary()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key1", 42 } };
        Dictionary<string, int> itemsToAdd = null;

        // Act
        dictionary.AddOrUpdate(itemsToAdd);

        // Assert
        dictionary.Count.ShouldBe(1);
        dictionary["key1"].ShouldBe(42);
    }
}