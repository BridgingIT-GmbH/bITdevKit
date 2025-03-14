// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

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
        var result = dictionary.SafeNull()
            .GetValueOrDefault("key", 100);

        // Assert
        result.ShouldBe(100);
    }

    [Fact]
    public void GetValueOrDefault_WithNullKey_ShouldReturnDefaultValue()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        var result = dictionary.SafeNull()
            .GetValueOrDefault(null, 100);

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
        dictionary["key2"]
            .ShouldBe(100);
    }

    [Fact]
    public void AddOrUpdate_WithExistingKey_ShouldUpdateEntry()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key", 42 } };

        // Act
        dictionary.AddOrUpdate("key", 100);

        // Assert
        dictionary["key"]
            .ShouldBe(100);
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
        dictionary["key"]
            .ShouldBe(42);
    }

    [Fact]
    public void AddOrUpdate_WithMultipleItems_ShouldAddOrUpdateAllEntries()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> { { "key1", 42 } };
        var itemsToAdd = new Dictionary<string, int> { { "key1", 100 }, { "key2", 200 } };

        // Act
        dictionary.AddOrUpdate(itemsToAdd);

        // Assert
        dictionary.Count.ShouldBe(2);
        dictionary["key1"]
            .ShouldBe(100);
        dictionary["key2"]
            .ShouldBe(200);
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
        dictionary["key1"]
            .ShouldBe(42);
    }

    [Fact]
    public void ContainsKeyIgnoreCase_NullDictionary_ReturnsFalse()
    {
        // Arrange
        Dictionary<string, int> sut = null;

        // Act
        var result = sut.ContainsKeyIgnoreCase("any");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsKeyIgnoreCase_EmptyDictionary_ReturnsFalse()
    {
        // Arrange
        var sut = new Dictionary<string, int>();

        // Act
        var result = sut.ContainsKeyIgnoreCase("any");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsKeyIgnoreCase_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var sut = new Dictionary<string, int> { ["Key"] = 1 };

        // Act
        var result = sut.ContainsKeyIgnoreCase("Key");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ContainsKeyIgnoreCase_DifferentCase_ReturnsTrue()
    {
        // Arrange
        var sut = new Dictionary<string, int> { ["Key"] = 1 };

        // Act
        var result = sut.ContainsKeyIgnoreCase("kEy");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ContainsKeyIgnoreCase_NonExistentKey_ReturnsFalse()
    {
        // Arrange
        var sut = new Dictionary<string, int> { ["Key"] = 1 };

        // Act
        var result = sut.ContainsKeyIgnoreCase("NonExistent");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsKeyIgnoreCase_MultipleKeys_ReturnsTrue()
    {
        // Arrange
        var sut = new Dictionary<string, int> { ["First"] = 1, ["Second"] = 2, ["Third"] = 3 };

        // Act
        var result = sut.ContainsKeyIgnoreCase("sEcOnD");

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ContainsKeyIgnoreCase_EmptyOrNullKey_ReturnsFalse(string key)
    {
        // Arrange
        var sut = new Dictionary<string, int> { ["Key"] = 1 };

        // Act
        var result = sut.ContainsKeyIgnoreCase(key);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void AddIf_ConditionTrue_AddsItem()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIf(1, "one", true);

        // Assert
        result.ShouldNotBeNull();
        sut.Count.ShouldBe(1);
        sut[1].ShouldBe("one");
        result.ShouldBeSameAs(sut); // Chaining returns same instance
    }

    [Fact]
    public void AddIf_ConditionFalse_DoesNotAdd()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIf(1, "one", false);

        // Assert
        result.ShouldNotBeNull();
        sut.ShouldBeEmpty();
        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void AddIf_NullDictionary_ReturnsNull()
    {
        // Arrange
        IDictionary<int, string> sut = null;

        // Act
        var result = sut.AddIf(1, "one", true);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddIfWithPredicate_PredicateTrue_AddsItem()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIf(1, "one", v => v.Length > 2);

        // Assert
        result.ShouldNotBeNull();
        sut.Count.ShouldBe(1);
        sut[1].ShouldBe("one");
    }

    [Fact]
    public void AddIfWithPredicate_PredicateFalse_DoesNotAdd()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIf(1, "one", v => v.Length > 3);

        // Assert
        result.ShouldNotBeNull();
        sut.ShouldBeEmpty();
    }

    [Fact]
    public void AddIfWithPredicate_NullDictionary_ReturnsNull()
    {
        // Arrange
        IDictionary<int, string> sut = null;

        // Act
        var result = sut.AddIf(1, "one", v => v.Length > 2);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddIfWithPredicate_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => sut.AddIf(1, "one", (Func<string, bool>)null));
    }

    [Fact]
    public void AddIfNotNull_ValueNotNull_AddsItem()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIfNotNull(1, "one");

        // Assert
        result.ShouldNotBeNull();
        sut.Count.ShouldBe(1);
        sut[1].ShouldBe("one");
    }

    [Fact]
    public void AddIfNotNull_ValueNull_DoesNotAdd()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIfNotNull(1, (string)null);

        // Assert
        result.ShouldNotBeNull();
        sut.ShouldBeEmpty();
    }

    [Fact]
    public void AddIfNotNull_NullDictionary_ReturnsNull()
    {
        // Arrange
        IDictionary<int, string> sut = null;

        // Act
        var result = sut.AddIfNotNull(1, "one");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddRangeIf_ConditionTrue_AddsItems()
    {
        // Arrange
        var sut = new Dictionary<int, string>();
        var items = new Dictionary<int, string> { { 1, "one" }, { 2, "two" } };

        // Act
        var result = sut.AddRangeIf(items, true);

        // Assert
        result.ShouldNotBeNull();
        sut.Count.ShouldBe(2);
        sut[1].ShouldBe("one");
        sut[2].ShouldBe("two");
    }

    [Fact]
    public void AddRangeIf_ConditionFalse_DoesNotAdd()
    {
        // Arrange
        var sut = new Dictionary<int, string>();
        var items = new Dictionary<int, string> { { 1, "one" }, { 2, "two" } };

        // Act
        var result = sut.AddRangeIf(items, false);

        // Assert
        result.ShouldNotBeNull();
        sut.ShouldBeEmpty();
    }

    [Fact]
    public void AddRangeIf_NullDictionary_ReturnsNull()
    {
        // Arrange
        IDictionary<int, string> sut = null;
        var items = new Dictionary<int, string> { { 1, "one" } };

        // Act
        var result = sut.AddRangeIf(items, true);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddRangeIf_NullItems_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => sut.AddRangeIf(null, true));
    }

    [Fact]
    public void AddIfUnique_KeyNotExists_AddsItem()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIfUnique(1, "one");

        // Assert
        result.ShouldNotBeNull();
        sut.Count.ShouldBe(1);
        sut[1].ShouldBe("one");
    }

    [Fact]
    public void AddIfUnique_KeyExists_DoesNotAdd()
    {
        // Arrange
        var sut = new Dictionary<int, string> { { 1, "one" } };

        // Act
        var result = sut.AddIfUnique(1, "new one");

        // Assert
        result.ShouldNotBeNull();
        sut.Count.ShouldBe(1);
        sut[1].ShouldBe("one"); // Original value preserved
    }

    [Fact]
    public void AddIfUnique_NullDictionary_ReturnsNull()
    {
        // Arrange
        IDictionary<int, string> sut = null;

        // Act
        var result = sut.AddIfUnique(1, "one");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddIfAll_AllPredicatesTrue_AddsItem()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIfAll(1, "one", v => v.Length > 2, v => v.StartsWith("o"));

        // Assert
        result.ShouldNotBeNull();
        sut.Count.ShouldBe(1);
        sut[1].ShouldBe("one");
    }

    [Fact]
    public void AddIfAll_AnyPredicateFalse_DoesNotAdd()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIfAll(1, "one", v => v.Length > 2, v => v.StartsWith("x"));

        // Assert
        result.ShouldNotBeNull();
        sut.ShouldBeEmpty();
    }

    [Fact]
    public void AddIfAll_EmptyPredicates_AddsItem()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIfAll(1, "one");

        // Assert
        result.ShouldNotBeNull();
        sut.Count.ShouldBe(1);
        sut[1].ShouldBe("one");
    }

    [Fact]
    public void AddIfAll_NullDictionary_ReturnsNull()
    {
        // Arrange
        IDictionary<int, string> sut = null;

        // Act
        var result = sut.AddIfAll(1, "one", v => v.Length > 2);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddIfAny_AnyPredicateTrue_AddsItem()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIfAny(1, "one", v => v.Length > 3, v => v.Contains("n"));

        // Assert
        result.ShouldNotBeNull();
        sut.Count.ShouldBe(1);
        sut[1].ShouldBe("one");
    }

    [Fact]
    public void AddIfAny_NoPredicateTrue_DoesNotAdd()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIfAny(1, "one", v => v.Length > 3, v => v.StartsWith("x"));

        // Assert
        result.ShouldNotBeNull();
        sut.ShouldBeEmpty();
    }

    [Fact]
    public void AddIfAny_NullDictionary_ReturnsNull()
    {
        // Arrange
        IDictionary<int, string> sut = null;

        // Act
        var result = sut.AddIfAny(1, "one", v => v.Length > 2);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddIfAny_EmptyPredicates_DoesNotAdd()
    {
        // Arrange
        var sut = new Dictionary<int, string>();

        // Act
        var result = sut.AddIfAny(1, "one");

        // Assert
        result.ShouldNotBeNull();
        sut.ShouldBeEmpty();
    }
}