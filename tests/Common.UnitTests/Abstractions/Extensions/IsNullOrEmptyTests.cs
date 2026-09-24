// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class IsNullOrEmptyTests
{
    [Fact]
    public void IsNullOrEmpty_Enumerable_ReturnsExpectedResult()
    {
        var nullSource = GetNull<IEnumerable<int>>();
        var emptySource = Enumerable.Empty<int>();
        var populatedSource = Enumerable.Repeat(1, 1);

        nullSource.IsNullOrEmpty().ShouldBeTrue();
        emptySource.IsNullOrEmpty().ShouldBeTrue();
        populatedSource.IsNullOrEmpty().ShouldBeFalse();
    }

    [Fact]
    public void IsNullOrEmpty_Collection_ReturnsExpectedResult()
    {
        var nullSource = GetNull<ICollection<int>>();
        ICollection<int> emptySource = [];
        ICollection<int> populatedSource = [1];

        nullSource.IsNullOrEmpty().ShouldBeTrue();
        emptySource.IsNullOrEmpty().ShouldBeTrue();
        populatedSource.IsNullOrEmpty().ShouldBeFalse();
    }

    [Fact]
    public void IsNullOrEmpty_Stream_ReturnsExpectedResult()
    {
        var nullSource = GetNull<Stream>();
        using var emptySource = new MemoryStream();
        using var populatedSource = new MemoryStream([1]);

        nullSource.IsNullOrEmpty().ShouldBeTrue();
        emptySource.IsNullOrEmpty().ShouldBeTrue();
        populatedSource.IsNullOrEmpty().ShouldBeFalse();
    }

    [Fact]
    public void IsEmpty_Guid_ReturnsExpectedResult()
    {
        Guid.Empty.IsEmpty().ShouldBeTrue();
        Guid.NewGuid().IsEmpty().ShouldBeFalse();
    }

    private static T GetNull<T>() => default;
}