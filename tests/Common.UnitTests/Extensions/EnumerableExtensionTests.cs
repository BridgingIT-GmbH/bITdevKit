// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class EnumerableExtensionTests
{
    [Fact]
    public void Insert_WhenCollectionIsNull_ShouldReturnListWithItem()
    {
        // Arrange
        IEnumerable<int> source = null;
        var item = 1;
        var expected = new[] { item };

        // Act
        var result = source.Insert(item);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Insert_WhenInsertingAtNegativeIndex_ShouldAddItemToEndOfCollection()
    {
        // Arrange
        IEnumerable<int> source = [1, 2, 3];
        var item = 4;
        var expected = new[] { 1, 2, 3, item };

        // Act
        var result = source.Insert(item, -1);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Insert_WhenInsertingAtIndexOfZero_ShouldAddItemAtStart()
    {
        // Arrange
        IEnumerable<int> source = [1, 2, 3];
        var item = 0;
        var expected = new List<int> { item, 1, 2, 3 };

        // Act
        var result = source.Insert(item);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Insert_WhenEmptyCollection_ShouldInsertItemToCollection()
    {
        // Arrange
        IEnumerable<int> source = new List<int>();
        var item = 1;
        var expected = new[] { item };

        // Act
        var result = source.Insert(item);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Insert_WhenNullCollection_ShouldInsertItemToCollection()
    {
        // Arrange
        IEnumerable<int> source = null;
        var item = 1;
        var expected = new[] { item };

        // Act
        var result = source.Insert(item);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Add_WhenAdding_ShouldAddItemToEndOfCollection()
    {
        // Arrange
        IEnumerable<int> source = [1, 2, 3];
        var item = 4;
        var expected = new[] { 1, 2, 3, item };

        // Act
        var result = source.Add(item);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Add_WhenAddingEmptyCollection_ShouldAddItemToEndOfCollection()
    {
        // Arrange
        IEnumerable<int> source = new List<int>();
        var item = 1;
        var expected = new[] { item };

        // Act
        var result = source.Add(item);

        // Assert
        Assert.Equal(expected, result);
    }
}