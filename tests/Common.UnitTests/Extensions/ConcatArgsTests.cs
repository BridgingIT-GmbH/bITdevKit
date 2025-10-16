// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[UnitTest("Common")]
public class ConcatArgsTests
{
    [Fact]
    public void Concat_BothNull_ReturnsEmptyArray()
    {
        // Arrange
        object[] left = null!;
        object[] right = null!;

        // Act
        var result = left.ConcatArgs(right);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(0);
    }

    [Fact]
    public void Concat_LeftNull_RightNonEmpty_ReturnsRight()
    {
        // Arrange
        object[] left = null!;
        object[] right = [1, "a"];

        // Act
        var result = left.ConcatArgs(right);

        // Assert
        result.ShouldBe([1, "a"]);
        // ensure original not modified
        right.ShouldBe([1, "a"]);
    }

    [Fact]
    public void Concat_LeftNonEmpty_RightNull_ReturnsLeft()
    {
        // Arrange
        object[] left = [1, "a"];
        object[] right = null!;

        // Act
        var result = left.ConcatArgs(right);

        // Assert
        result.ShouldBe([1, "a"]);
        left.ShouldBe([1, "a"]);
    }

    [Fact]
    public void Concat_LeftEmpty_RightEmpty_ReturnsEmpty()
    {
        // Arrange
        var left = Array.Empty<object>();
        var right = Array.Empty<object>();

        // Act
        var result = left.ConcatArgs(right);

        // Assert
        result.ShouldBe([]);
    }

    [Fact]
    public void Concat_LeftEmpty_RightNonEmpty_ReturnsRight()
    {
        // Arrange
        var left = Array.Empty<object>();
        object[] right = [1, 2, 3];

        // Act
        var result = left.ConcatArgs(right);

        // Assert
        result.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Concat_LeftNonEmpty_RightEmpty_ReturnsLeft()
    {
        // Arrange
        object[] left = ["x", "y"];
        var right = Array.Empty<object>();

        // Act
        var result = left.ConcatArgs(right);

        // Assert
        result.ShouldBe(["x", "y"]);
    }

    [Fact]
    public void Concat_MultipleItems_PreservesOrderAndValues()
    {
        // Arrange
        object[] left = ["RES", "Customer", 3, 0];
        object[] right = [123, "foo@bar.com"];

        // Act
        var result = left.ConcatArgs(right);

        // Assert
        result.ShouldBe(["RES", "Customer", 3, 0, 123, "foo@bar.com"]);
    }

    [Fact]
    public void Concat_DoesNotMutateInputs()
    {
        // Arrange
        var left = new object[] { 1, 2 };
        var right = new object[] { 3, 4 };

        // Act
        var result = left.ConcatArgs(right);

        // Assert
        result.ShouldBe([1, 2, 3, 4]);
        left.ShouldBe([1, 2]);
        right.ShouldBe([3, 4]);
    }
}
