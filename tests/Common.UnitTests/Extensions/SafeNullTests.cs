// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;
using Shouldly;

[UnitTest("Common")]
public class SafeNullTests
{
    [Fact]
    public void SafeNull_GivenNullEnumerable_ReturnsEmptyEnumerable()
    {
        // Arrange
        IEnumerable<string> source = null;

        // Act
        var safeSource = source.SafeNull();

        // Assert
        safeSource.ShouldNotBeNull();
        safeSource.ShouldBeEmpty();
    }

    [Fact]
    public void SafeNull_GivenEnumerableWithNulls_ReturnsEnumerableWithoutNulls()
    {
        // Arrange
        var source = new List<string> { "one", null, "two", null, "three" };

        // Act
        var safeSource = source.SafeNull();

        // Assert
        safeSource.ShouldNotBeNull();
        safeSource.ShouldBe(new[] { "one", "two", "three" });
    }

    [Fact]
    public void SafeNull_GivenNullCollection_ReturnsEmptyCollection()
    {
        // Arrange
        ICollection<string> source = null;

        // Act
        var safeSource = source.SafeNull();

        // Assert
        safeSource.ShouldNotBeNull();
        safeSource.ShouldBeEmpty();
    }

    [Fact]
    public void SafeNull_GivenCollectionWithNulls_ReturnsCollectionWithoutNulls()
    {
        // Arrange
        var source = new Collection<string> { "one", null, "two", null, "three" };

        // Act
        var safeSource = source.SafeNull();

        // Assert
        safeSource.ShouldNotBeNull();
        safeSource.ShouldBe(new[] { "one", "two", "three" });
    }

    [Fact]
    public void SafeNull_GivenNullDictionary_ReturnsEmptyDictionary()
    {
        // Arrange
        IDictionary<string, int> source = null;

        // Act
        var safeSource = source.SafeNull();

        // Assert
        safeSource.ShouldNotBeNull();
        safeSource.ShouldBeEmpty();
    }

    [Fact]
    public void SafeNull_GivenDictionaryWithNulls_ReturnsDictionaryWithoutNulls()
    {
        // Arrange
        var source = new Dictionary<string, int> { { "one", 1 }, { "two", 2 }, { "three", 3 } };

        // Act
        var safeSource = source.SafeNull();

        // Assert
        safeSource.ShouldNotBeNull();
        safeSource.ShouldBe(new Dictionary<string, int> { { "one", 1 }, { "two", 2 }, { "three", 3 } });
    }

    [Fact]
    public void SafeNull_GivenNullString_ReturnsEmptyString()
    {
        // Arrange
        string source = null;

        // Act
        var safeSource = source.SafeNull();

        // Assert
        safeSource.ShouldNotBeNull();
        safeSource.ShouldBeEmpty();
    }

    [Fact]
    public void SafeNull_GivenNonNullString_ReturnsSameString()
    {
        // Arrange
        var source = "hello world!";

        // Act
        var safeSource = source.SafeNull();

        // Assert
        safeSource.ShouldNotBeNull();
        safeSource.ShouldBe(source);
    }
}