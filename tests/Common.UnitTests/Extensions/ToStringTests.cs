// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

// ReSharper disable ExpressionIsAlwaysNull

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

public class ToStringTests
{
    private readonly Faker faker = new();

    [Fact]
    public void ToString_NullEnumerable_ReturnsEmptyString()
    {
        // Arrange
        IEnumerable<string> source = null;
        var separator = this.faker.Random.String2(1);

        // Act
        var result = source.ToString(separator);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void ToString_EmptyEnumerable_ReturnsEmptyString()
    {
        // Arrange
        var source = new List<string>();
        var separator = this.faker.Random.String2(1);

        // Act
        var result = source.ToString(separator);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void ToString_SingleItem_ReturnsItemWithoutSeparator()
    {
        // Arrange
        var item = this.faker.Lorem.Word();
        var source = new List<string> { item };
        var separator = this.faker.Random.String2(1);

        // Act
        var result = source.ToString(separator);

        // Assert
        result.ShouldBe(item);
    }

    [Fact]
    public void ToString_MultipleItems_ReturnsJoinedString()
    {
        // Arrange
        var items = this.faker.Make(3, () => this.faker.Lorem.Word());
        var separator = this.faker.Random.String2(1);

        // Act
        var result = items.ToString(separator);

        // Assert
        result.ShouldBe(string.Join(separator, items));
    }

    [Fact]
    public void ToString_WithCharSeparator_CallsStringOverload()
    {
        // Arrange
        var items = this.faker.Make(3, () => this.faker.Lorem.Word());
        var separator = this.faker.Random.Char();

        // Act
        var result = items.ToString(separator);

        // Assert
        result.ShouldBe(string.Join(separator.ToString(), items));
    }

    [Fact]
    public void ToString_WithNonStringType_ConvertsToString()
    {
        // Arrange
        var items = this.faker.Make(3, () => this.faker.Random.Int(1, 100));
        var separator = this.faker.Random.String2(1);

        // Act
        var result = items.ToString(separator);

        // Assert
        result.ShouldBe(string.Join(separator, items));
    }
}