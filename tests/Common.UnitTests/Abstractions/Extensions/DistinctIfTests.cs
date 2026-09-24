// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class DistinctIfTests
{
    [Fact]
    public void DistinctIf_KeySelectorProvided_RetainsFirstItemForEachKey()
    {
        var source = new[]
        {
            new Item(1),
            new Item(1),
            new Item(2)
        };

        var result = source.DistinctIf(item => item.Group).ToArray();

        result.ShouldBe([source[0], source[2]]);
    }

    [Fact]
    public void DistinctIf_NullKeySelector_ReturnsOriginalSequence()
    {
        var source = new[] { new Item(1) }.AsEnumerable();
        var selector = GetNull<Func<Item, object>>();

        var result = source.DistinctIf(selector);

        result.ShouldBeSameAs(source);
    }

    private static T GetNull<T>() => default;

    private sealed record Item(int Group);
}