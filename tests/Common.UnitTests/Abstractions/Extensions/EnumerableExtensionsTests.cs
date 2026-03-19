// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class EnumerableExtensionsTests
{
    [Fact]
    public async Task ToAsyncEnumerable_WithItems_YieldsItemsInOrder()
    {
        // Arrange
        var source = new[] { 1, 2, 3 };

        // Act
        var result = await source.ToAsyncEnumerable().ToListAsync();

        // Assert
        result.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task ToAsyncEnumerable_WithEmptyEnumerable_YieldsNoItems()
    {
        // Arrange
        var source = Array.Empty<int>();

        // Act
        var result = await source.ToAsyncEnumerable().ToListAsync();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToAsyncEnumerable_WithNullEnumerable_YieldsNoItems()
    {
        // Arrange
        IEnumerable<int> source = null;

        // Act
        var result = await source.ToAsyncEnumerable().ToListAsync();

        // Assert
        result.ShouldBeEmpty();
    }
}
