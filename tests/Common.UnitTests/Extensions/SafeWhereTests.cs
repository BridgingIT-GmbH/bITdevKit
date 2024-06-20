// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System.Collections.Generic;
using Xunit;
using Shouldly;

[UnitTest("Common")]
public class SafeWhereTests
{
    // Test for SafeWhere with IEnumerable
    [Fact]
    public void SafeWhereIEnumerable_NullSource_ReturnsEmpty()
    {
        // Arrange
        IEnumerable<int> source = null;
        Func<int, bool> predicate = i => i > 0;

        // Act
        var result = source.SafeWhere(predicate);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SafeWhereIEnumerable_ValidSource_ReturnsFiltered()
    {
        // Arrange
        var source = new List<int> { 1, 2, 3, 4 };
        Func<int, bool> predicate = i => i > 2;

        // Act
        var result = source.SafeWhere(predicate);

        // Assert
        result.ShouldBe(new List<int> { 3, 4 });
    }

    // Test for SafeWhere with ICollection
    [Fact]
    public void SafeWhereICollection_NullSource_ReturnsEmpty()
    {
        // Arrange
        ICollection<int> source = null;
        Func<int, bool> predicate = i => i > 0;

        // Act
        var result = source.SafeWhere(predicate);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SafeWhereICollection_ValidSource_ReturnsFiltered()
    {
        // Arrange
        var source = new List<int> { 1, 2, 3, 4 };
        Func<int, bool> predicate = i => i > 2;

        // Act
        var result = source.SafeWhere(predicate);

        // Assert
        result.ShouldBe(new List<int> { 3, 4 });
    }

    // Test for SafeWhere with IDictionary
    [Fact]
    public void SafeWhereIDictionary_NullSource_ReturnsEmpty()
    {
        // Arrange
        IDictionary<int, string> source = null;
        Func<KeyValuePair<int, string>, bool> predicate = kvp => kvp.Key > 0;

        // Act
        var result = source.SafeWhere(predicate);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SafeWhereIDictionary_ValidSource_ReturnsFiltered()
    {
        // Arrange
        var source = new Dictionary<int, string>
            {
                { 1, "one" },
                { 2, "two" },
                { 3, "three" },
                { 4, "four" }
            };
        Func<KeyValuePair<int, string>, bool> predicate = kvp => kvp.Key > 2;

        // Act
        var result = source.SafeWhere(predicate);

        // Assert
        result.ShouldBe(new Dictionary<int, string>
            {
                { 3, "three" },
                { 4, "four" }
            });
    }

    // Non-happy flow test: Predicate throwing exception
    [Fact]
    public void SafeWhereIEnumerable_PredicateThrowsException_ThrowsException()
    {
        // Arrange
        var source = new List<int> { 1, 2, 3, 4 };
        Func<int, bool> predicate = i => throw new InvalidOperationException();

        // Act
        Action act = () => source.SafeWhere(predicate).ToList();

        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void SafeWhereICollection_PredicateThrowsException_ThrowsException()
    {
        // Arrange
        var source = new List<int> { 1, 2, 3, 4 };
        Func<int, bool> predicate = i => throw new InvalidOperationException();

        // Act
        Action act = () => source.SafeWhere(predicate).ToList();

        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void SafeWhereIDictionary_PredicateThrowsException_ThrowsException()
    {
        // Arrange
        var source = new Dictionary<int, string>
            {
                { 1, "one" },
                { 2, "two" },
                { 3, "three" },
                { 4, "four" }
            };
        Func<KeyValuePair<int, string>, bool> predicate = kvp => throw new InvalidOperationException();

        // Act
        Action act = () => source.SafeWhere(predicate).ToList();

        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }
}