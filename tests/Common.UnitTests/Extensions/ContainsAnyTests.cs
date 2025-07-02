// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

[UnitTest("Common")]
public class ContainsAnyTests
{
    [Fact]
    public void ContainsAny_ReturnsFalse_GivenNullOrEmptySourceString()
    {
        // Arrange
        string source = null;
        var items = new[] { "abc", "def", "ghi" };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsAny_ReturnsFalse_GivenNullOrEmptyItemsArray()
    {
        // Arrange
        var source = "Hello world";
        string[] items = null;

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsAny_ReturnsFalse_GivenItemsNotFoundInSourceString()
    {
        // Arrange
        var source = "The quick brown fox jumps over the lazy dog";
        var items = new[] { "cat", "horse" };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsAny_ReturnsTrue_GivenSingleItemFoundInSourceString()
    {
        // Arrange
        var source = "The quick brown fox jumps over the lazy dog";
        var items = new[] { "fox" };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ContainsAny_ReturnsTrue_GivenMultipleItemsFoundInSourceString()
    {
        // Arrange
        var source = "The quick brown fox jumps over the lazy dog";
        var items = new[] { "fox", "dog" };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ContainsAny_Generic_ReturnsFalse_GivenNullOrEmptySourceCollection()
    {
        // Arrange
        List<int> source = null;
        var items = new[] { 1, 2, 3 };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsAny_Generic_ReturnsFalse_GivenNullOrEmptyItemsCollection()
    {
        // Arrange
        var source = new List<int> { 1, 2, 3 };
        int[] items = null;

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsAny_Generic_ReturnsFalse_GivenItemsNotFoundInSourceCollection()
    {
        // Arrange
        var source = new List<int> { 1, 2, 3 };
        var items = new[] { 4, 5 };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsAny_Generic_ReturnsTrue_GivenSingleItemFoundInSourceCollection()
    {
        // Arrange
        var source = new List<int> { 1, 2, 3 };
        var items = new[] { 2 };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ContainsAny_Generic_ReturnsTrue_GivenMultipleItemsFoundInSourceCollection()
    {
        // Arrange
        var source = new List<int> { 1, 2, 3 };
        var items = new[] { 2, 3 };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ContainsAny_Generic_CustomType_UsesDefaultEquality()
    {
        // Arrange
        var source = new List<CustomType> { new CustomType { Id = 1, Name = "A" } };
        var items = new[] { new CustomType { Id = 1, Name = "B" } };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeTrue(); // Id matches, default equality
    }

    [Fact]
    public void ContainsAny_Generic_CustomType_UsesCustomComparer()
    {
        // Arrange
        var source = new List<CustomType> { new CustomType { Id = 1, Name = "A" } };
        var items = new[] { new CustomType { Id = 2, Name = "A" } };
        var comparer = new CustomTypeComparer();

        // Act
        var result = source.ContainsAny(items, comparer);

        // Assert
        result.ShouldBeTrue(); // Name matches, custom comparer
    }

    private class CustomType
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public override bool Equals(object obj) => obj is CustomType other && Id == other.Id;
        public override int GetHashCode() => Id.GetHashCode();
    }

    private class CustomTypeComparer : IEqualityComparer<CustomType>
    {
        public bool Equals(CustomType x, CustomType y) => x?.Name == y?.Name;
        public int GetHashCode(CustomType obj) => obj.Name?.GetHashCode() ?? 0;
    }
}